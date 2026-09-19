using Rhythmo.Shared.Ranking;
using Xunit;

namespace Rhythmo.Shared.Tests.Ranking;

public class MuscleRankingCatalogTests
{
	[Fact]
	public void Every_classifying_exercise_has_complete_r10_and_measurement()
	{
		foreach (var def in MuscleRankingCatalog.All.Where(x => x.IsClassifying))
		{
			Assert.True(def.MeasurementType.HasValue, def.NameFr);
			Assert.True(def.Source.HasValue, def.NameFr);
			Assert.True(def.R10Male70 is > 0, def.NameFr);
			Assert.True(def.R10Male100 is > 0, def.NameFr);
			Assert.True(def.R10Female60 is > 0, def.NameFr);
			Assert.True(def.R10Female90 is > 0, def.NameFr);
		}
	}

	[Fact]
	public void Non_classifying_exercises_have_no_primary()
	{
		foreach (var def in MuscleRankingCatalog.All.Where(x => !x.IsClassifying))
			Assert.Empty(def.PrimaryMuscles);
	}

	[Fact]
	public void Deadlift_primary_is_erectors_only()
	{
		var dl = MuscleRankingCatalog.Find("Dos", "Soulevé de terre")!;
		Assert.Equal([MuscleIds.Erectors], dl.PrimaryMuscles);
		Assert.Contains(MuscleIds.Glutes, dl.SecondaryMuscles);
		Assert.Contains(MuscleIds.Hamstrings, dl.SecondaryMuscles);
		Assert.Contains(MuscleIds.Traps, dl.SecondaryMuscles);
	}

	[Fact]
	public void Bss_and_lunges_are_classifying()
	{
		var bss = MuscleRankingCatalog.Find("Jambes — Quadriceps", "Bulgarian split squat")!;
		Assert.Equal(90, bss.R10Female90);
		Assert.Contains(MuscleIds.Quads, bss.PrimaryMuscles);
		Assert.Contains(MuscleIds.Glutes, bss.PrimaryMuscles);

		var lunges = MuscleRankingCatalog.Find("Ischios / Fessiers", "Fentes marchées")!;
		Assert.Equal(75, lunges.R10Female90);
		Assert.Contains(MuscleIds.Quads, lunges.PrimaryMuscles);
	}

	[Fact]
	public void Adductors_have_no_primary_in_v1()
	{
		Assert.False(MuscleRankingCatalog.Find("Jambes — Quadriceps", "Adducteur machine")!.IsClassifying);
		Assert.True(MuscleIds.V1Coefficients.ContainsKey(MuscleIds.Adductors));
	}

	[Fact]
	public void V1_coefficients_cover_every_muscle_and_sum_per_group()
	{
		foreach (var muscle in MuscleIds.Muscles)
			Assert.True(MuscleIds.V1Coefficients.ContainsKey(muscle.Id), muscle.Id);

		Assert.Equal(1.0, Sum(MuscleIds.Chest), 10);
		Assert.Equal(1.0, Sum(MuscleIds.Back), 10);
		Assert.Equal(1.0, Sum(MuscleIds.Shoulders), 10);
		Assert.Equal(1.0, Sum(MuscleIds.Arms), 10);
		Assert.Equal(1.0, Sum(MuscleIds.Abs), 10);
		Assert.Equal(1.0, Sum(MuscleIds.Legs), 10);
	}

	private static double Sum(string groupId) =>
		MuscleIds.Muscles.Where(m => m.GroupId == groupId).Sum(m => MuscleIds.V1Coefficients[m.Id]);
}
