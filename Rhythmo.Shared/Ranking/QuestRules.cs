namespace Rhythmo.Shared.Ranking;

public readonly record struct QuestAvailability(
	bool IsAvailable,
	int? TargetRank);

public readonly record struct QuestStartCheck(
	bool CanStart,
	string? DenialReason);

public static class QuestRules
{
	public static readonly TimeSpan TimerDuration = TimeSpan.FromMinutes(5);

	public const int ExactObjectiveFromRank = 5;

	public static QuestAvailability Availability(int? theoreticalRank, int? validatedRank)
	{
		if (theoreticalRank is null || validatedRank is null)
			return new QuestAvailability(false, null);
		if (validatedRank.Value >= ExerciseRankCalculator.MaxRank)
			return new QuestAvailability(false, null);
		var target = validatedRank.Value + 1;
		if (theoreticalRank.Value < target)
			return new QuestAvailability(false, target);
		return new QuestAvailability(true, target);
	}

	public static bool ReachesTarget(double e1RmKg, double r10Kg, int targetRank) =>
		e1RmKg >= ExerciseRankCalculator.RankThreshold(r10Kg, targetRank);

	/// <summary>
	/// R5+ : kg et reps de l’objectif affiché. R1–R4 : e1RM comme avant.
	/// </summary>
	public static bool CompletesDisplayedObjective(
		int targetRank,
		QuestSetSuggestion primary,
		double performedKg,
		int performedReps,
		double r10Kg)
	{
		if (targetRank >= ExactObjectiveFromRank)
			return performedKg + 1e-9 >= primary.WeightKg && performedReps >= primary.Reps;

		return ReachesTarget(E1Rm.From(performedKg, performedReps), r10Kg, targetRank);
	}

	public static DateTime AsUtc(DateTime value) =>
		value.Kind == DateTimeKind.Local
			? value.ToUniversalTime()
			: DateTime.SpecifyKind(value, DateTimeKind.Utc);

	public static DateTime DeadlineUtc(DateTime startedUtc, DateTime expiresUtc)
	{
		var start = AsUtc(startedUtc);
		var intended = start + TimerDuration;
		var stored = AsUtc(expiresUtc);
		return stored <= intended ? stored : intended;
	}

	public static TimeSpan Remaining(DateTime startedUtc, DateTime expiresUtc, DateTime utcNow)
	{
		var left = DeadlineUtc(startedUtc, expiresUtc) - AsUtc(utcNow);
		if (left <= TimeSpan.Zero)
			return TimeSpan.Zero;
		return left > TimerDuration ? TimerDuration : left;
	}

	public static string FormatRemaining(TimeSpan remaining)
	{
		if (remaining < TimeSpan.Zero)
			remaining = TimeSpan.Zero;
		if (remaining > TimerDuration)
			remaining = TimerDuration;
		return $"{(int)remaining.TotalMinutes:00}:{remaining.Seconds:00}";
	}

	public static int ApplySuccess(int validatedRank) =>
		Math.Min(ExerciseRankCalculator.MaxRank, validatedRank + 1);

	public static int ApplyFailure(int validatedRank, bool hasSuccessfulValidation, int? calibrationFloor)
	{
		if (hasSuccessfulValidation)
		{
			var floor = calibrationFloor ?? validatedRank;
			return Math.Max(floor, validatedRank);
		}

		return Math.Max(ExerciseRankCalculator.MinRank, validatedRank - 1);
	}

	public static QuestStartCheck CanStart(
		bool questMarkedAvailable,
		bool exerciseIsClassifying,
		bool exerciseIsClassified,
		bool standardIsActive,
		int? theoreticalRank,
		int? validatedRank,
		int expectedTargetRank,
		bool alreadyAttemptedToday)
	{
		if (!questMarkedAvailable)
			return Denied("quête indisponible");
		if (!exerciseIsClassifying)
			return Denied("exercice non classifiant");
		if (!exerciseIsClassified)
			return Denied("exercice non classé");
		if (!standardIsActive)
			return Denied("standard inactif");
		if (alreadyAttemptedToday)
			return Denied("tentative déjà effectuée aujourd'hui");

		var availability = Availability(theoreticalRank, validatedRank);
		if (!availability.IsAvailable || availability.TargetRank != expectedTargetRank)
			return Denied("conditions de rang non réunies");

		return new QuestStartCheck(true, null);
	}

	public static bool IsExpired(DateTime startedUtc, DateTime expiresUtc, DateTime utcNow) =>
		Remaining(startedUtc, expiresUtc, utcNow) <= TimeSpan.Zero;

	public static bool IsExpired(DateTime expiresUtc, DateTime utcNow) =>
		AsUtc(utcNow) >= AsUtc(expiresUtc);

	private static QuestStartCheck Denied(string reason) => new(false, reason);
}
