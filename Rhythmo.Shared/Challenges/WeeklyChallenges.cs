using System.Text.Json;
using System.Text.Json.Serialization;
using Rhythmo.Shared.Cardio;
using Rhythmo.Shared.History;
using Rhythmo.Shared.Ranking;

namespace Rhythmo.Shared.Challenges;

public enum ChallengeFamily
{
	Strength,
	SessionVolume,
	WeekVolume,
	ExerciseVariety,
	GroupVariety,
	TargetedMuscle,
	Progression,
	RunningDistance,
	CardioPace
}

public enum ChallengeStatus
{
	Offered,
	Active,
	Succeeded,
	Expired
}

public sealed record FrozenTarget(
	ChallengeFamily Family,
	Guid? ExerciseId,
	string? GroupId,
	double? Number,
	int? Count);

public sealed record LoggedSet(Guid ExerciseId, int Reps, double WeightKg);

public sealed record LoggedWorkout(Guid Id, DateTime CompletedUtc, IReadOnlyList<LoggedSet> Sets);

public sealed record LoggedCardio(DateTime PerformedUtc, double DistanceM, int DurationS);

public sealed record ChallengeContext
{
	public required DateTime NowUtc { get; init; }
	public required TimeZoneInfo Zone { get; init; }
	public required IReadOnlyList<LoggedWorkout> Workouts { get; init; }
	public required IReadOnlyList<LoggedCardio> Cardio { get; init; }
	public required IReadOnlyList<(string GroupId, int? ValidatedRank)> Groups { get; init; }
	public required IReadOnlyList<(Guid ExerciseId, double MaxKg, DateTime UpdatedUtc)> Bests { get; init; }
	public required Func<Guid, string?> PrimaryGroup { get; init; }
	public required Func<Guid, string, bool> IsPrimaryFor { get; init; }
}

public readonly record struct IsoWeekStamp(int Year, int Week, DateOnly Monday)
{
	public IsoWeekStamp AddWeeks(int count) => IsoWeek.Of(Monday.AddDays(7 * count));

	public DateTime StartUtc(TimeZoneInfo zone) => IsoWeek.ToUtc(Monday, zone);

	public DateTime EndUtc(TimeZoneInfo zone) => IsoWeek.ToUtc(Monday.AddDays(7), zone);
}

public static class IsoWeek
{
	public static IsoWeekStamp Of(DateOnly date)
	{
		var monday = date.AddDays(-((int)date.DayOfWeek + 6) % 7);
		var thursday = monday.AddDays(3);
		var week1Monday = new DateOnly(thursday.Year, 1, 4);
		week1Monday = week1Monday.AddDays(-((int)week1Monday.DayOfWeek + 6) % 7);
		var week = (monday.DayNumber - week1Monday.DayNumber) / 7 + 1;
		return new IsoWeekStamp(thursday.Year, week, monday);
	}

	public static DateTime ToUtc(DateOnly day, TimeZoneInfo zone)
	{
		var local = DateTime.SpecifyKind(day.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified);
		return TimeZoneInfo.ConvertTimeToUtc(local, zone);
	}
}

public static class WeeklyChallengeCatalog
{
	public static readonly IReadOnlyList<(int Slot, ChallengeFamily Family)> Slots =
	[
		(1, ChallengeFamily.Strength),
		(2, ChallengeFamily.Strength),
		(3, ChallengeFamily.SessionVolume),
		(4, ChallengeFamily.WeekVolume),
		(5, ChallengeFamily.ExerciseVariety),
		(6, ChallengeFamily.GroupVariety),
		(7, ChallengeFamily.TargetedMuscle),
		(8, ChallengeFamily.Progression),
		(9, ChallengeFamily.RunningDistance),
		(10, ChallengeFamily.CardioPace)
	];

	public static IReadOnlyList<ChallengeFamily> TypesFor(Guid profileId, int isoYear, int isoWeek) =>
		Slots.Select(s => s.Family).ToList();

	public static string TemplateId(int slot) => slot switch
	{
		1 => "strength-1",
		2 => "strength-2",
		3 => "session-volume",
		4 => "week-volume",
		5 => "variety-exercises",
		6 => "variety-groups",
		7 => "targeted-muscle",
		8 => "progression",
		9 => "running",
		10 => "pace",
		_ => throw new ArgumentOutOfRangeException(nameof(slot))
	};

	public static ChallengeFamily FamilyOf(string templateId) => templateId switch
	{
		"strength-1" or "strength-2" => ChallengeFamily.Strength,
		"session-volume" => ChallengeFamily.SessionVolume,
		"week-volume" => ChallengeFamily.WeekVolume,
		"variety-exercises" => ChallengeFamily.ExerciseVariety,
		"variety-groups" => ChallengeFamily.GroupVariety,
		"targeted-muscle" => ChallengeFamily.TargetedMuscle,
		"progression" => ChallengeFamily.Progression,
		"running" => ChallengeFamily.RunningDistance,
		"pace" => ChallengeFamily.CardioPace,
		_ => throw new ArgumentOutOfRangeException(nameof(templateId))
	};

	public static int StrengthIndex(int slot) => slot == 2 ? 1 : 0;

	public static string StatusName(ChallengeStatus status) => status switch
	{
		ChallengeStatus.Offered => "offered",
		ChallengeStatus.Active => "active",
		ChallengeStatus.Succeeded => "succeeded",
		ChallengeStatus.Expired => "expired",
		_ => "offered"
	};

	public static ChallengeStatus ParseStatus(string status) => status switch
	{
		"active" => ChallengeStatus.Active,
		"succeeded" => ChallengeStatus.Succeeded,
		"expired" => ChallengeStatus.Expired,
		_ => ChallengeStatus.Offered
	};
}

public static class FrozenTargetJson
{
	private static readonly JsonSerializerOptions Options = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
	};

	public static string Write(FrozenTarget target) => JsonSerializer.Serialize(target, Options);

	public static FrozenTarget Read(string json) =>
		JsonSerializer.Deserialize<FrozenTarget>(json, Options)
		?? throw new InvalidOperationException("Cible illisible.");
}

public static class ChallengeTargets
{
	public static FrozenTarget? TryBuild(
		ChallengeFamily family,
		int strengthIndex,
		DateTime startedUtc,
		ChallengeContext ctx)
	{
		var before = startedUtc;
		var from = startedUtc.AddDays(-28);
		return family switch
		{
			ChallengeFamily.Strength => Strength(strengthIndex, from, before, ctx),
			ChallengeFamily.SessionVolume => SessionVolume(before, ctx),
			ChallengeFamily.WeekVolume => WeekVolume(before, ctx),
			ChallengeFamily.ExerciseVariety => Variety(from, before, ctx, groups: false),
			ChallengeFamily.GroupVariety => Variety(from, before, ctx, groups: true),
			ChallengeFamily.TargetedMuscle => Muscle(from, before, ctx),
			ChallengeFamily.Progression => Progression(ctx),
			ChallengeFamily.RunningDistance => new FrozenTarget(family, null, null, 5000, null),
			ChallengeFamily.CardioPace => Pace(from, before, ctx),
			_ => null
		};
	}

	private static FrozenTarget? Strength(int index, DateTime from, DateTime before, ChallengeContext ctx)
	{
		var chosen = RecentExercises(from, before, ctx);
		if (index < 0 || index >= chosen.Count)
			return null;
		var id = chosen[index];
		var sets = SetsBetween(from, before, ctx).Where(s => s.Set.ExerciseId == id).Select(s => s.Set).ToList();
		if (sets.Count == 0)
			return null;
		var held = sets.Where(s => s.Reps >= 6).Select(s => s.WeightKg).DefaultIfEmpty().Max();
		var kg = held > 0
			? RoundStep(held, 2.5)
			: RoundStep(sets.Max(s => s.WeightKg) * 0.8, 2.5);
		if (kg <= 0)
			return null;
		return new FrozenTarget(ChallengeFamily.Strength, id, null, kg, 3);
	}

	public static IReadOnlyList<Guid> RecentExercises(DateTime from, DateTime before, ChallengeContext ctx)
	{
		return SetsBetween(from, before, ctx)
			.GroupBy(s => s.Set.ExerciseId)
			.Select(g => (Id: g.Key, Volume: g.Sum(s => s.Set.Reps * s.Set.WeightKg)))
			.OrderByDescending(x => x.Volume)
			.ThenBy(x => x.Id)
			.Select(x => x.Id)
			.ToList();
	}

	private static FrozenTarget? SessionVolume(DateTime before, ChallengeContext ctx)
	{
		var volumes = ctx.Workouts
			.Where(w => Utc(w.CompletedUtc) < before)
			.OrderByDescending(w => Utc(w.CompletedUtc))
			.Take(8)
			.Select(VolumeOf)
			.ToList();
		if (volumes.Count < 3)
			return null;
		var target = (int)Math.Round(Median(volumes) * 1.05, MidpointRounding.AwayFromZero);
		return new FrozenTarget(ChallengeFamily.SessionVolume, null, null, target, null);
	}

	private static FrozenTarget? WeekVolume(DateTime before, ChallengeContext ctx)
	{
		var local = TrainingHistory.LocalDate(before, ctx.Zone);
		var week = IsoWeek.Of(local);
		var sums = Enumerable.Range(1, 4)
			.Select(i =>
			{
				var previous = week.AddWeeks(-i);
				var start = previous.StartUtc(ctx.Zone);
				var end = previous.EndUtc(ctx.Zone);
				return ctx.Workouts
					.Where(w => Utc(w.CompletedUtc) >= start && Utc(w.CompletedUtc) < end)
					.Sum(VolumeOf);
			})
			.ToList();
		if (sums.All(s => s <= 0))
			return null;
		var target = (int)Math.Round(sums.Average() * 1.05, MidpointRounding.AwayFromZero);
		return new FrozenTarget(ChallengeFamily.WeekVolume, null, null, target, null);
	}

	private static FrozenTarget? Variety(DateTime from, DateTime before, ChallengeContext ctx, bool groups)
	{
		var ids = SetsBetween(from, before, ctx).Select(s => s.Set.ExerciseId).Distinct().ToList();
		if (!groups)
		{
			if (ids.Count == 0)
				return null;
			return new FrozenTarget(ChallengeFamily.ExerciseVariety, null, null, null, Math.Min(ids.Count + 1, 12));
		}

		var groupCount = ids.Select(ctx.PrimaryGroup).Where(g => g is not null).Distinct().Count();
		if (groupCount == 0)
			return null;
		return new FrozenTarget(ChallengeFamily.GroupVariety, null, null, null, Math.Min(groupCount + 1, 6));
	}

	private static FrozenTarget? Muscle(DateTime from, DateTime before, ChallengeContext ctx)
	{
		var ranked = ctx.Groups
			.Where(g => g.ValidatedRank is not null)
			.OrderBy(g => g.ValidatedRank)
			.ThenBy(g => GroupSort(g.GroupId))
			.ToList();
		if (ranked.Count == 0)
			return null;
		var groupId = ranked[0].GroupId;
		var sets = SetsBetween(from, before, ctx).Count(s => ctx.IsPrimaryFor(s.Set.ExerciseId, groupId));
		if (sets == 0)
			return null;
		return new FrozenTarget(ChallengeFamily.TargetedMuscle, null, groupId, null, sets / 4 + 2);
	}

	private static FrozenTarget? Progression(ChallengeContext ctx)
	{
		var best = ctx.Bests
			.OrderByDescending(b => Utc(b.UpdatedUtc))
			.ThenBy(b => b.ExerciseId)
			.FirstOrDefault();
		if (best.ExerciseId == Guid.Empty && ctx.Bests.Count == 0)
			return null;
		if (ctx.Bests.Count == 0)
			return null;
		return new FrozenTarget(ChallengeFamily.Progression, best.ExerciseId, null, best.MaxKg + 0.01, null);
	}

	private static FrozenTarget? Pace(DateTime from, DateTime before, ChallengeContext ctx)
	{
		var paces = ctx.Cardio
			.Where(c => Utc(c.PerformedUtc) >= from && Utc(c.PerformedUtc) < before)
			.Select(c => CardioMath.PaceSecondsPerKm(c.DistanceM, c.DurationS))
			.Where(p => p is not null)
			.Select(p => p!.Value)
			.ToList();
		if (paces.Count == 0)
			return null;
		return new FrozenTarget(ChallengeFamily.CardioPace, null, null, Median(paces), null);
	}

	private static List<(LoggedWorkout Workout, LoggedSet Set)> SetsBetween(
		DateTime from, DateTime before, ChallengeContext ctx) =>
		ctx.Workouts
			.Where(w => Utc(w.CompletedUtc) >= from && Utc(w.CompletedUtc) < before)
			.SelectMany(w => w.Sets.Select(s => (w, s)))
			.ToList();

	private static int GroupSort(string groupId)
	{
		foreach (var group in MuscleIds.Groups)
		{
			if (group.Id == groupId)
				return group.Sort;
		}
		return 99;
	}

	internal static double VolumeOf(LoggedWorkout workout) =>
		WorkoutVolume.Sum(workout.Sets.Select(s => (s.Reps, s.WeightKg)));

	internal static double Median(IReadOnlyList<double> values)
	{
		var sorted = values.OrderBy(v => v).ToList();
		var n = sorted.Count;
		if (n == 0)
			return 0;
		if (n % 2 == 1)
			return sorted[n / 2];
		return (sorted[n / 2 - 1] + sorted[n / 2]) / 2d;
	}

	internal static double RoundStep(double kg, double step) =>
		Math.Round(kg / step, MidpointRounding.AwayFromZero) * step;

	internal static DateTime Utc(DateTime value) => value.Kind switch
	{
		DateTimeKind.Utc => value,
		DateTimeKind.Local => value.ToUniversalTime(),
		_ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
	};
}

public sealed record AwardPlan(bool WriteAward, int Points, ChallengeStatus NextStatus, bool AlreadyAwarded);

public static class ChallengeEvaluator
{
	public static AwardPlan Decide(
		ChallengeFamily family,
		FrozenTarget? target,
		ChallengeStatus status,
		int? existingPoints,
		DateTime? startedUtc,
		DateTime expiresUtc,
		ChallengeContext ctx)
	{
		var now = ChallengeTargets.Utc(ctx.NowUtc);
		var expires = ChallengeTargets.Utc(expiresUtc);
		var awarded = existingPoints ?? 0;

		if (now >= expires)
		{
			return new AwardPlan(
				false,
				awarded,
				awarded > 0 ? ChallengeStatus.Succeeded : ChallengeStatus.Expired,
				false);
		}

		if (family != ChallengeFamily.RunningDistance && awarded > 0)
		{
			return new AwardPlan(false, awarded, ChallengeStatus.Succeeded, true);
		}

		if (status is ChallengeStatus.Offered or ChallengeStatus.Expired || startedUtc is null || target is null)
		{
			return new AwardPlan(false, awarded, status, false);
		}

		var started = ChallengeTargets.Utc(startedUtc.Value);
		var progress = Measure(family, target, started, expires, ctx);
		if (family == ChallengeFamily.RunningDistance)
		{
			var write = progress.Points != awarded && (progress.Points > 0 || awarded > 0);
			return new AwardPlan(write, progress.Points, ChallengeStatus.Active, false);
		}

		if (!progress.Met)
			return new AwardPlan(false, 0, ChallengeStatus.Active, false);

		if (awarded > 0)
			return new AwardPlan(false, awarded, ChallengeStatus.Succeeded, true);

		return new AwardPlan(true, 10, ChallengeStatus.Succeeded, false);
	}

	public static (bool Met, int Points, double DistanceM) Measure(
		ChallengeFamily family,
		FrozenTarget target,
		DateTime startedUtc,
		DateTime expiresUtc,
		ChallengeContext ctx)
	{
		var started = ChallengeTargets.Utc(startedUtc);
		var expires = ChallengeTargets.Utc(expiresUtc);
		var workouts = ctx.Workouts
			.Where(w => In(ChallengeTargets.Utc(w.CompletedUtc), started, expires))
			.ToList();
		var cardio = ctx.Cardio
			.Where(c => In(ChallengeTargets.Utc(c.PerformedUtc), started, expires))
			.ToList();

		switch (family)
		{
			case ChallengeFamily.Strength:
				var met = workouts.Any(w => w.Sets.Count(s =>
					s.ExerciseId == target.ExerciseId &&
					s.Reps is >= 6 and <= 12 &&
					s.WeightKg >= (target.Number ?? 0)) >= 3);
				return (met, met ? 10 : 0, 0);
			case ChallengeFamily.SessionVolume:
				var sessionOk = workouts.Any(w => ChallengeTargets.VolumeOf(w) >= (target.Number ?? 0));
				return (sessionOk, sessionOk ? 10 : 0, 0);
			case ChallengeFamily.WeekVolume:
				var sum = workouts.Sum(ChallengeTargets.VolumeOf);
				var weekOk = sum >= (target.Number ?? 0);
				return (weekOk, weekOk ? 10 : 0, 0);
			case ChallengeFamily.ExerciseVariety:
				var exercises = workouts.SelectMany(w => w.Sets).Select(s => s.ExerciseId).Distinct().Count();
				var exOk = exercises >= (target.Count ?? int.MaxValue);
				return (exOk, exOk ? 10 : 0, 0);
			case ChallengeFamily.GroupVariety:
				var groups = workouts.SelectMany(w => w.Sets)
					.Select(s => ctx.PrimaryGroup(s.ExerciseId))
					.Where(g => g is not null)
					.Distinct()
					.Count();
				var groupOk = groups >= (target.Count ?? int.MaxValue);
				return (groupOk, groupOk ? 10 : 0, 0);
			case ChallengeFamily.TargetedMuscle:
				var primarySets = workouts.SelectMany(w => w.Sets)
					.Count(s => target.GroupId is not null && ctx.IsPrimaryFor(s.ExerciseId, target.GroupId));
				var muscleOk = primarySets >= (target.Count ?? int.MaxValue);
				return (muscleOk, muscleOk ? 10 : 0, 0);
			case ChallengeFamily.Progression:
				var beat = workouts.SelectMany(w => w.Sets).Any(s =>
					s.ExerciseId == target.ExerciseId && s.WeightKg > (target.Number ?? double.MaxValue));
				return (beat, beat ? 10 : 0, 0);
			case ChallengeFamily.RunningDistance:
				var distance = cardio.Sum(c => c.DistanceM);
				return (CardioMath.RunningPoints(distance) >= 10, CardioMath.RunningPoints(distance), distance);
			case ChallengeFamily.CardioPace:
				var paceOk = cardio.Any(c =>
				{
					var pace = CardioMath.PaceSecondsPerKm(c.DistanceM, c.DurationS);
					return c.DistanceM >= 1000 && pace is not null && pace <= (target.Number ?? 0);
				});
				return (paceOk, paceOk ? 10 : 0, 0);
			default:
				return (false, 0, 0);
		}
	}

	private static bool In(DateTime instant, DateTime started, DateTime expires) =>
		instant >= started && instant < expires;
}

public static class ChallengeText
{
	public static string Title(ChallengeFamily family) => family switch
	{
		ChallengeFamily.Strength => "Force contrôlée",
		ChallengeFamily.SessionVolume => "Volume d’une séance",
		ChallengeFamily.WeekVolume => "Volume de la semaine",
		ChallengeFamily.ExerciseVariety => "Variété d’exercices",
		ChallengeFamily.GroupVariety => "Variété de groupes",
		ChallengeFamily.TargetedMuscle => "Muscle ciblé",
		ChallengeFamily.Progression => "Progression",
		ChallengeFamily.RunningDistance => "Course",
		ChallengeFamily.CardioPace => "Allure",
		_ => "Défi"
	};

	public static string LockedReason(ChallengeFamily family) => family switch
	{
		ChallengeFamily.RunningDistance => "",
		_ => "Pas assez d’historique pour lancer ce défi."
	};
}
