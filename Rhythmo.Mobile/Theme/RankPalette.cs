namespace Rhythmo.Mobile.Theme;

public static class RankPalette
{
	public static readonly Color R1 = Color.FromArgb("#9CA3AF");
	public static readonly Color R2 = Color.FromArgb("#4B6B8A");
	public static readonly Color R3 = Color.FromArgb("#60A5FA");
	public static readonly Color R4 = Color.FromArgb("#16A34A");
	public static readonly Color R5 = Color.FromArgb("#4ADE80");
	public static readonly Color R6 = Color.FromArgb("#7F1D1D");
	public static readonly Color R7 = Color.FromArgb("#F87171");
	public static readonly Color R8 = Color.FromArgb("#F5E942");
	public static readonly Color R9 = Color.FromArgb("#FF6B00");
	public static readonly Color R10 = Color.FromArgb("#FF2D95");
	public static readonly Color Unevaluated = Color.FromArgb("#4B5563");

	public static Color For(int? rank) => rank switch
	{
		1 => R1,
		2 => R2,
		3 => R3,
		4 => R4,
		5 => R5,
		6 => R6,
		7 => R7,
		8 => R8,
		9 => R9,
		10 => R10,
		_ => Unevaluated
	};
}
