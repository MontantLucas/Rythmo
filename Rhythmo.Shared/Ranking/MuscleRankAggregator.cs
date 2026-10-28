namespace Rhythmo.Shared.Ranking;

public readonly record struct MuscleRankResult(
	string MuscleId,
	int? ValidatedRank,
	int? TheoreticalRank,
	int ClassifiedPrimaryCount);

public static class MuscleRankAggregator
{
	/// <summary>Meilleur rang validé parmi les exercices Primary classés. Secondary ignorés.</summary>
	public static MuscleRankResult Aggregate(
		string muscleId,
		IEnumerable<(bool IsPrimary, int? ValidatedRank, int? TheoreticalRank)> exercises)
	{
		int? bestValidated = null;
		int? bestTheoretical = null;
		var classified = 0;

		foreach (var (isPrimary, validated, theoretical) in exercises)
		{
			if (!isPrimary || validated is null)
				continue;

			classified++;
			if (bestValidated is null || validated.Value > bestValidated.Value)
				bestValidated = validated;
			if (theoretical is not null &&
			    (bestTheoretical is null || theoretical.Value > bestTheoretical.Value))
				bestTheoretical = theoretical;
		}

		return new MuscleRankResult(muscleId, bestValidated, bestTheoretical, classified);
	}
}
