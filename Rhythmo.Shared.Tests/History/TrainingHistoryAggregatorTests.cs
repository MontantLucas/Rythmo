using Rhythmo.Shared.Cardio;
using Rhythmo.Shared.Challenges;
using Rhythmo.Shared.History;
using Rhythmo.Shared.Ranking;
using Xunit;

namespace Rhythmo.Shared.Tests.History;

public class TrainingPeriodTests
{
	private static readonly DateOnly Today = new(2026, 10, 28);

	[Fact]
	public void Seven_days_include_today()
	{
		var window = TrainingPeriodCalendar.Current(TrainingPeriodKind.Rolling7Days, Today);
		Assert.Equal(new DateOnly(2026, 10, 22), window.Start);
		Assert.Equal(Today, window.End);
		Assert.Equal("7 j", window.ChipLabel);
		Assert.Equal("22–28 oct. 2026", window.RangeLabel);
		Assert.False(window.CanGoNext);
	}

	[Fact]
	public void Month_chip_is_the_month_name_through_today()
	{
		var window = TrainingPeriodCalendar.Current(TrainingPeriodKind.CalendarMonth, Today);
		Assert.Equal(new DateOnly(2026, 10, 1), window.Start);
		Assert.Equal(Today, window.End);
		Assert.Equal("Oct.", window.ChipLabel);
		Assert.False(window.CanGoNext);
	}

	[Fact]
	public void Three_months_on_28_oct_starts_1_august()
	{
		var window = TrainingPeriodCalendar.Current(TrainingPeriodKind.ThreeMonths, Today);
		Assert.Equal(new DateOnly(2026, 8, 1), window.Start);
		Assert.Equal(Today, window.End);
		Assert.Equal("3 mois", window.ChipLabel);
		Assert.Equal("1 août–28 oct. 2026", window.RangeLabel);
	}

	[Fact]
	public void Year_chip_is_the_civil_year()
	{
		var window = TrainingPeriodCalendar.Current(TrainingPeriodKind.CalendarYear, Today);
		Assert.Equal(new DateOnly(2026, 1, 1), window.Start);
		Assert.Equal(Today, window.End);
		Assert.Equal("2026", window.ChipLabel);
	}

	[Fact]
	public void Short_month_does_not_invent_a_day()
	{
		var march = TrainingPeriodCalendar.Current(TrainingPeriodKind.CalendarMonth, new DateOnly(2026, 3, 31));
		var previous = TrainingPeriodCalendar.PreviousAnchor(march);
		var february = TrainingPeriodCalendar.Window(TrainingPeriodKind.CalendarMonth, previous, new DateOnly(2026, 3, 31));
		Assert.Equal(new DateOnly(2026, 2, 1), february.Start);
		Assert.Equal(new DateOnly(2026, 2, 28), february.End);
		Assert.True(february.CanGoNext);
	}

	[Fact]
	public void Next_stops_on_the_window_that_contains_today()
	{
		var current = TrainingPeriodCalendar.Current(TrainingPeriodKind.Rolling7Days, Today);
		var back = TrainingPeriodCalendar.Window(
			TrainingPeriodKind.Rolling7Days,
			TrainingPeriodCalendar.PreviousAnchor(current),
			Today);
		Assert.True(back.CanGoNext);
		var forward = TrainingPeriodCalendar.NextAnchor(back, Today);
		var restored = TrainingPeriodCalendar.Window(TrainingPeriodKind.Rolling7Days, forward!.Value, Today);
		Assert.Equal(current, restored);
		Assert.Null(TrainingPeriodCalendar.NextAnchor(restored, Today));
	}
}

public class TrainingHistoryAggregatorTests
{
	private static readonly TimeZoneInfo Zone = TimeZoneInfo.Utc;
	private static readonly DateOnly Today = new(2026, 10, 28);
	private static readonly Guid Bench = Guid.Parse("11111111-1111-1111-1111-111111111111");
	private static readonly Guid Squat = Guid.Parse("22222222-2222-2222-2222-222222222222");
	private static readonly Guid Unknown = Guid.Parse("33333333-3333-3333-3333-333333333333");

	[Fact]
	public void Volume_matches_reps_times_kg()
	{
		var sets = new (int, double)[] { (10, 20), (8, 20) };
		Assert.Equal(360, WorkoutVolume.Sum(sets));
	}

	[Fact]
	public void Repeated_exercise_counts_once_and_blocks_add_up()
	{
		var window = TrainingPeriodCalendar.Current(TrainingPeriodKind.Rolling7Days, Today);
		var workouts = new[]
		{
			Workout(new DateTime(2026, 10, 28, 10, 0, 0, DateTimeKind.Utc), Bench, (8, 80d), (6, 80d)),
			Workout(new DateTime(2026, 10, 27, 10, 0, 0, DateTimeKind.Utc), Bench, (10, 70d))
		};
		workouts[0] = workouts[0] with
		{
			Blocks =
			[
				workouts[0].Blocks[0],
				new HistoryExerciseBlock(Bench, [new HistorySet(5, 60)])
			]
		};

		var agg = TrainingHistory.Build(workouts, window, Zone, Resolve, []);
		Assert.Equal(2, agg.SessionCount);
		Assert.Equal(1, agg.DistinctExerciseCount);
		Assert.Equal(8 * 80 + 6 * 80 + 5 * 60 + 10 * 70, agg.VolumeKg);
	}

	[Fact]
	public void Unknown_exercise_is_kept_and_empty_session_counts()
	{
		var window = TrainingPeriodCalendar.Current(TrainingPeriodKind.CalendarMonth, Today);
		var workouts = new[]
		{
			new HistoryWorkout(Guid.NewGuid(), new DateTime(2026, 10, 2, 8, 0, 0, DateTimeKind.Utc), "Vide", []),
			new HistoryWorkout(
				Guid.NewGuid(),
				new DateTime(2026, 10, 3, 8, 0, 0, DateTimeKind.Utc),
				"Inconnu",
				[new HistoryExerciseBlock(Unknown, [new HistorySet(10, 10)])])
		};
		var agg = TrainingHistory.Build(workouts, window, Zone, Resolve, []);
		Assert.Equal(2, agg.SessionCount);
		Assert.Equal(1, agg.DistinctExerciseCount);
		Assert.Equal(100, agg.VolumeKg);
		Assert.Equal(TrainingHistory.UnclassifiedName, agg.ExerciseGroups[0].Name);
		Assert.Equal(TrainingHistory.UnknownExerciseName, agg.ExerciseGroups[0].Exercises[0].Name);
		Assert.Contains(agg.Sessions, s => !s.HasSets);
	}

	[Fact]
	public void Quest_pr_without_workout_does_not_badge()
	{
		var window = TrainingPeriodCalendar.Current(TrainingPeriodKind.Rolling7Days, Today);
		var id = Guid.NewGuid();
		var workouts = new[]
		{
			new HistoryWorkout(id, new DateTime(2026, 10, 28, 12, 0, 0, DateTimeKind.Utc), "S",
				[new HistoryExerciseBlock(Bench, [new HistorySet(5, 100)])])
		};
		var prs = new[]
		{
			new HistoryPr(Bench, id, "weight"),
			new HistoryPr(Bench, null, "reps")
		};
		var agg = TrainingHistory.Build(workouts, window, Zone, Resolve, prs);
		var day = agg.ExerciseGroups.Single().Exercises.Single().Days.Single();
		Assert.Equal(["weight"], day.PrKinds);
	}

	[Fact]
	public void Outside_window_and_daily_max_are_ignored()
	{
		var window = TrainingPeriodCalendar.Current(TrainingPeriodKind.Rolling7Days, Today);
		var old = Workout(new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc), Bench, (10, 100d));
		var inside = Workout(new DateTime(2026, 10, 28, 10, 0, 0, DateTimeKind.Utc), Bench, (1, 10d));
		var agg = TrainingHistory.Build([old, inside], window, Zone, Resolve, []);
		Assert.Equal(1, agg.SessionCount);
		Assert.Equal(10, agg.VolumeKg);
	}

	private static HistoryWorkout Workout(DateTime utc, Guid exercise, params (int Reps, double Kg)[] sets) =>
		new(Guid.NewGuid(), utc, "Séance",
			[new HistoryExerciseBlock(exercise, sets.Select(s => new HistorySet(s.Reps, s.Kg)).ToList())]);

	private static HistoryExerciseInfo? Resolve(Guid id)
	{
		if (id == Bench)
			return new HistoryExerciseInfo("Développé couché barre", MuscleIds.Chest);
		if (id == Squat)
			return new HistoryExerciseInfo("Squat", MuscleIds.Legs);
		return null;
	}
}

public class ExerciseArtTests
{
	[Fact]
	public void Classifying_exercise_has_its_own_file() =>
		Assert.Equal("ex_developpe_couche", ExerciseArt.Resolve("Pectoraux", "Développé couché barre"));

	[Fact]
	public void Secondary_only_falls_back_to_the_group() =>
		Assert.Equal("muscle_chest", ExerciseArt.Resolve("Pectoraux", "Pompes"));

	[Fact]
	public void Duplicate_name_uses_the_catalog_group() =>
		Assert.Equal(
			ExerciseArt.Resolve("Dos", "Tractions"),
			ExerciseArt.Resolve("Poids du corps / Street Workout", "Tractions"));

	[Fact]
	public void Landmine_and_unknown_use_the_generic_file()
	{
		Assert.Equal(ExerciseArt.Generic, ExerciseArt.Resolve("Dos", "Landmine 180"));
		Assert.Equal(ExerciseArt.Generic, ExerciseArt.Resolve("Autre", "Inexistant"));
	}

	[Fact]
	public void Every_classifying_exercise_has_a_file()
	{
		var classifying = MuscleRankingCatalog.All.Where(x => x.IsClassifying).ToList();
		Assert.Equal(52, classifying.Count);
		Assert.Equal(52, classifying.Select(x => ExerciseArt.Resolve(x.Category, x.NameFr)).Distinct().Count());
	}
}

public class MuscleCompareModelTests
{
	[Fact]
	public void Null_rank_stays_absent_and_count_is_over_eighteen()
	{
		var side = MuscleCompareModel.Build(
			[new CompareSnapshot(MuscleIds.Chest, 4), new CompareSnapshot(MuscleIds.Back, null)],
			[new CompareSnapshot(MuscleIds.MidChest, 3), new CompareSnapshot(MuscleIds.Lats, null)]);
		Assert.Equal(1, side.EvaluatedCount);
		Assert.Equal(MuscleIds.Muscles.Count, side.MuscleCount);
		Assert.Equal(4, side.GroupRanks[MuscleIds.Chest]);
		Assert.Null(side.GroupRanks[MuscleIds.Back]);
		Assert.Null(side.GroupRanks[MuscleIds.Legs]);
		Assert.Null(side.MuscleRanks[MuscleIds.Quads]);
		Assert.Null(side.MuscleRanks[MuscleIds.Lats]);
		Assert.DoesNotContain(side.GroupRanks.Values, v => v == 1 && false);
		Assert.All(side.GroupRanks.Values.Where(v => v is null), v => Assert.Null(v));
	}
}

public class CardioMathTests
{
	[Fact]
	public void Pace_is_seconds_per_kilometre()
	{
		var pace = CardioMath.PaceSecondsPerKm(5000, 1800);
		Assert.Equal(360, pace);
	}

	[Theory]
	[InlineData(0, 60)]
	[InlineData(1000, 0)]
	public void Non_positive_values_are_rejected(double meters, int seconds)
	{
		Assert.False(CardioMath.IsPositive(meters, seconds));
		Assert.Null(CardioMath.PaceSecondsPerKm(meters, seconds));
	}

	[Fact]
	public void Running_points_are_proportional_and_capped()
	{
		Assert.Equal(5, CardioMath.RunningPoints(2500));
		Assert.Equal(10, CardioMath.RunningPoints(5000));
		Assert.Equal(10, CardioMath.RunningPoints(8000));
	}
}

public class WeeklyChallengeTests
{
	private static readonly TimeZoneInfo Zone = TimeZoneInfo.Utc;
	private static readonly Guid Bench = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
	private static readonly Guid Row = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
	private static readonly DateTime Start = new(2026, 10, 28, 8, 0, 0, DateTimeKind.Utc);

	[Fact]
	public void Same_week_always_has_the_same_ten_types()
	{
		var profile = Guid.NewGuid();
		var a = WeeklyChallengeCatalog.TypesFor(profile, 2026, 44);
		var b = WeeklyChallengeCatalog.TypesFor(profile, 2026, 44);
		Assert.Equal(a, b);
		Assert.Equal(2, a.Count(f => f == ChallengeFamily.Strength));
		Assert.Equal(2, a.Count(f => f is ChallengeFamily.SessionVolume or ChallengeFamily.WeekVolume));
		Assert.Equal(2, a.Count(f => f is ChallengeFamily.ExerciseVariety or ChallengeFamily.GroupVariety));
		Assert.Equal(1, a.Count(f => f == ChallengeFamily.TargetedMuscle));
		Assert.Equal(1, a.Count(f => f == ChallengeFamily.Progression));
		Assert.Equal(1, a.Count(f => f == ChallengeFamily.RunningDistance));
		Assert.Equal(1, a.Count(f => f == ChallengeFamily.CardioPace));
	}

	[Fact]
	public void Target_ignores_a_session_logged_after_the_freeze()
	{
		var before = Context([Workout(Start.AddDays(-2), Bench, (8, 60))]);
		var frozen = ChallengeTargets.TryBuild(ChallengeFamily.Strength, 0, Start, before);
		var after = Context(
		[
			Workout(Start.AddDays(-2), Bench, (8, 60)),
			Workout(Start.AddHours(2), Bench, (8, 200))
		]);
		var again = ChallengeTargets.TryBuild(ChallengeFamily.Strength, 0, Start, after);
		Assert.Equal(frozen, again);
		Assert.Equal(60, frozen!.Number);
	}

	[Fact]
	public void Session_before_start_or_after_expiry_does_not_succeed()
	{
		var target = new FrozenTarget(ChallengeFamily.SessionVolume, null, null, 100, null);
		var early = Context([Workout(Start.AddHours(-1), Bench, (10, 50))]);
		var plan = ChallengeEvaluator.Decide(
			ChallengeFamily.SessionVolume, target, ChallengeStatus.Active, null,
			Start, Start.AddDays(4), early with { NowUtc = Start.AddHours(1) });
		Assert.False(plan.WriteAward);

		var late = Context([Workout(Start.AddDays(5), Bench, (10, 50))]);
		var expired = ChallengeEvaluator.Decide(
			ChallengeFamily.SessionVolume, target, ChallengeStatus.Active, null,
			Start, Start.AddDays(4), late with { NowUtc = Start.AddDays(5) });
		Assert.Equal(ChallengeStatus.Expired, expired.NextStatus);
		Assert.False(expired.WriteAward);
	}

	[Fact]
	public void Second_credit_is_already_awarded()
	{
		var target = new FrozenTarget(ChallengeFamily.Strength, Bench, null, 60, 3);
		var ctx = Context([Workout(Start.AddHours(1), Bench, (8, 60), (8, 60), (6, 60))]);
		var plan = ChallengeEvaluator.Decide(
			ChallengeFamily.Strength, target, ChallengeStatus.Active, 10,
			Start, Start.AddDays(4), ctx with { NowUtc = Start.AddHours(2) });
		Assert.True(plan.AlreadyAwarded);
		Assert.False(plan.WriteAward);
		Assert.Equal(10, plan.Points);
	}

	[Fact]
	public void Strength_succeeds_only_inside_one_session()
	{
		var target = new FrozenTarget(ChallengeFamily.Strength, Bench, null, 60, 3);
		var good = Context([Workout(Start.AddHours(1), Bench, (8, 60), (8, 60), (6, 60))]);
		Assert.True(Measure(ChallengeFamily.Strength, target, good).Met);

		var light = Context([Workout(Start.AddHours(1), Bench, (8, 50), (8, 50), (6, 50))]);
		Assert.False(Measure(ChallengeFamily.Strength, target, light).Met);

		var split = Context(
		[
			Workout(Start.AddHours(1), Bench, (8, 60), (8, 60)),
			Workout(Start.AddHours(3), Bench, (6, 60))
		]);
		Assert.False(Measure(ChallengeFamily.Strength, target, split).Met);
	}

	[Fact]
	public void Volume_targets_follow_the_examples()
	{
		var sessions = new[]
		{
			Workout(Start.AddDays(-1), Bench, (10, 180)),
			Workout(Start.AddDays(-2), Bench, (10, 200)),
			Workout(Start.AddDays(-3), Bench, (10, 220)),
			Workout(Start.AddDays(-4), Bench, (10, 240))
		};
		var session = ChallengeTargets.TryBuild(ChallengeFamily.SessionVolume, 0, Start, Context(sessions));
		Assert.Equal(2205, session!.Number);

		var weeks = new List<LoggedWorkout>();
		var monday = IsoWeek.Of(DateOnly.FromDateTime(Start)).Monday;
		double[] sums = [10000, 9000, 8000, 7000];
		for (var i = 0; i < 4; i++)
		{
			var day = monday.AddDays(-7 * (i + 1));
			weeks.Add(Workout(day.ToDateTime(new TimeOnly(8, 0), DateTimeKind.Utc), Bench, (10, sums[i] / 10)));
		}
		var week = ChallengeTargets.TryBuild(ChallengeFamily.WeekVolume, 0, Start, Context(weeks));
		Assert.Equal(8925, week!.Number);
	}

	[Fact]
	public void Variety_adds_one_and_does_not_invent_a_floor()
	{
		var ctx = Context([Workout(Start.AddDays(-1), Bench, (8, 40)), Workout(Start.AddDays(-1), Row, (8, 40))]);
		var exercises = ChallengeTargets.TryBuild(ChallengeFamily.ExerciseVariety, 0, Start, ctx);
		var groups = ChallengeTargets.TryBuild(ChallengeFamily.GroupVariety, 0, Start, ctx);
		Assert.Equal(3, exercises!.Count);
		Assert.Equal(3, groups!.Count);
		Assert.Null(ChallengeTargets.TryBuild(ChallengeFamily.ExerciseVariety, 0, Start, Context([])));
	}

	[Fact]
	public void Missing_history_keeps_the_slot_unstartable()
	{
		var empty = Context([]);
		Assert.Null(ChallengeTargets.TryBuild(ChallengeFamily.Strength, 1, Start, empty));
		Assert.Null(ChallengeTargets.TryBuild(ChallengeFamily.TargetedMuscle, 0, Start, empty));
		Assert.Null(ChallengeTargets.TryBuild(ChallengeFamily.Progression, 0, Start, empty with { Bests = [] }));
		Assert.Null(ChallengeTargets.TryBuild(ChallengeFamily.CardioPace, 0, Start, empty));
		Assert.NotNull(ChallengeTargets.TryBuild(ChallengeFamily.RunningDistance, 0, Start, empty));
	}

	[Fact]
	public void Each_family_has_a_hit_and_a_miss()
	{
		var hitWork = Context([Workout(Start.AddHours(1), Bench, (8, 60), (8, 60), (6, 60), (8, 40))]);
		Assert.True(Measure(ChallengeFamily.Strength, new FrozenTarget(ChallengeFamily.Strength, Bench, null, 60, 3), hitWork).Met);
		Assert.False(Measure(ChallengeFamily.Strength, new FrozenTarget(ChallengeFamily.Strength, Bench, null, 80, 3), hitWork).Met);

		Assert.True(Measure(ChallengeFamily.SessionVolume, new FrozenTarget(ChallengeFamily.SessionVolume, null, null, 100, null), hitWork).Met);
		Assert.False(Measure(ChallengeFamily.SessionVolume, new FrozenTarget(ChallengeFamily.SessionVolume, null, null, 5000, null), hitWork).Met);

		Assert.True(Measure(ChallengeFamily.WeekVolume, new FrozenTarget(ChallengeFamily.WeekVolume, null, null, 100, null), hitWork).Met);
		Assert.False(Measure(ChallengeFamily.WeekVolume, new FrozenTarget(ChallengeFamily.WeekVolume, null, null, 5000, null), hitWork).Met);

		Assert.True(Measure(ChallengeFamily.ExerciseVariety, new FrozenTarget(ChallengeFamily.ExerciseVariety, null, null, null, 1), hitWork).Met);
		Assert.False(Measure(ChallengeFamily.ExerciseVariety, new FrozenTarget(ChallengeFamily.ExerciseVariety, null, null, null, 4), hitWork).Met);

		Assert.True(Measure(ChallengeFamily.GroupVariety, new FrozenTarget(ChallengeFamily.GroupVariety, null, null, null, 1), hitWork).Met);
		Assert.False(Measure(ChallengeFamily.GroupVariety, new FrozenTarget(ChallengeFamily.GroupVariety, null, null, null, 3), hitWork).Met);

		Assert.True(Measure(ChallengeFamily.TargetedMuscle, new FrozenTarget(ChallengeFamily.TargetedMuscle, null, MuscleIds.Chest, null, 2), hitWork).Met);
		Assert.False(Measure(ChallengeFamily.TargetedMuscle, new FrozenTarget(ChallengeFamily.TargetedMuscle, null, MuscleIds.Chest, null, 9), hitWork).Met);

		Assert.True(Measure(ChallengeFamily.Progression, new FrozenTarget(ChallengeFamily.Progression, Bench, null, 50.01, null), hitWork).Met);
		Assert.False(Measure(ChallengeFamily.Progression, new FrozenTarget(ChallengeFamily.Progression, Bench, null, 100.01, null), hitWork).Met);

		var run = Context([], [new LoggedCardio(Start.AddHours(1), 2500, 900)]);
		var running = Measure(ChallengeFamily.RunningDistance, new FrozenTarget(ChallengeFamily.RunningDistance, null, null, 5000, null), run);
		Assert.Equal(5, running.Points);
		Assert.False(Measure(ChallengeFamily.CardioPace, new FrozenTarget(ChallengeFamily.CardioPace, null, null, 300, null), run).Met);
		var fast = Context([], [new LoggedCardio(Start.AddHours(1), 2000, 600)]);
		Assert.True(Measure(ChallengeFamily.CardioPace, new FrozenTarget(ChallengeFamily.CardioPace, null, null, 360, null), fast).Met);
	}

	[Fact]
	public void Targeted_muscle_uses_the_lowest_rank_and_recent_sets()
	{
		var ctx = Context(
			[Workout(Start.AddDays(-3), Bench, (8, 40), (8, 40), (8, 40), (8, 40))],
			groups: [(MuscleIds.Shoulders, 2), (MuscleIds.Chest, 6)]);
		var sets = Enumerable.Repeat((8, 40d), 12).ToArray();
		var chestSets = Context(
			[Workout(Start.AddDays(-3), Bench, sets)],
			groups: [(MuscleIds.Chest, 2)]);
		var target = ChallengeTargets.TryBuild(ChallengeFamily.TargetedMuscle, 0, Start, chestSets);
		Assert.Equal(MuscleIds.Chest, target!.GroupId);
		Assert.Equal(5, target.Count);
		Assert.Null(ChallengeTargets.TryBuild(ChallengeFamily.TargetedMuscle, 0, Start, ctx));
	}

	private static (bool Met, int Points, double DistanceM) Measure(
		ChallengeFamily family, FrozenTarget target, ChallengeContext ctx) =>
		ChallengeEvaluator.Measure(family, target, Start, Start.AddDays(5), ctx);

	private static ChallengeContext Context(
		IReadOnlyList<LoggedWorkout> workouts,
		IReadOnlyList<LoggedCardio>? cardio = null,
		IReadOnlyList<(string GroupId, int? ValidatedRank)>? groups = null) =>
		new()
		{
			NowUtc = Start,
			Zone = Zone,
			Workouts = workouts,
			Cardio = cardio ?? [],
			Groups = groups ?? [],
			Bests = [(Bench, 100, Start.AddDays(-10))],
			PrimaryGroup = id => id == Bench ? MuscleIds.Chest : id == Row ? MuscleIds.Back : null,
			IsPrimaryFor = (id, group) => id == Bench && group == MuscleIds.Chest
		};

	private static LoggedWorkout Workout(DateTime utc, Guid exercise, params (int Reps, double Kg)[] sets) =>
		new(Guid.NewGuid(), utc, sets.Select(s => new LoggedSet(exercise, s.Reps, s.Kg)).ToList());
}
