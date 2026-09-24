namespace Rhythmo.Shared.Ranking;

public static class RankLabels
{
	private static readonly string[] Titles =
	[
		"",
		"Débutant",
		"Initié",
		"Novice",
		"Confirmé",
		"Intermédiaire",
		"Solide",
		"Avancé",
		"Expert",
		"Élite",
		"Légende"
	];

	public static string Code(int? rank) =>
		rank is >= 1 and <= 10 ? $"R{rank}" : "—";

	public static string Title(int? rank) =>
		rank is >= 1 and <= 10 ? Titles[rank.Value] : "Non évalué";

	public static string Display(int? rank) =>
		rank is >= 1 and <= 10 ? $"R{rank} {Titles[rank.Value]}" : "—";
}
