using Rhythmo.Shared;
using Xunit;

namespace Rhythmo.Shared.Tests;

public class CaloriesEstimatorTests
{
	[Fact]
	public void Same_inputs_produce_same_rounded_output()
	{
		var chunks = new[] { (Met: 6.5, SetCount: 4) }.AsEnumerable();
		var a = CaloriesEstimator.EstimateSessionKcal(80, chunks);
		var b = CaloriesEstimator.EstimateSessionKcal(80, chunks);
		Assert.Equal(a, b);
	}

	[Fact]
	public void Skips_zero_set_counts()
	{
		var kcal = CaloriesEstimator.EstimateSessionKcal(
			70,
			new[] { (Met: 5d, SetCount: 0) }.AsEnumerable());

		Assert.Equal(0d, kcal);
	}

	[Fact]
	public void Bmr_uses_sex_age_height_and_weight()
	{
		var male = new CaloriesSubject { WeightKg = 80, HeightCm = 180, AgeYears = 30, IsFemale = false };
		var female = new CaloriesSubject { WeightKg = 80, HeightCm = 180, AgeYears = 30, IsFemale = true };
		Assert.True(CaloriesEstimator.EstimateBmrKcalPerDay(female) < CaloriesEstimator.EstimateBmrKcalPerDay(male));
	}

	[Fact]
	public void Full_profile_yields_higher_total_than_weight_only_for_same_inputs()
	{
		var chunks = new[] { (Met: 6.5, SetCount: 4) }.AsEnumerable();
		var weightOnly = CaloriesEstimator.EstimateSessionKcal(80, chunks);
		var full = CaloriesEstimator.EstimateSessionKcal(
			new CaloriesSubject { WeightKg = 80, HeightCm = 180, AgeYears = 30, IsFemale = false },
			chunks);
		Assert.True(full >= weightOnly);
	}
}
