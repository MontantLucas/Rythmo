using Microsoft.Maui.Controls.Shapes;
using Rhythmo.Shared.Ranking;

namespace Rhythmo.Mobile.Theme;

internal static class RankBadge
{
	public static View Stacked(int? rank, TextAlignment align = TextAlignment.Start)
	{
		var color = RankPalette.For(rank);
		return new VerticalStackLayout
		{
			Spacing = 2,
			HorizontalOptions = align == TextAlignment.Center
				? LayoutOptions.Center
				: LayoutOptions.Start,
			Children =
			{
				new Label
				{
					Text = RankLabels.Code(rank),
					FontFamily = "OpenSansSemibold",
					FontSize = 28,
					TextColor = color,
					HorizontalTextAlignment = align
				},
				new Label
				{
					Text = RankLabels.Title(rank).ToUpperInvariant(),
					FontFamily = "OpenSansSemibold",
					FontSize = 12,
					TextColor = color,
					HorizontalTextAlignment = align
				}
			}
		};
	}

	public static View Progress(int? from, int to)
	{
		var color = RankPalette.For(to);
		var track = new Grid
		{
			HeightRequest = 6,
			Margin = new Thickness(0, 10, 0, 6)
		};
		track.Children.Add(new BoxView
		{
			Color = Color.FromArgb("#2A3140"),
			CornerRadius = 3
		});
		track.Children.Add(new BoxView
		{
			Color = color,
			CornerRadius = 3,
			WidthRequest = 12,
			HeightRequest = 12,
			VerticalOptions = LayoutOptions.Center,
			HorizontalOptions = LayoutOptions.End,
			Margin = new Thickness(0, 0, 6, 0)
		});

		var labels = new Grid();
		labels.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
		labels.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
		var left = new Label
		{
			Text = $"{RankLabels.Code(from)}  actuel",
			FontSize = 12,
			TextColor = RhythmColors.TextSecondary
		};
		var right = new Label
		{
			Text = $"{RankLabels.Code(to)}  prochain",
			FontSize = 12,
			HorizontalTextAlignment = TextAlignment.End,
			TextColor = color
		};
		labels.Add(left, 0);
		labels.Add(right, 1);
		return new VerticalStackLayout { Spacing = 0, Children = { track, labels } };
	}
}

internal static class MuscleChip
{
	public static Border Primary(string name) => Chip(name, RhythmColors.Accent, Color.FromArgb("#2622D3C5"));

	public static Border Secondary(string name) => Chip(name, RhythmColors.TextSecondary, RhythmColors.Surface2);

	private static Border Chip(string name, Color fg, Color bg) => new()
	{
		Padding = new Thickness(8, 4),
		Margin = new Thickness(0, 0, 6, 6),
		StrokeThickness = 0,
		BackgroundColor = bg,
		StrokeShape = new RoundRectangle { CornerRadius = 10 },
		Content = new Label
		{
			Text = name,
			FontSize = 12,
			FontFamily = "OpenSansSemibold",
			TextColor = fg
		}
	};
}
