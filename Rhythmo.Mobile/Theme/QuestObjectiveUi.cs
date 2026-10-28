using System.Globalization;
using Rhythmo.Shared.Ranking;

namespace Rhythmo.Mobile.Theme;

internal static class QuestObjectiveUi
{
	private static readonly CultureInfo Fr = CultureInfo.GetCultureInfo("fr-FR");

	public static string FormatSet(QuestSetSuggestion set, LoadMode loadMode, bool includeRepsWord = true) =>
		FormatSet(set.WeightKg, set.Reps, loadMode, includeRepsWord);

	public static string FormatSet(double kg, int reps, LoadMode loadMode, bool includeRepsWord = true)
	{
		var weight = kg.ToString("0.#", Fr);
		var unit = loadMode switch
		{
			LoadMode.PerDumbbell => "kg / haltère",
			LoadMode.AddedLoad => "kg ajoutés",
			_ => "kg"
		};
		var repsPart = includeRepsWord ? $"{reps} reps" : $"{reps}";
		return $"{weight} {unit} × {repsPart}";
	}
}
