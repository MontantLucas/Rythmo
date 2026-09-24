using Rhythmo.Shared.Ranking;
using Xunit;

namespace Rhythmo.Shared.Tests.Ranking;

public class E1RmTests
{
	[Fact]
	public void Epley_divides_reps_by_thirty()
	{
		Assert.Equal(80d * (1d + 5d / 30d), E1Rm.From(80, 5), 10);
	}

	[Fact]
	public void Dumbbell_load_is_not_doubled()
	{
		var oneDumbbell = E1Rm.From(62, 5);
		var bothDumbbells = E1Rm.From(124, 5);
		Assert.Equal(62d * (1d + 5d / 30d), oneDumbbell, 10);
		Assert.True(oneDumbbell < bothDumbbells / 1.5);
	}

	[Fact]
	public void Added_load_ignores_bodyweight()
	{
		var added = E1Rm.From(20, 5);
		var wronglyWithBw = E1Rm.From(100, 5);
		Assert.True(added < wronglyWithBw / 2);
		Assert.Equal(20d * (1d + 5d / 30d), added, 10);
	}

	[Theory]
	[InlineData(0, 5)]
	[InlineData(80, 0)]
	[InlineData(-1, 5)]
	public void Invalid_sets_are_zero(double kg, int reps) =>
		Assert.Equal(0d, E1Rm.From(kg, reps));
}

public class BodyweightStandardSelectorTests
{
	[Theory]
	[InlineData(80, "male_70")]
	[InlineData(85, "male_100")]
	[InlineData(70, "male_70")]
	[InlineData(100, "male_100")]
	public void Male_nearest_or_upper_on_tie(double kg, string key) =>
		Assert.Equal(key, BodyweightStandardSelector.Select(BiologicalSexKind.Male, kg).Key);

	[Theory]
	[InlineData(74, "female_60")]
	[InlineData(75, "female_90")]
	[InlineData(80, "female_90")]
	[InlineData(60, "female_60")]
	public void Female_nearest_or_upper_on_tie(double kg, string key) =>
		Assert.Equal(key, BodyweightStandardSelector.Select(BiologicalSexKind.Female, kg).Key);

	[Fact]
	public void Does_not_cross_sex() =>
		Assert.Equal("female_60", BodyweightStandardSelector.Select(BiologicalSexKind.Female, 70).Key);
}

public class ExerciseRankCalculatorTests
{
	private const double R10 = 100;

	[Theory]
	[InlineData(5, RankClassification.BelowR1, null)]
	[InlineData(9.9, RankClassification.BelowR1, null)]
	[InlineData(10, RankClassification.Classified, 1)]
	[InlineData(15, RankClassification.Classified, 1)]
	[InlineData(20, RankClassification.Classified, 2)]
	[InlineData(29.9, RankClassification.Classified, 2)]
	[InlineData(30, RankClassification.Classified, 3)]
	public void R1_threshold_examples(double e1Rm, RankClassification expected, int? rank)
	{
		var result = ExerciseRankCalculator.Evaluate(e1Rm, R10);
		Assert.Equal(expected, result.Classification);
		Assert.Equal(rank, result.TheoreticalRank);
	}

	[Fact]
	public void Floor_6_9_is_R6() =>
		Assert.Equal(6, ExerciseRankCalculator.Evaluate(69, R10).TheoreticalRank);

	[Fact]
	public void Floor_7_0_is_R7() =>
		Assert.Equal(7, ExerciseRankCalculator.Evaluate(70, R10).TheoreticalRank);

	[Fact]
	public void Caps_at_R10() =>
		Assert.Equal(10, ExerciseRankCalculator.Evaluate(250, R10).TheoreticalRank);

	[Fact]
	public void No_performance_when_zero() =>
		Assert.Equal(RankClassification.NoPerformance, ExerciseRankCalculator.Evaluate(0, R10).Classification);

	[Fact]
	public void Init_theo_minus_three_per_exercise()
	{
		Assert.Equal(3, ExerciseRankCalculator.InitialValidatedRank(6));
		Assert.Equal(1, ExerciseRankCalculator.InitialValidatedRank(4));
		Assert.Equal(4, ExerciseRankCalculator.InitialValidatedRank(7));
		Assert.Equal(1, ExerciseRankCalculator.InitialValidatedRank(1));
	}
}

public class MuscleRankAggregatorTests
{
	[Fact]
	public void Uses_max_validated_primary_not_average()
	{
		var result = MuscleRankAggregator.Aggregate("biceps",
		[
			(true, 4, 6),
			(true, 6, 7),
			(true, 5, 5)
		]);
		Assert.Equal(6, result.ValidatedRank);
		Assert.Equal(3, result.ClassifiedPrimaryCount);
	}

	[Fact]
	public void Ignores_secondary()
	{
		var result = MuscleRankAggregator.Aggregate("glutes",
		[
			(false, 9, 9),
			(true, 3, 4)
		]);
		Assert.Equal(3, result.ValidatedRank);
	}

	[Fact]
	public void Unclassified_primary_does_not_evaluate()
	{
		var result = MuscleRankAggregator.Aggregate("mid_chest",
		[
			(true, null, null)
		]);
		Assert.Null(result.ValidatedRank);
		Assert.Equal(0, result.ClassifiedPrimaryCount);
	}
}

public class GroupRankAggregatorTests
{
	[Fact]
	public void Ignores_unevaluated_and_renormalizes()
	{
		var result = GroupRankAggregator.Aggregate("legs",
		[
			(7, 0.28),
			(null, 0.26),
			(5, 0.20),
			(null, 0.14),
			(null, 0.12)
		]);
		Assert.Equal(2, result.EvaluatedMuscles);
		Assert.Equal(5, result.TotalMuscles);
		Assert.Equal("2/5 muscles évalués", result.EvaluatedLabel);
		var expected = (7 * 0.28 + 5 * 0.20) / (0.28 + 0.20);
		Assert.Equal(expected, result.ValidatedRaw!.Value, 10);
		Assert.Equal((int)Math.Floor(expected), result.ValidatedRank);
	}

	[Fact]
	public void Empty_group_is_not_R1()
	{
		var result = GroupRankAggregator.Aggregate("legs",
		[
			(null, 0.28),
			(null, 0.12)
		]);
		Assert.Null(result.ValidatedRank);
		Assert.Equal(0, result.EvaluatedMuscles);
	}

	[Fact]
	public void Adductors_without_rank_are_ignored()
	{
		var result = GroupRankAggregator.Aggregate("legs",
		[
			(4, 0.28),
			(null, 0.12)
		]);
		Assert.Equal(4, result.ValidatedRank);
		Assert.Equal(1, result.EvaluatedMuscles);
	}
}

public class QuestRulesTests
{
	[Fact]
	public void Four_reps_can_succeed_a_five_rm_quest_via_e1rm()
	{
		var r10 = 100d;
		var e1Rm = E1Rm.From(40, 4);
		Assert.True(QuestRules.ReachesTarget(e1Rm, r10, 4));
		var option = QuestObjectivePlanner.ForRank(r10, 4, MeasurementType.FiveRm, LoadMode.Total);
		Assert.True(QuestRules.CompletesDisplayedObjective(4, option.Primary, 40, 4, r10));
	}

	[Fact]
	public void R5_plus_requires_target_weight_and_reps_not_e1rm()
	{
		const double r10 = 100d;
		var option = QuestObjectivePlanner.ForRank(r10, 7, MeasurementType.FiveRm, LoadMode.Total);
		var primary = option.Primary;
		Assert.Equal(5, primary.Reps);

		Assert.True(QuestRules.CompletesDisplayedObjective(7, primary, primary.WeightKg, primary.Reps, r10));
		Assert.True(QuestRules.CompletesDisplayedObjective(7, primary, primary.WeightKg, primary.Reps + 1, r10));
		Assert.True(QuestRules.CompletesDisplayedObjective(7, primary, primary.WeightKg + 2.5, primary.Reps, r10));
		Assert.False(QuestRules.CompletesDisplayedObjective(7, primary, primary.WeightKg + 2.5, primary.Reps - 1, r10));
	}

	[Fact]
	public void Timer_never_displays_more_than_five_minutes()
	{
		var start = new DateTime(2026, 9, 18, 10, 0, 0, DateTimeKind.Utc);
		var skewedExpiry = start.AddMinutes(54);
		var now = start.AddSeconds(1);
		var remaining = QuestRules.Remaining(start, skewedExpiry, now);
		Assert.True(remaining <= QuestRules.TimerDuration);
		Assert.Equal("04:59", QuestRules.FormatRemaining(remaining));
		Assert.False(QuestRules.IsExpired(start, skewedExpiry, now));
		Assert.True(QuestRules.IsExpired(start, skewedExpiry, start.AddMinutes(5)));
	}

	[Fact]
	public void Success_increments_exactly_one()
	{
		Assert.Equal(4, QuestRules.ApplySuccess(3));
		Assert.Equal(10, QuestRules.ApplySuccess(10));
	}

	[Fact]
	public void Calibration_drops_each_failure_until_r1_then_floors_after_success()
	{
		var r = 3;
		r = QuestRules.ApplyFailure(r, false, null);
		Assert.Equal(2, r);
		r = QuestRules.ApplyFailure(r, false, null);
		Assert.Equal(1, r);
		r = QuestRules.ApplyFailure(r, false, null);
		Assert.Equal(1, r);

		r = QuestRules.ApplySuccess(3);
		Assert.Equal(4, r);
		r = QuestRules.ApplyFailure(r, true, 4);
		Assert.Equal(4, r);
	}

	[Fact]
	public void No_quest_below_r1()
	{
		var avail = QuestRules.Availability(null, null);
		Assert.False(avail.IsAvailable);
	}

	[Fact]
	public void Unlocks_next_rank_when_theoretical_allows()
	{
		var avail = QuestRules.Availability(7, 4);
		Assert.True(avail.IsAvailable);
		Assert.Equal(5, avail.TargetRank);
	}

	[Fact]
	public void Locks_when_theoretical_is_insufficient()
	{
		var avail = QuestRules.Availability(4, 4);
		Assert.False(avail.IsAvailable);
	}

	[Fact]
	public void TryStart_rejects_already_attempted_today()
	{
		var check = QuestRules.CanStart(true, true, true, true, 7, 4, 5, alreadyAttemptedToday: true);
		Assert.False(check.CanStart);
	}

	[Fact]
	public void TryStart_rejects_below_r1()
	{
		var check = QuestRules.CanStart(true, true, false, true, null, null, 2, false);
		Assert.False(check.CanStart);
	}

	[Fact]
	public void TryStart_accepts_valid_state()
	{
		var check = QuestRules.CanStart(true, true, true, true, 7, 4, 5, false);
		Assert.True(check.CanStart);
	}
}

public class ExerciseRankProgressionTests
{
	[Fact]
	public void First_classifying_sets_validated_minus_three()
	{
		var computation = ExerciseRankCalculator.Evaluate(70, 100);
		var state = ExerciseRankProgression.ApplyTheoretical(null, computation);
		Assert.Equal(7, state.TheoreticalRank);
		Assert.Equal(4, state.ValidatedRank);
		Assert.Equal(5, state.AvailableQuestRank);
		Assert.False(state.HasSuccessfulValidation);
	}

	[Fact]
	public void Below_r1_does_not_initialize_validated()
	{
		var computation = ExerciseRankCalculator.Evaluate(5, 100);
		var state = ExerciseRankProgression.ApplyTheoretical(null, computation);
		Assert.Null(state.TheoreticalRank);
		Assert.Null(state.ValidatedRank);
		Assert.Null(state.AvailableQuestRank);
	}

	[Fact]
	public void Independent_exercises_initialize_separately()
	{
		var bench = ExerciseRankProgression.ApplyTheoretical(null, ExerciseRankCalculator.Evaluate(60, 100));
		var squat = ExerciseRankProgression.ApplyTheoretical(null, ExerciseRankCalculator.Evaluate(40, 100));
		Assert.Equal(3, bench.ValidatedRank);
		Assert.Equal(1, squat.ValidatedRank);
	}

	[Fact]
	public void Validated_does_not_drop_when_theoretical_falls()
	{
		var first = ExerciseRankProgression.ApplyTheoretical(null, ExerciseRankCalculator.Evaluate(70, 100));
		Assert.Equal(4, first.ValidatedRank);
		var afterWeightChange = ExerciseRankProgression.ApplyTheoretical(first, ExerciseRankCalculator.Evaluate(70, 200));
		Assert.Equal(3, afterWeightChange.TheoreticalRank);
		Assert.Equal(4, afterWeightChange.ValidatedRank);
	}

	[Fact]
	public void Quest_success_does_not_skip_ranks()
	{
		var state = ExerciseRankProgression.ApplyTheoretical(null, ExerciseRankCalculator.Evaluate(70, 100));
		var after = ExerciseRankProgression.ApplyQuestSuccess(state);
		Assert.Equal(5, after.ValidatedRank);
		Assert.True(after.HasSuccessfulValidation);
		Assert.Equal(5, after.CalibrationFloor);
		Assert.Null(after.AvailableQuestRank);
	}
}

public class RankLabelsTests
{
	[Fact]
	public void Known_titles()
	{
		Assert.Equal("R1 Débutant", RankLabels.Display(1));
		Assert.Equal("R10 Légende", RankLabels.Display(10));
		Assert.Equal("—", RankLabels.Display(null));
	}
}
