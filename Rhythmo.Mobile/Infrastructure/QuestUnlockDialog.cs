using Microsoft.Maui.Controls.Shapes;
using Rhythmo.Mobile.Theme;
using Rhythmo.Shared.Ranking;

namespace Rhythmo.Mobile.Infrastructure;

public sealed record QuestUnlockItem(
	Guid ExerciseId,
	string NameFr,
	int TargetRank,
	int? FromRank,
	string Objective);

internal static class QuestUnlockDialog
{
	public static Task<bool> ShowAsync(Page page, IReadOnlyList<QuestUnlockItem> items)
	{
		if (items.Count == 0)
			return Task.FromResult(false);

		var tcs = new TaskCompletionSource<bool>();
		var overlay = new Grid
		{
			InputTransparent = false,
			ZIndex = 90,
			BackgroundColor = Colors.Transparent
		};

		var scrim = new BoxView { Color = RhythmColors.Overlay };

		var accent = RankPalette.For(items[0].TargetRank);
		var card = new Border
		{
			Opacity = 0,
			BackgroundColor = RhythmColors.Surface2,
			StrokeThickness = 1.5,
			Stroke = accent.WithAlpha(0.55f),
			Padding = new Thickness(26, 24),
			HorizontalOptions = LayoutOptions.Center,
			VerticalOptions = LayoutOptions.Center,
			MaximumWidthRequest = 360,
			StrokeShape = new RoundRectangle { CornerRadius = 24 },
			Shadow = new Shadow
			{
				Brush = accent,
				Opacity = 0.28f,
				Radius = 28,
				Offset = new Point(0, 8)
			}
		};

		var layout = new VerticalStackLayout { Spacing = 10 };
		layout.Children.Add(new Label
		{
			Text = "✦",
			FontSize = 28,
			HorizontalTextAlignment = TextAlignment.Center,
			TextColor = accent
		});

		if (items.Count == 1)
		{
			var item = items[0];
			layout.Children.Add(Title("NOUVELLE QUÊTE"));
			layout.Children.Add(new Label
			{
				Text = RankLabels.Code(item.TargetRank),
				FontFamily = "OpenSansSemibold",
				FontSize = 36,
				HorizontalTextAlignment = TextAlignment.Center,
				TextColor = accent
			});
			layout.Children.Add(new Label
			{
				Text = item.NameFr.ToUpperInvariant(),
				FontFamily = "OpenSansSemibold",
				FontSize = 16,
				HorizontalTextAlignment = TextAlignment.Center,
				TextColor = RhythmColors.TextPrimary,
				LineBreakMode = LineBreakMode.WordWrap
			});
			layout.Children.Add(new Label
			{
				Text = RankLabels.Title(item.TargetRank),
				HorizontalTextAlignment = TextAlignment.Center,
				TextColor = accent
			});
			layout.Children.Add(new Label
			{
				Text = "Ton prochain niveau",
				HorizontalTextAlignment = TextAlignment.Center,
				Style = (Style)Application.Current!.Resources["RhythmCaption"]
			});
			layout.Children.Add(new Label
			{
				Text = item.Objective,
				FontFamily = "OpenSansSemibold",
				FontSize = 22,
				HorizontalTextAlignment = TextAlignment.Center,
				TextColor = RhythmColors.TextPrimary,
				Margin = new Thickness(0, 4, 0, 8)
			});
		}
		else
		{
			layout.Children.Add(Title($"{items.Count} NOUVELLES QUÊTES"));
			layout.Children.Add(new Label
			{
				Text = "Ta séance a débloqué de nouveaux défis.",
				HorizontalTextAlignment = TextAlignment.Center,
				Style = (Style)Application.Current!.Resources["RhythmCaption"]
			});
			foreach (var item in items.Take(5))
			{
				layout.Children.Add(new Label
				{
					Text = $"{RankLabels.Code(item.TargetRank)}  {item.NameFr}",
					FontSize = 15,
					HorizontalTextAlignment = TextAlignment.Center,
					TextColor = RhythmColors.TextPrimary,
					LineBreakMode = LineBreakMode.TailTruncation
				});
			}
		}

		var seeBtn = new Button
		{
			Text = items.Count == 1 ? "Voir la quête" : "Voir les quêtes",
			Margin = new Thickness(0, 10, 0, 0),
			MinimumHeightRequest = 48
		};
		var laterBtn = new Button
		{
			Text = "Plus tard",
			Style = (Style)Application.Current!.Resources["RhythmBtnGhost"]
		};

		seeBtn.Clicked += (_, _) =>
		{
			PageOverlay.Detach(page, overlay);
			tcs.TrySetResult(true);
		};
		laterBtn.Clicked += (_, _) =>
		{
			PageOverlay.Detach(page, overlay);
			tcs.TrySetResult(false);
		};

		layout.Children.Add(seeBtn);
		layout.Children.Add(laterBtn);
		card.Content = layout;

		overlay.Children.Add(scrim);
		overlay.Children.Add(card);
		if (!PageOverlay.Attach(page, overlay))
			return Task.FromResult(false);

		_ = card.FadeToAsync(1, 280, Easing.CubicOut);
		return tcs.Task;
	}

	private static Label Title(string text) => new()
	{
		Text = text,
		FontFamily = "OpenSansSemibold",
		FontSize = 14,
		HorizontalTextAlignment = TextAlignment.Center,
		TextColor = RhythmColors.TextSecondary
	};
}
