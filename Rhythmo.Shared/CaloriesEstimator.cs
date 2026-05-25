namespace Rhythmo.Shared;

/// <summary>Estimation MET × durée + métabolisme de base (Mifflin-St Jeor). Usage affichage, pas conseil santé.</summary>
public static class CaloriesEstimator
{
	public const double MinutesPerStrengthSet = 2.75;

	public static double EstimateSessionKcal(double weightKg,
		IEnumerable<(double Met, int SetCount)> exerciseChunks) =>
		EstimateSessionKcal(new CaloriesSubject { WeightKg = weightKg }, exerciseChunks);

	public static double EstimateSessionKcal(CaloriesSubject subject,
		IEnumerable<(double Met, int SetCount)> exerciseChunks)
	{
		var weight = subject.WeightKg > 0 ? subject.WeightKg : 75;
		double activeKcal = 0;
		var totalMinutes = 0d;

		foreach (var (met, count) in exerciseChunks)
		{
			if (count <= 0)
				continue;
			var mins = count * MinutesPerStrengthSet;
			totalMinutes += mins;
			activeKcal += KcalPerMinute(weight, met) * mins;
		}

		if (totalMinutes <= 0)
			return 0;

		var restingKcal = RestingKcalDuringSession(subject, totalMinutes);
		return Math.Round(activeKcal + restingKcal);
	}

	/// <summary>BMR journalier (Mifflin-St Jeor), kcal/jour.</summary>
	public static double EstimateBmrKcalPerDay(CaloriesSubject subject)
	{
		var weight = subject.WeightKg > 0 ? subject.WeightKg : 75;
		var height = subject.HeightCm is > 0 ? subject.HeightCm.Value : subject.IsFemale ? 160 : 175;
		var age = subject.AgeYears is > 0 and < 120 ? subject.AgeYears.Value : 30;
		var baseBmr = 10 * weight + 6.25 * height - 5 * age;
		return subject.IsFemale ? baseBmr - 161 : baseBmr + 5;
	}

	private static double RestingKcalDuringSession(CaloriesSubject subject, double minutes) =>
		EstimateBmrKcalPerDay(subject) / 1440d * minutes;

	private static double KcalPerMinute(double kg, double met) => met * kg * (3.5 / 200.0);
}
