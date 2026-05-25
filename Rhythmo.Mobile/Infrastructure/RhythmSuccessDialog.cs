using Microsoft.Maui.Controls.Shapes;
using Rhythmo.Mobile.Theme;

namespace Rhythmo.Mobile.Infrastructure;

/// <summary>Popup succès cohérente avec le design Rhythmo (remplace DisplayAlert système).</summary>
public static class RhythmSuccessDialog
{
	public static Task ShowAsync(Page page, string message, string buttonText = "OK")
	{
		var tcs = new TaskCompletionSource();

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
			Padding = new Thickness(28, 26),
			HorizontalOptions = LayoutOptions.Center,
			VerticalOptions = LayoutOptions.Center,
			MaximumWidthRequest = 340,
			StrokeShape = new RoundRectangle { CornerRadius = 22 },
			Shadow = new Shadow
			{
				Brush = Colors.Black,
				Opacity = 0.45f,
				Radius = 24,
				Offset = new Point(0, 6)
			}
		};

		var check = new Label
		{
			Text = "✓",
			FontSize = 42,
			HorizontalTextAlignment = TextAlignment.Center,
			TextColor = RhythmColors.Accent
		};

		var body = new Label
		{
			Text = message,
			FontFamily = "OpenSansSemibold",
			FontSize = 17,
			HorizontalTextAlignment = TextAlignment.Center,
			TextColor = RhythmColors.TextPrimary,
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
			BackgroundColor = RhythmColors.Accent,
			TextColor = RhythmColors.Bg
		};

		okBtn.Clicked += (_, _) =>
		{
			DetachOverlay(page, overlay);
			tcs.TrySetResult();
		};

		card.Content = new VerticalStackLayout
		{
			Spacing = 0,
			Children = { check, body, okBtn }
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
