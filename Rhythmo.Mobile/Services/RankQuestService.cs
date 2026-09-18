using Rhythmo.Mobile.Data;
using Rhythmo.Shared.Ranking;

namespace Rhythmo.Mobile.Services;

public sealed record QuestStartResult(bool Ok, string? Error, RankQuestAttemptRow? Attempt);

public sealed class RankQuestService(IRhythmoRepository repo, MuscleRankingService ranking)
{
	public async Task ExpireStaleAsync(Guid profileId, CancellationToken ct = default)
	{
		var attempts = await repo.ListQuestAttemptsAsync(profileId, ct).ConfigureAwait(false);
		foreach (var current in attempts.Where(a => a.Status == "in_progress"))
		{
			var sibling = attempts
				.Where(a => a.Id != current.Id
				            && a.ExerciseId == current.ExerciseId
				            && a.LocalDate == current.LocalDate
				            && a.Status is "succeeded" or "failed_expired" or "failed_user")
				.OrderByDescending(a => a.StartedUtc)
				.FirstOrDefault();
			if (sibling is not null)
			{
				current.Status = sibling.Status;
				current.WeightKg = sibling.WeightKg;
				current.Reps = sibling.Reps;
				await repo.UpdateQuestAttemptAsync(current, ct).ConfigureAwait(false);
				continue;
			}

			if (QuestRules.IsExpired(current.StartedUtc, current.ExpiresUtc, DateTime.UtcNow))
				await FailAsync(current, "failed_expired", ct).ConfigureAwait(false);
		}
	}

	public async Task<QuestStartResult> TryStartAsync(Guid profileId, Guid exerciseId, CancellationToken ct = default)
	{
		await ExpireStaleAsync(profileId, ct).ConfigureAwait(false);

		var inProgress = await repo.GetInProgressQuestAsync(profileId, ct).ConfigureAwait(false);
		if (inProgress is not null)
			return new QuestStartResult(false, "Une quête est déjà en cours.", null);

		var exercises = await repo.ListExercisesAsync(ct).ConfigureAwait(false);
		var defs = RankingExerciseIndex.Map(exercises);
		if (!defs.TryGetValue(exerciseId, out var def) || !def.IsClassifying)
			return new QuestStartResult(false, "Exercice non classifiant.", null);

		var ranks = await repo.ListProfileExerciseRanksAsync(profileId, ct).ConfigureAwait(false);
		var rank = ranks.FirstOrDefault(r => r.ExerciseId == exerciseId);
		var localToday = DateOnly.FromDateTime(DateTime.Now);
		var attemptedToday = await repo.HasQuestAttemptOnLocalDateAsync(profileId, exerciseId, localToday, ct)
			.ConfigureAwait(false);

		var target = rank?.AvailableQuestRank
		             ?? QuestRules.Availability(rank?.TheoreticalRank, rank?.ValidatedRank).TargetRank
		             ?? 0;

		var check = QuestRules.CanStart(
			rank?.AvailableQuestRank is not null,
			def.IsClassifying,
			rank?.ValidatedRank is not null,
			rank?.StandardVersionId == MuscleIds.StandardVersionId,
			rank?.TheoreticalRank,
			rank?.ValidatedRank,
			target,
			attemptedToday);

		if (!check.CanStart)
			return new QuestStartResult(false, check.DenialReason, null);

		var now = DateTime.UtcNow;
		var attempt = new RankQuestAttemptRow
		{
			Id = Guid.NewGuid(),
			ProfileId = profileId,
			ExerciseId = exerciseId,
			TargetRank = target,
			StartedUtc = now,
			ExpiresUtc = now.Add(QuestRules.TimerDuration),
			LocalDate = localToday,
			Status = "in_progress",
			StandardVersionId = MuscleIds.StandardVersionId
		};
		await repo.InsertQuestAttemptAsync(attempt, ct).ConfigureAwait(false);
		return new QuestStartResult(true, null, attempt);
	}

	public async Task<bool> TryCompleteSuccessAsync(
		RankQuestAttemptRow attempt,
		double weightKg,
		int reps,
		CancellationToken ct = default)
	{
		if (QuestRules.IsExpired(attempt.StartedUtc, attempt.ExpiresUtc, DateTime.UtcNow))
		{
			await FailAsync(attempt, "failed_expired", ct).ConfigureAwait(false);
			return false;
		}

		var exercises = await repo.ListExercisesAsync(ct).ConfigureAwait(false);
		var defs = RankingExerciseIndex.Map(exercises);
		if (!defs.TryGetValue(attempt.ExerciseId, out var def) || !def.IsClassifying)
			return false;

		var ranks = await repo.ListProfileExerciseRanksAsync(attempt.ProfileId, ct).ConfigureAwait(false);
		var rank = ranks.FirstOrDefault(r => r.ExerciseId == attempt.ExerciseId);
		if (rank?.R10Used is null or <= 0)
			return false;

		var option = QuestObjectivePlanner.ForRank(
			rank.R10Used.Value,
			attempt.TargetRank,
			def.MeasurementType ?? MeasurementType.FiveRm,
			def.LoadMode);
		if (!QuestRules.CompletesDisplayedObjective(
			    attempt.TargetRank, option.Primary, weightKg, reps, rank.R10Used.Value))
			return false;

		attempt.WeightKg = weightKg;
		attempt.Reps = reps;
		attempt.Status = "succeeded";
		await repo.UpdateQuestAttemptAsync(attempt, ct).ConfigureAwait(false);

		var next = ExerciseRankProgression.ApplyQuestSuccess(new ExerciseRankState(
			rank.TheoreticalRaw,
			rank.TheoreticalRank,
			rank.ValidatedRank,
			rank.HasSuccessfulValidation,
			rank.CalibrationFloor,
			rank.AvailableQuestRank));

		rank.ValidatedRank = next.ValidatedRank;
		rank.HasSuccessfulValidation = next.HasSuccessfulValidation;
		rank.CalibrationFloor = next.CalibrationFloor;
		rank.AvailableQuestRank = null;
		rank.QuestUnlockedUtc = null;
		rank.UpdatedUtc = DateTime.UtcNow;
		await ranking.PersistAfterQuestAsync(rank, ct).ConfigureAwait(false);
		return true;
	}

	public Task FailAsync(RankQuestAttemptRow attempt, string status, CancellationToken ct = default) =>
		FailCoreAsync(attempt, status, ct);

	public Task AbandonAsync(RankQuestAttemptRow attempt, CancellationToken ct = default) =>
		FailCoreAsync(attempt, "failed_user", ct);

	private async Task FailCoreAsync(RankQuestAttemptRow attempt, string status, CancellationToken ct)
	{
		if (attempt.Status is "succeeded" or "failed_expired" or "failed_user")
			return;

		attempt.Status = status;
		await repo.UpdateQuestAttemptAsync(attempt, ct).ConfigureAwait(false);

		var ranks = await repo.ListProfileExerciseRanksAsync(attempt.ProfileId, ct).ConfigureAwait(false);
		var rank = ranks.FirstOrDefault(r => r.ExerciseId == attempt.ExerciseId);
		if (rank?.ValidatedRank is null)
			return;

		var next = ExerciseRankProgression.ApplyQuestFailure(new ExerciseRankState(
			rank.TheoreticalRaw,
			rank.TheoreticalRank,
			rank.ValidatedRank,
			rank.HasSuccessfulValidation,
			rank.CalibrationFloor,
			rank.AvailableQuestRank));

		rank.ValidatedRank = next.ValidatedRank;
		rank.AvailableQuestRank = null;
		rank.QuestUnlockedUtc = null;
		rank.UpdatedUtc = DateTime.UtcNow;
		await ranking.PersistAfterQuestAsync(rank, ct).ConfigureAwait(false);
	}
}
