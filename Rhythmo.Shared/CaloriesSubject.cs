namespace Rhythmo.Shared;

/// <summary>Données biométriques pour estimer l’énergie dépensée pendant une séance.</summary>
public sealed class CaloriesSubject
{
	public double WeightKg { get; init; } = 75;
	public bool IsFemale { get; init; }
	public int? AgeYears { get; init; }
	public double? HeightCm { get; init; }
}
