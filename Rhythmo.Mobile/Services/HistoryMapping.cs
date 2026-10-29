using Rhythmo.Mobile.Data;
using Rhythmo.Shared.Challenges;
using Rhythmo.Shared.Contracts;
using Rhythmo.Shared.History;

namespace Rhythmo.Mobile.Services;

internal static class HistoryMapping
{
	public static HistoryWorkout ToHistory(CompletedWorkoutRow row)
	{
		var snap = CompletedWorkoutSnapshot.DeserializeRequestSnapshot(row.PayloadJson);
		return new HistoryWorkout(row.Id, row.CompletedUtc, row.SessionTitle ?? "", Blocks(snap));
	}

	public static LoggedWorkout ToLogged(CompletedWorkoutRow row)
	{
		var snap = CompletedWorkoutSnapshot.DeserializeRequestSnapshot(row.PayloadJson);
		var sets = Blocks(snap)
			.SelectMany(b => b.Sets.Select(s => new LoggedSet(b.ExerciseId, s.Reps, s.WeightKg)))
			.ToList();
		return new LoggedWorkout(row.Id, row.CompletedUtc, sets);
	}

	private static IReadOnlyList<HistoryExerciseBlock> Blocks(WorkoutCompletedRequest? snap)
	{
		if (snap?.Exercises is not { Count: > 0 })
			return [];
		return snap.Exercises
			.Select(e => new HistoryExerciseBlock(
				e.ExerciseId,
				(e.Sets ?? []).Select(s => new HistorySet(s.Reps, s.WeightKg)).ToList()))
			.ToList();
	}
}
