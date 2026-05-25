using System.Globalization;
using Rhythmo.Mobile.Data;
using Rhythmo.Mobile.Services;
using Rhythmo.Shared.Contracts;
using Supabase.Postgrest.Exceptions;

namespace Rhythmo.Mobile.Social;

/// <summary>Détecte et persiste les PR à la finalisation de séance (tables <c>exercise_personal_bests</c> + <c>pr_events</c>).</summary>
public sealed class PersonalRecordService
{
	public const int MinSetsForPr = 5;

	public async Task ProcessCompletedWorkoutAsync(
		IRhythmoRepository repo,
		Guid profileId,
		Guid completedWorkoutId,
		DateTime achievedUtc,
		IReadOnlyList<CompletedExerciseSetsDto> exercises,
		int totalFilledSets,
		CancellationToken ct = default)
	{
		if (totalFilledSets < MinSetsForPr || exercises.Count == 0)
			return;

		foreach (var ex in exercises)
		{
			if (ex.Sets is not { Count: > 0 })
				continue;

			var sets = ex.Sets;
			var maxKg = sets.Max(s => s.WeightKg);
			var maxReps = sets.Max(s => s.Reps);
			var sessionVol = sets.Sum(s => s.Reps * s.WeightKg);

			var prev = await repo.GetExercisePersonalBestAsync(profileId, ex.ExerciseId, ct)
				.ConfigureAwait(false);

			if (prev is null)
			{
				var bestSet = sets.OrderByDescending(s => s.WeightKg).First();
				await InsertPrEventSafeAsync(repo, new PrEventRow
				{
					Id = Guid.NewGuid(),
					ProfileId = profileId,
					ExerciseId = ex.ExerciseId,
					Kind = "weight",
					WeightKg = bestSet.WeightKg,
					Reps = bestSet.Reps,
					PerformanceLine =
						$"{bestSet.WeightKg.ToString("0.#", CultureInfo.InvariantCulture)} kg × {bestSet.Reps}",
					CompletedWorkoutId = completedWorkoutId,
					AchievedUtc = achievedUtc
				}, ct).ConfigureAwait(false);

				await repo.UpsertExercisePersonalBestAsync(new ExercisePersonalBestRow
				{
					ProfileId = profileId,
					ExerciseId = ex.ExerciseId,
					MaxKg = maxKg,
					MaxReps = maxReps,
					MaxSessionVolume = sessionVol,
					UpdatedUtc = achievedUtc
				}, ct).ConfigureAwait(false);
				continue;
			}

			PrEventRow? pr = null;
			if (maxKg > prev.MaxKg + 0.01)
			{
				var bestSet = sets.OrderByDescending(s => s.WeightKg).First();
				pr = new PrEventRow
				{
					Id = Guid.NewGuid(),
					ProfileId = profileId,
					ExerciseId = ex.ExerciseId,
					Kind = "weight",
					WeightKg = bestSet.WeightKg,
					Reps = bestSet.Reps,
					PerformanceLine =
						$"{bestSet.WeightKg.ToString("0.#", CultureInfo.InvariantCulture)} kg × {bestSet.Reps}",
					CompletedWorkoutId = completedWorkoutId,
					AchievedUtc = achievedUtc
				};
			}
			else if (maxReps > prev.MaxReps)
			{
				var bestSet = sets.OrderByDescending(s => s.Reps).First();
				pr = new PrEventRow
				{
					Id = Guid.NewGuid(),
					ProfileId = profileId,
					ExerciseId = ex.ExerciseId,
					Kind = "reps",
					WeightKg = bestSet.WeightKg,
					Reps = bestSet.Reps,
					PerformanceLine =
						$"{bestSet.WeightKg.ToString("0.#", CultureInfo.InvariantCulture)} kg × {bestSet.Reps}",
					CompletedWorkoutId = completedWorkoutId,
					AchievedUtc = achievedUtc
				};
			}
			else if (sessionVol > prev.MaxSessionVolume + 0.01)
			{
				pr = new PrEventRow
				{
					Id = Guid.NewGuid(),
					ProfileId = profileId,
					ExerciseId = ex.ExerciseId,
					Kind = "volume",
					PerformanceLine = $"volume · {FormatVolume(sessionVol)}",
					CompletedWorkoutId = completedWorkoutId,
					AchievedUtc = achievedUtc
				};
			}

			if (pr is not null)
				await InsertPrEventSafeAsync(repo, pr, ct).ConfigureAwait(false);

			await repo.UpsertExercisePersonalBestAsync(new ExercisePersonalBestRow
			{
				ProfileId = profileId,
				ExerciseId = ex.ExerciseId,
				MaxKg = Math.Max(prev.MaxKg, maxKg),
				MaxReps = Math.Max(prev.MaxReps, maxReps),
				MaxSessionVolume = Math.Max(prev.MaxSessionVolume, sessionVol),
				UpdatedUtc = achievedUtc
			}, ct).ConfigureAwait(false);
		}
	}

	internal static string FormatVolume(double kg) =>
		kg >= 1000 ? $"{kg / 1000d:0.#} t" : $"{kg:0} kg";

	private static async Task InsertPrEventSafeAsync(
		IRhythmoRepository repo, PrEventRow pr, CancellationToken ct)
	{
		try
		{
			await repo.InsertPrEventAsync(pr, ct).ConfigureAwait(false);
		}
		catch (PostgrestException ex) when (IsMissingWorkoutFk(ex) && pr.CompletedWorkoutId is not null)
		{
			pr.CompletedWorkoutId = null;
			await repo.InsertPrEventAsync(pr, ct).ConfigureAwait(false);
		}
	}

	private static bool IsMissingWorkoutFk(PostgrestException ex) =>
		ex.Message.Contains("23503", StringComparison.Ordinal) &&
		ex.Message.Contains("pr_events_completed_workout_id_fkey", StringComparison.OrdinalIgnoreCase);
}
