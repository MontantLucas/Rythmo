namespace Rhythmo.Shared.Cardio;

public static class CardioMath
{
	public static bool IsPositive(double distanceM, int durationS) =>
		distanceM > 0 && durationS > 0;

	public static double? PaceSecondsPerKm(double distanceM, int durationS)
	{
		if (!IsPositive(distanceM, durationS))
			return null;
		return durationS / (distanceM / 1000d);
	}

	public static double SumDistanceM(IEnumerable<double> distancesM) =>
		distancesM.Where(d => d > 0).Sum();

	public static int RunningPoints(double distanceM)
	{
		if (distanceM <= 0)
			return 0;
		var km = distanceM / 1000d;
		return (int)Math.Round(10d * Math.Min(km, 5d) / 5d, MidpointRounding.AwayFromZero);
	}
}
