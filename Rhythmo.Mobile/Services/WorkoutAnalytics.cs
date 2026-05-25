using System.Text.Json;
using Rhythmo.Shared.Contracts;

namespace Rhythmo.Mobile.Services;

public static class WorkoutAnalytics
{
	private static readonly JsonSerializerOptions JsonRead = new()
	{
		PropertyNameCaseInsensitive = true,
		ReadCommentHandling = JsonCommentHandling.Skip,
		AllowTrailingCommas = true
	};

	public static double ComputeVolumeKg(WorkoutCompletedRequest? snap) =>
		VolumeFromExerciseList(snap?.Exercises);

	public static double ComputeVolumeKgFromPayload(string? payloadJson)
	{
		var snap = CompletedWorkoutSnapshot.DeserializeRequestSnapshot(payloadJson);
		return ComputeVolumeKg(snap);
	}

	public static double ComputeVolumeKgFromSessionSnapshot(string? sessionSnapshotJson)
	{
		if (string.IsNullOrWhiteSpace(sessionSnapshotJson))
			return 0;
		var env = JsonSerializer.Deserialize<LastRunEnvelope>(sessionSnapshotJson, JsonRead);
		return VolumeFromExerciseList(env?.Exercises);
	}

	private static double VolumeFromExerciseList(IReadOnlyList<CompletedExerciseSetsDto>? exercises)
	{
		if (exercises is null)
			return 0;
		double v = 0;
		foreach (var ex in exercises)
		foreach (var s in ex.Sets)
			v += s.Reps * s.WeightKg;
		return v;
	}
}
