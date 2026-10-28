using Rhythmo.Shared.Ranking;
using Xunit;

namespace Rhythmo.Shared.Tests.Ranking;

public class QuestObjectivePlannerTests
{
	private const double R10 = 100;

	[Fact]
	public void Five_rm_primary_reaches_threshold()
	{
		var option = QuestObjectivePlanner.ForRank(R10, 7, MeasurementType.FiveRm, LoadMode.Total);
		Assert.Equal(7, option.Rank);
		Assert.Equal(5, option.Primary.Reps);
		Assert.True(E1Rm.From(option.Primary.WeightKg, option.Primary.Reps)
		            >= ExerciseRankCalculator.RankThreshold(R10, 7) - 1e-9);
	}

	[Fact]
	public void Rounded_weight_never_falls_below_threshold()
	{
		var option = QuestObjectivePlanner.ForRank(137, 4, MeasurementType.FiveRm, LoadMode.Total);
		var threshold = ExerciseRankCalculator.RankThreshold(137, 4);
		Assert.True(E1Rm.From(option.Primary.WeightKg, option.Primary.Reps) >= threshold - 1e-9);
		foreach (var eq in option.Equivalences)
			Assert.True(E1Rm.From(eq.WeightKg, eq.Reps) >= threshold - 1e-9);
	}

	[Fact]
	public void Equivalences_for_r1_to_r4_use_other_rm_formats()
	{
		var option = QuestObjectivePlanner.ForRank(R10, 3, MeasurementType.FiveRm, LoadMode.Total);
		Assert.Equal(5, option.Primary.Reps);
		Assert.Equal(2, option.Equivalences.Count);
		Assert.Contains(option.Equivalences, eq => eq.Reps == 1);
		Assert.Contains(option.Equivalences, eq => eq.Reps == 10);
	}

	[Fact]
	public void R5_and_above_have_no_equivalences()
	{
		var option = QuestObjectivePlanner.ForRank(R10, 5, MeasurementType.FiveRm, LoadMode.Total);
		Assert.Empty(option.Equivalences);
		Assert.Equal(5, option.Primary.Reps);
	}

	[Fact]
	public void Available_targets_only_offer_validated_plus_one()
	{
		var targets = QuestObjectivePlanner.AvailableTargets(
			R10, theoreticalRank: 8, validatedRank: 4, MeasurementType.FiveRm, LoadMode.Total);
		Assert.Single(targets);
		Assert.Equal(5, targets[0].Rank);
	}

	[Fact]
	public void Available_targets_empty_when_theoretical_too_low()
	{
		var targets = QuestObjectivePlanner.AvailableTargets(
			R10, theoreticalRank: 4, validatedRank: 4, MeasurementType.FiveRm, LoadMode.Total);
		Assert.Empty(targets);
	}

	[Fact]
	public void One_rm_primary_uses_single_rep()
	{
		var option = QuestObjectivePlanner.ForRank(R10, 3, MeasurementType.OneRm, LoadMode.Total);
		Assert.Equal(1, option.Primary.Reps);
		Assert.True(E1Rm.From(option.Primary.WeightKg, 1)
		            >= ExerciseRankCalculator.RankThreshold(R10, 3) - 1e-9);
	}
}
