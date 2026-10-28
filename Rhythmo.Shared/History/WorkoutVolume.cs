namespace Rhythmo.Shared.History;

public static class WorkoutVolume
{
	public static double Sum(IEnumerable<(int Reps, double WeightKg)> sets)
	{
		double v = 0;
		foreach (var (reps, kg) in sets)
			v += reps * kg;
		return v;
	}
}
