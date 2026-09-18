namespace Rhythmo.Shared.Ranking;

public readonly record struct BodyweightReference(
	BiologicalSexKind Sex,
	double BodyweightKg,
	string Key);

/// <summary>Plus proche par sexe ; égalité de distance → référence supérieure. Pas d’interpolation V1.</summary>
public static class BodyweightStandardSelector
{
	public static readonly BodyweightReference Male70 = new(BiologicalSexKind.Male, 70, "male_70");
	public static readonly BodyweightReference Male100 = new(BiologicalSexKind.Male, 100, "male_100");
	public static readonly BodyweightReference Female60 = new(BiologicalSexKind.Female, 60, "female_60");
	public static readonly BodyweightReference Female90 = new(BiologicalSexKind.Female, 90, "female_90");

	public static BodyweightReference Select(BiologicalSexKind sex, double weightKg)
	{
		var a = sex == BiologicalSexKind.Female ? Female60 : Male70;
		var b = sex == BiologicalSexKind.Female ? Female90 : Male100;
		var da = Math.Abs(weightKg - a.BodyweightKg);
		var db = Math.Abs(weightKg - b.BodyweightKg);
		if (da < db)
			return a;
		if (db < da)
			return b;
		return b;
	}

	public static double SelectR10(
		BiologicalSexKind sex,
		double weightKg,
		double r10Male70,
		double r10Male100,
		double r10Female60,
		double r10Female90)
	{
		var reference = Select(sex, weightKg);
		return reference.Key switch
		{
			"male_70" => r10Male70,
			"male_100" => r10Male100,
			"female_60" => r10Female60,
			_ => r10Female90
		};
	}
}
