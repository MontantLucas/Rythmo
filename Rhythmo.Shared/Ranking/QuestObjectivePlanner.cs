namespace Rhythmo.Shared.Ranking;

public readonly record struct QuestSetSuggestion(double WeightKg, int Reps);

/// <summary>
/// Cible de quête affichable. V1 : une seule option (rang validé + 1).
/// La liste est prévue pour une sélection future, sans jamais sauter de rangs validés.
/// </summary>
public sealed record QuestTargetOption(
	int Rank,
	QuestSetSuggestion Primary,
	IReadOnlyList<QuestSetSuggestion> Equivalences);

/// <summary>
/// Convertit un seuil e1RM en combinaisons kg×reps affichables.
/// R5+ : une seule cible (reps du MeasurementType). R1–R4 : alternatives 1/5/10 RM.
/// </summary>
public static class QuestObjectivePlanner
{
	public const int MaxEquivalences = 2;

	public static IReadOnlyList<QuestTargetOption> AvailableTargets(
		double r10Kg,
		int? theoreticalRank,
		int? validatedRank,
		MeasurementType measurement,
		LoadMode loadMode)
	{
		var availability = QuestRules.Availability(theoreticalRank, validatedRank);
		if (!availability.IsAvailable || availability.TargetRank is not { } target)
			return [];

		return [ForRank(r10Kg, target, measurement, loadMode)];
	}

	public static QuestTargetOption ForRank(
		double r10Kg,
		int targetRank,
		MeasurementType measurement,
		LoadMode loadMode)
	{
		var threshold = ExerciseRankCalculator.RankThreshold(r10Kg, targetRank);
		var primaryReps = measurement switch
		{
			MeasurementType.OneRm => 1,
			MeasurementType.TenRm => 10,
			_ => 5
		};

		var primary = Suggest(threshold, primaryReps, loadMode);
		var extras = new List<QuestSetSuggestion>(MaxEquivalences);
		if (targetRank < QuestRules.ExactObjectiveFromRank)
		{
			foreach (var reps in EquivalenceReps(primaryReps))
			{
				if (extras.Count >= MaxEquivalences)
					break;
				var suggestion = Suggest(threshold, reps, loadMode);
				if (suggestion.WeightKg == primary.WeightKg && suggestion.Reps == primary.Reps)
					continue;
				if (extras.Exists(x => x.WeightKg == suggestion.WeightKg && x.Reps == suggestion.Reps))
					continue;
				extras.Add(suggestion);
			}
		}

		return new QuestTargetOption(targetRank, primary, extras);
	}

	public static QuestSetSuggestion Suggest(double thresholdE1Rm, int reps, LoadMode loadMode)
	{
		if (thresholdE1Rm <= 0 || reps <= 0)
			return new QuestSetSuggestion(0, Math.Max(1, reps));

		var raw = thresholdE1Rm / (1d + reps / 30d);
		var increment = Increment(loadMode, raw);
		var kg = RoundUp(raw, increment);
		while (E1Rm.From(kg, reps) < thresholdE1Rm - 1e-9)
			kg += increment;

		return new QuestSetSuggestion(kg, reps);
	}

	internal static double Increment(LoadMode loadMode, double rawKg)
	{
		if (rawKg < 10)
			return loadMode == LoadMode.PerDumbbell ? 1d : 1.25;
		return 2.5;
	}

	internal static double RoundUp(double value, double increment)
	{
		if (increment <= 0)
			return value;
		return Math.Ceiling(value / increment - 1e-9) * increment;
	}

	private static int[] EquivalenceReps(int primaryReps) =>
		new[] { 1, 5, 10 }.Where(r => r != primaryReps).ToArray();
}
