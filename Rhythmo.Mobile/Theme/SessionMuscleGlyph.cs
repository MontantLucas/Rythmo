namespace Rhythmo.Mobile.Theme;

/// <summary>Icône Material Symbols Rounded par zone (glyphe BMP + nom de police MAUI).</summary>
internal static class SessionMuscleGlyph
{
	public const string FontAlias = "MatSymRound";

	/// <summary>Glyphe UCS-2 depuis la table codepoints officielle Material Symbols Rounded.</summary>
	public static string FromCategory(string? category)
	{
		var c = (category ?? "").Trim();

		if (c.Contains("Pectoraux", StringComparison.OrdinalIgnoreCase) ||
		    c.Contains("Biceps", StringComparison.OrdinalIgnoreCase) ||
		    c.Contains("Triceps", StringComparison.OrdinalIgnoreCase))
			return Chr(0xEB43); // fitness_center

		if (c.StartsWith("Dos", StringComparison.OrdinalIgnoreCase))
			return Chr(0xE92C); // accessibility_new

		if (c.Contains("Jambe", StringComparison.OrdinalIgnoreCase) ||
		    c.Contains("Mollet", StringComparison.OrdinalIgnoreCase) ||
		    c.Contains("Ischios", StringComparison.OrdinalIgnoreCase) ||
		    c.Contains("Fessiers", StringComparison.OrdinalIgnoreCase) ||
		    c.Contains("Quadriceps", StringComparison.OrdinalIgnoreCase))
			return Chr(0xE566); // directions_run

		if (c.Contains("Épaule", StringComparison.OrdinalIgnoreCase))
			return Chr(0xE837); // radio_button_checked

		if (c.Contains("Abdo", StringComparison.OrdinalIgnoreCase))
			return Chr(0xEA78); // self_improvement

		if (c.Contains("Cardio", StringComparison.OrdinalIgnoreCase))
			return Chr(0xE87E); // favorite

		if (c.Contains("Poids du corps", StringComparison.OrdinalIgnoreCase) ||
		    c.Contains("Street", StringComparison.OrdinalIgnoreCase))
			return Chr(0xEBC4); // sports_gymnastics

		return Chr(0xEBC4);
	}

	public static string DragHandle => Chr(0xE945); // drag_indicator

	public static string ArrowUp => Chr(0xE316); // keyboard_arrow_up

	public static string ArrowDown => Chr(0xE313); // keyboard_arrow_down

	private static string Chr(int codePoint) =>
		char.ConvertFromUtf32(codePoint);
}
