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
		decimal weighted = 0;
		decimal coeffSum = 0;
		var evaluated = 0;

		foreach (var (validated, coeff) in muscles)
		{
			if (validated is null || coeff <= 0)
				continue;
			evaluated++;
			// decimal : 6×0,34 + 6×0,28 / 0,62 vaut 6. En double ça tombe à 5,999… et le floor affiche R5.
			var weight = (decimal)coeff;
			weighted += validated.Value * weight;
			coeffSum += weight;
		}

		if (evaluated == 0 || coeffSum <= 0)
			return new GroupRankResult(groupId, null, null, 0, total);

		var rawDecimal = weighted / coeffSum;
		var raw = (double)rawDecimal;
		var displayed = Math.Clamp(
			(int)Math.Floor(rawDecimal),
			ExerciseRankCalculator.MinRank,
			ExerciseRankCalculator.MaxRank);
		return new GroupRankResult(groupId, raw, displayed, evaluated, total);
	}
}
