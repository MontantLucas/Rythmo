namespace Rhythmo.Mobile.Social;

/// <summary>Glyphes Material Symbols Rounded (police <see cref="Theme.SessionMuscleGlyph.FontAlias"/>).</summary>
internal static class SocialGlyph
{
	public const string Font = Theme.SessionMuscleGlyph.FontAlias;

	public static string Event => Chr(0xE878);
	public static string FitnessCenter => Chr(0xEB43);
	public static string Bolt => Chr(0xE7E1);
	public static string TrendUp => Chr(0xE8E5);
	public static string TrendDown => Chr(0xE8E6);
	public static string TrendFlat => Chr(0xF88B);
	public static string Lock => Chr(0xE897);

	private static string Chr(int cp) => char.ConvertFromUtf32(cp);
}
