using Microsoft.Maui.Controls.Shapes;
using Rhythmo.Mobile.Data;
using Rhythmo.Mobile.Theme;

namespace Rhythmo.Mobile.Social;

internal static class ProfileGenderAvatar
{
	public static View Create(
		string displayName,
		BiologicalSex sex,
		bool highlightMe = false,
		double size = 40,
		Action? onTap = null)
	{
		var isFemale = sex == BiologicalSex.Female;
		var fill = isFemale ? RhythmColors.Pink.WithAlpha(0.88f) : RhythmColors.Accent.WithAlpha(0.85f);
		var highlight = isFemale ? RhythmColors.Pink : RhythmColors.Accent;
		var border = new Border
		{
			WidthRequest = size,
			HeightRequest = size,
			StrokeThickness = highlightMe ? 2 : 0,
			Stroke = highlightMe ? highlight : Colors.Transparent,
			Padding = 0,
			Content = new Label
			{
				Text = GetInitial(displayName),
				FontFamily = "OpenSansSemibold",
				FontSize = size * 0.42,
				TextColor = Colors.White,
				HorizontalTextAlignment = TextAlignment.Center,
				VerticalTextAlignment = TextAlignment.Center
			}
		};

		border.BackgroundColor = fill;
		border.StrokeShape = new RoundRectangle { CornerRadius = (float)(size / 2) };

		if (onTap is not null)
		{
			var tap = new TapGestureRecognizer();
			tap.Tapped += (_, _) => onTap();
			border.GestureRecognizers.Add(tap);
		}

		return border;
	}

	static string GetInitial(string displayName)
	{
		foreach (var c in displayName.Trim())
		{
			if (char.IsLetterOrDigit(c))
				return char.ToUpperInvariant(c).ToString();
		}

		return "?";
	}
}
