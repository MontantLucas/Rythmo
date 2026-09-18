namespace Rhythmo.Shared.Ranking;

public readonly record struct GroupRankResult(
	string GroupId,
	double? ValidatedRaw,
	int? ValidatedRank,
	int EvaluatedMuscles,
	int TotalMuscles)
{
	public string EvaluatedLabel => $"{EvaluatedMuscles}/{TotalMuscles} muscles évalués";
}

public static class GroupRankAggregator
{
	/// <summary>
	/// Moyenne pondérée des sous-muscles évalués uniquement. Un muscle sans rang validé
	/// n’est jamais traité comme R1. Les coefficients des non-évalués sont ignorés (renormalisation).
	/// </summary>
	public static GroupRankResult Aggregate(
		string groupId,
		IReadOnlyList<(int? ValidatedRank, double Coefficient)> muscles)
	{
		var total = muscles.Count;
		double weighted = 0;
		double coeffSum = 0;
		var evaluated = 0;

		foreach (var (validated, coeff) in muscles)
		{
			if (validated is null || coeff <= 0)
				continue;
			evaluated++;
			weighted += validated.Value * coeff;
			coeffSum += coeff;
		}

		if (evaluated == 0 || coeffSum <= 0)
			return new GroupRankResult(groupId, null, null, 0, total);

		var raw = weighted / coeffSum;
		var displayed = Math.Clamp((int)Math.Floor(raw), ExerciseRankCalculator.MinRank, ExerciseRankCalculator.MaxRank);
		return new GroupRankResult(groupId, raw, displayed, evaluated, total);
	}
}
