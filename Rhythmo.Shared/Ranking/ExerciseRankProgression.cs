namespace Rhythmo.Shared.Ranking;

public readonly record struct ExerciseRankState(
	double TheoreticalRaw,
	int? TheoreticalRank,
	int? ValidatedRank,
	bool HasSuccessfulValidation,
	int? CalibrationFloor,
	int? AvailableQuestRank);

public static class ExerciseRankProgression
{
	/// <summary>
	/// Recalcule le théorique. N’initialise le validé qu’à la première perf classifiante.
	/// Le validé ne redescend pas si le poids / R10 change.
	/// </summary>
	public static ExerciseRankState ApplyTheoretical(
		ExerciseRankState? previous,
		ExerciseRankComputation computation)
	{
		var prev = previous ?? default;

		if (!computation.IsClassified)
		{
			var keptValidated = prev.ValidatedRank;
			return new ExerciseRankState(
				computation.TheoreticalRaw,
				null,
				keptValidated,
				prev.HasSuccessfulValidation,
				prev.CalibrationFloor,
				QuestRules.Availability(null, keptValidated).IsAvailable
					? QuestRules.Availability(null, keptValidated).TargetRank
					: null);
		}

		int validated;
		var hasSuccess = prev.HasSuccessfulValidation;
		var floor = prev.CalibrationFloor;

		if (prev.ValidatedRank is null)
			validated = ExerciseRankCalculator.InitialValidatedRank(computation.TheoreticalRank!.Value);
		else
			validated = prev.ValidatedRank.Value;

		var availability = QuestRules.Availability(computation.TheoreticalRank, validated);
		return new ExerciseRankState(
			computation.TheoreticalRaw,
			computation.TheoreticalRank,
			validated,
			hasSuccess,
			floor,
			availability.IsAvailable ? availability.TargetRank : null);
	}

	public static ExerciseRankState ApplyQuestSuccess(ExerciseRankState state)
	{
		if (state.ValidatedRank is null)
			return state;

		var next = QuestRules.ApplySuccess(state.ValidatedRank.Value);
		var availability = QuestRules.Availability(state.TheoreticalRank, next);
		return state with
		{
			ValidatedRank = next,
			HasSuccessfulValidation = true,
			CalibrationFloor = next,
			AvailableQuestRank = null
		};
	}

	public static ExerciseRankState ApplyQuestFailure(ExerciseRankState state)
	{
		if (state.ValidatedRank is null)
			return state;

		var next = QuestRules.ApplyFailure(
			state.ValidatedRank.Value,
			state.HasSuccessfulValidation,
			state.CalibrationFloor);
		var availability = QuestRules.Availability(state.TheoreticalRank, next);
		return state with
		{
			ValidatedRank = next,
			AvailableQuestRank = availability.IsAvailable ? availability.TargetRank : null
		};
	}
}
