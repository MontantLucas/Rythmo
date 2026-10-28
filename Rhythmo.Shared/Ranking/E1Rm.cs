namespace Rhythmo.Shared.Ranking;

/// <summary>e1RM interne (Epley / 30). Ne jamais afficher ni persister comme PR.</summary>
public static class E1Rm
{
	public static double From(double weightKg, int reps)
	{
		if (weightKg <= 0 || reps <= 0)
			return 0;
		return weightKg * (1d + reps / 30d);
	}

	public static double BestOf(IEnumerable<(double WeightKg, int Reps)> sets)
	{
		var best = 0d;
		foreach (var (weightKg, reps) in sets)
		{
			var value = From(weightKg, reps);
			if (value > best)
				best = value;
		}

		return best;
	}
}
