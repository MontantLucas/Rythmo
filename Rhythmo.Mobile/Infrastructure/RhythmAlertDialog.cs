using Microsoft.Maui.Controls.Shapes;
using Rhythmo.Mobile.Theme;

namespace Rhythmo.Mobile.Infrastructure;

/// <summary>Popup info / erreur cohérente avec le design Rhythmo.</summary>
public static class RhythmAlertDialog
{
	public static Task ShowAsync(
		Page page,
		string title,
		string message,
		string buttonText = "OK",
		bool isError = false)
	{
		var tcs = new TaskCompletionSource();
		var accent = isError ? RhythmColors.Error : RhythmColors.Accent;
		var icon = isError ? "!" : "i";

		var overlay = new Grid
		{
			InputTransparent = false,
			ZIndex = 80,
			BackgroundColor = Colors.Transparent
		};

		var scrim = new BoxView { Color = RhythmColors.Overlay };
		scrim.GestureRecognizers.Add(new TapGestureRecognizer());

		var card = new Border
		{
			BackgroundColor = RhythmColors.Surface2,
			StrokeThickness = 0,
			Padding = new Thickness(24, 22),
			HorizontalOptions = LayoutOptions.Center,
			VerticalOptions = LayoutOptions.Center,
			MaximumWidthRequest = 340,
			StrokeShape = new RoundRectangle { CornerRadius = 22 }
		};

		var iconBadge = new Border
		{
			WidthRequest = 44,
			HeightRequest = 44,
			HorizontalOptions = LayoutOptions.Center,
			BackgroundColor = accent.WithAlpha(0.15f),
			Stroke = accent.WithAlpha(0.45f),
			StrokeThickness = 1,
			StrokeShape = new RoundRectangle { CornerRadius = 22 },
			Content = new Label
			{
				Text = icon,
				FontFamily = "OpenSansSemibold",
				FontSize = 22,
				TextColor = accent,
				HorizontalTextAlignment = TextAlignment.Center,
				VerticalTextAlignment = TextAlignment.Center
			}
		};

		var titleLabel = new Label
		{
			Text = title,
			FontFamily = "OpenSansSemibold",
			FontSize = 18,
			HorizontalTextAlignment = TextAlignment.Center,
			TextColor = RhythmColors.TextPrimary,
			Margin = new Thickness(0, 14, 0, 0)
		};

		var body = new Label
		{
			Text = message,
			FontSize = 14,
			HorizontalTextAlignment = TextAlignment.Center,
			TextColor = RhythmColors.TextSecondary,
			LineBreakMode = LineBreakMode.WordWrap,
			Margin = new Thickness(0, 8, 0, 20)
		};

		var okBtn = new Button
		{
			Text = buttonText,
			FontFamily = "OpenSansSemibold",
			FontSize = 16,
			CornerRadius = 14,
			HeightRequest = 48,
			BackgroundColor = isError ? RhythmColors.Surface1 : RhythmColors.Accent,
			TextColor = isError ? RhythmColors.TextPrimary : RhythmColors.Bg,
			BorderColor = isError ? RhythmColors.TextSecondary.WithAlpha(0.25f) : Colors.Transparent,
			BorderWidth = isError ? 1 : 0
		};

		okBtn.Clicked += (_, _) =>
		{
			DetachOverlay(page, overlay);
			tcs.TrySetResult();
		};

		card.Content = new VerticalStackLayout
		{
			Spacing = 0,
			Children = { iconBadge, titleLabel, body, okBtn }
		};

		overlay.Children.Add(scrim);
		overlay.Children.Add(card);

		if (!AttachOverlay(page, overlay))
		{
			tcs.TrySetResult();
			return tcs.Task;
		}

		return tcs.Task;
	}

	private static bool AttachOverlay(Page page, Grid overlay)
	{
		if (page is not ContentPage cp)
			return false;

		if (cp.Content is Grid host)
		{
			host.Children.Add(overlay);
			return true;
		}

		var wrapper = new Grid();
		if (cp.Content is not null)
			wrapper.Children.Add(cp.Content);
		wrapper.Children.Add(overlay);
		cp.Content = wrapper;
		return true;
	}

	private static void DetachOverlay(Page page, Grid overlay)
	{
		if (page is not ContentPage cp || cp.Content is not Grid host)
			return;

		host.Children.Remove(overlay);
	}
}
