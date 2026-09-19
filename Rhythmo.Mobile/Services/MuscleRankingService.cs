using Rhythmo.Mobile.Data;
using Rhythmo.Shared.Contracts;
using Rhythmo.Shared.Ranking;

namespace Rhythmo.Mobile.Services;

public sealed record NewlyUnlockedQuest(Guid ExerciseId, int TargetRank, int? ValidatedRank);

public sealed record RankingRefreshResult(IReadOnlyList<NewlyUnlockedQuest> Unlocked)
{
	public int NewlyUnlockedQuests => Unlocked.Count;
}

public sealed class MuscleRankingService(IRhythmoRepository repo)
{
	public async Task<RankingRefreshResult> RefreshAsync(Guid profileId, CancellationToken ct = default)
	{
		var profile = await repo.GetProfileAsync(profileId, ct).ConfigureAwait(false)
		              ?? throw new InvalidOperationException("Profil introuvable.");
		var exercises = await repo.ListExercisesAsync(ct).ConfigureAwait(false);
		var defs = RankingExerciseIndex.Map(exercises);
		var existing = (await repo.ListProfileExerciseRanksAsync(profileId, ct).ConfigureAwait(false))
			.ToDictionary(r => r.ExerciseId);
		var workouts = await repo.ListCompletedWorkoutsAsync(profileId, ct).ConfigureAwait(false);
		var attempts = await repo.ListQuestAttemptsAsync(profileId, ct).ConfigureAwait(false);
		var localToday = DateOnly.FromDateTime(DateTime.Now);
		var sex = profile.BiologicalSex == BiologicalSex.Female
			? BiologicalSexKind.Female
			: BiologicalSexKind.Male;

		var bestE1Rm = BestE1RmByExercise(workouts, attempts, defs.Keys);

		var newlyUnlocked = new List<NewlyUnlockedQuest>();
		var now = DateTime.UtcNow;
		var nextStates = new Dictionary<Guid, ProfileExerciseRankRow>();

		foreach (var (exerciseId, def) in defs)
		{
			if (!def.IsClassifying)
				continue;

			bestE1Rm.TryGetValue(exerciseId, out var e1Rm);
			var r10 = BodyweightStandardSelector.SelectR10(
				sex, profile.WeightKg,
				def.R10Male70!.Value, def.R10Male100!.Value,
				def.R10Female60!.Value, def.R10Female90!.Value);
			var computation = ExerciseRankCalculator.Evaluate(e1Rm, r10);

			existing.TryGetValue(exerciseId, out var prevRow);
			var prevState = prevRow is null
				? (ExerciseRankState?)null
				: ToState(prevRow);

			var state = ExerciseRankProgression.ApplyTheoretical(prevState, computation);
			var attemptedToday = attempts.Any(a => a.ExerciseId == exerciseId && a.LocalDate == localToday);
			if (attemptedToday)
				state = state with { AvailableQuestRank = null };

			var wasLocked = prevRow?.AvailableQuestRank is null;
			if (wasLocked && state.AvailableQuestRank is not null)
				newlyUnlocked.Add(new NewlyUnlockedQuest(
					exerciseId,
					state.AvailableQuestRank.Value,
					state.ValidatedRank));

			nextStates[exerciseId] = new ProfileExerciseRankRow
			{
				ProfileId = profileId,
				ExerciseId = exerciseId,
				TheoreticalRaw = state.TheoreticalRaw,
				TheoreticalRank = state.TheoreticalRank,
				ValidatedRank = state.ValidatedRank,
				HasSuccessfulValidation = state.HasSuccessfulValidation,
				CalibrationFloor = state.CalibrationFloor,
				AvailableQuestRank = state.AvailableQuestRank,
				QuestUnlockedUtc = state.AvailableQuestRank is null
					? null
					: prevRow?.QuestUnlockedUtc ?? now,
				BodyweightKgAtCompute = profile.WeightKg,
				SexAtCompute = (int)profile.BiologicalSex,
				R10Used = r10,
				StandardVersionId = MuscleIds.StandardVersionId,
				UpdatedUtc = now
			};
		}

		foreach (var row in nextStates.Values)
		{
			var hadPrev = existing.ContainsKey(row.ExerciseId);
			if (!hadPrev && !bestE1Rm.ContainsKey(row.ExerciseId))
				continue;
			await repo.UpsertProfileExerciseRankAsync(row, ct).ConfigureAwait(false);
		}

		await PersistAggregatesAsync(profileId, nextStates, defs, now, ct).ConfigureAwait(false);
		return new RankingRefreshResult(newlyUnlocked);
	}

	public async Task PersistAfterQuestAsync(ProfileExerciseRankRow row, CancellationToken ct = default)
	{
		await repo.UpsertProfileExerciseRankAsync(row, ct).ConfigureAwait(false);
		var ranks = await repo.ListProfileExerciseRanksAsync(row.ProfileId, ct).ConfigureAwait(false);
		var exercises = await repo.ListExercisesAsync(ct).ConfigureAwait(false);
		var defs = RankingExerciseIndex.Map(exercises);
		var map = ranks.ToDictionary(r => r.ExerciseId);
		await PersistAggregatesAsync(row.ProfileId, map, defs, DateTime.UtcNow, ct).ConfigureAwait(false);
	}

	private async Task PersistAggregatesAsync(
		Guid profileId,
		IReadOnlyDictionary<Guid, ProfileExerciseRankRow> ranks,
		IReadOnlyDictionary<Guid, ExerciseRankingDef> defs,
		DateTime now,
		CancellationToken ct)
	{
		var version = MuscleIds.StandardVersionId;
		foreach (var muscle in MuscleIds.Muscles)
		{
			var inputs = new List<(bool IsPrimary, int? ValidatedRank, int? TheoreticalRank)>();
			foreach (var (exerciseId, def) in defs)
			{
				if (!ranks.TryGetValue(exerciseId, out var rank))
					continue;
				var isPrimary = def.PrimaryMuscles.Contains(muscle.Id);
				inputs.Add((isPrimary, rank.ValidatedRank, rank.TheoreticalRank));
			}

			var agg = MuscleRankAggregator.Aggregate(muscle.Id, inputs);
			await repo.UpsertMuscleRankSnapshotAsync(new MuscleRankSnapshotRow
			{
				ProfileId = profileId,
				MuscleId = muscle.Id,
				StandardVersionId = version,
				ValidatedRank = agg.ValidatedRank,
				TheoreticalRank = agg.TheoreticalRank,
				EvaluatedCount = agg.ClassifiedPrimaryCount,
				UpdatedUtc = now
			}, ct).ConfigureAwait(false);
		}

		var muscleSnaps = (await repo.ListMuscleRankSnapshotsAsync(profileId, version, ct).ConfigureAwait(false))
			.ToDictionary(s => s.MuscleId);

		foreach (var group in MuscleIds.Groups)
		{
			var members = MuscleIds.Muscles.Where(m => m.GroupId == group.Id).ToList();
			var inputs = members.Select(m =>
			{
				muscleSnaps.TryGetValue(m.Id, out var snap);
				var coeff = MuscleIds.V1Coefficients[m.Id];
				return (snap?.ValidatedRank, coeff);
			}).ToList();

			var agg = GroupRankAggregator.Aggregate(group.Id, inputs);
			await repo.UpsertGroupRankSnapshotAsync(new GroupRankSnapshotRow
			{
				ProfileId = profileId,
				GroupId = group.Id,
				StandardVersionId = version,
				ValidatedRaw = agg.ValidatedRaw,
				ValidatedRank = agg.ValidatedRank,
				EvaluatedCount = agg.EvaluatedMuscles,
				TotalCount = agg.TotalMuscles,
				UpdatedUtc = now
			}, ct).ConfigureAwait(false);
		}
	}

	private static Dictionary<Guid, double> BestE1RmByExercise(
		IReadOnlyList<CompletedWorkoutRow> workouts,
		IReadOnlyList<RankQuestAttemptRow> attempts,
		IEnumerable<Guid> classifyingIds)
	{
		var ids = classifyingIds.ToHashSet();
		var best = new Dictionary<Guid, double>();

		void Consider(Guid id, double e1)
		{
			if (e1 <= 0 || !ids.Contains(id))
				return;
			if (!best.TryGetValue(id, out var prev) || e1 > prev)
				best[id] = e1;
		}

		foreach (var workout in workouts)
		{
			var payload = CompletedWorkoutSnapshot.DeserializeRequestSnapshot(workout.PayloadJson);
			if (payload?.Exercises is null)
				continue;
			foreach (var ex in payload.Exercises)
			{
				foreach (var set in ex.Sets)
					Consider(ex.ExerciseId, E1Rm.From(set.WeightKg, set.Reps));
			}
		}

		foreach (var attempt in attempts.Where(a => a.Status == "succeeded" && a.WeightKg is > 0 && a.Reps is > 0))
			Consider(attempt.ExerciseId, E1Rm.From(attempt.WeightKg!.Value, attempt.Reps!.Value));

		return best;
	}

	private static ExerciseRankState ToState(ProfileExerciseRankRow row) => new(
		row.TheoreticalRaw,
		row.TheoreticalRank,
		row.ValidatedRank,
		row.HasSuccessfulValidation,
		row.CalibrationFloor,
		row.AvailableQuestRank);
}
