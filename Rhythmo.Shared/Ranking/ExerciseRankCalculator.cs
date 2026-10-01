namespace Rhythmo.Shared.Ranking;

public readonly record struct ExerciseRankComputation(
	RankClassification Classification,
	double TheoreticalRaw,
	int? TheoreticalRank)
{
	public bool IsClassified => Classification == RankClassification.Classified;
}

public static class ExerciseRankCalculator
{
	public const int MinRank = 1;
	public const int MaxRank = 10;
	public const double R1FractionOfR10 = 0.10;

	public static double Rank1Threshold(double r10Kg) => r10Kg * R1FractionOfR10;

	public static double RankThreshold(double r10Kg, int rank) =>
		r10Kg * (Math.Clamp(rank, MinRank, MaxRank) / 10d);

	public static ExerciseRankComputation Evaluate(double e1RmKg, double r10Kg)
	{
		if (e1RmKg <= 0 || r10Kg <= 0)
			return new ExerciseRankComputation(RankClassification.NoPerformance, 0, null);

		var raw = 10d * e1RmKg / r10Kg;
		if (e1RmKg < Rank1Threshold(r10Kg))
			return new ExerciseRankComputation(RankClassification.BelowR1, raw, null);

		var floor = (int)Math.Floor(raw);
		var theoretical = Math.Clamp(floor, MinRank, MaxRank);
		return new ExerciseRankComputation(RankClassification.Classified, raw, theoretical);
	}

	public static int InitialValidatedRank(int theoreticalRank) =>
		Math.Max(MinRank, theoreticalRank - 3);
}
