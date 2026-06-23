using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls.Shapes;
using Rhythmo.Mobile;
using Rhythmo.Mobile.Theme;

namespace Rhythmo.Mobile.Infrastructure;

public static class RhythmReauthDialog
{
	public sealed record Result(bool Confirmed, string Email, string Password);

	public static Task<Result> ShowAsync(
		Page page,
		string? email,
		string message = "Ta session a expiré. Reconnecte-toi pour enregistrer la séance sans perdre les données saisies.")
	{
		var tcs = new TaskCompletionSource<Result>();
		var overlay = new Grid
		{
			InputTransparent = false,
			ZIndex = 90,
			BackgroundColor = Colors.Transparent
		};

		var scrim = new BoxView { Color = RhythmColors.Overlay };
		scrim.GestureRecognizers.Add(new TapGestureRecognizer());

		var emailEntry = new Entry
		{
			Text = email ?? "",
			Placeholder = "E-mail",
			Keyboard = Keyboard.Email,
			BackgroundColor = Colors.Transparent,
			TextColor = RhythmColors.TextPrimary,
			PlaceholderColor = RhythmColors.TextSecondary,
			Margin = new Thickness(4, 0)
		};
		var passwordEntry = new Entry
		{
			Placeholder = "Mot de passe",
			IsPassword = true,
			BackgroundColor = Colors.Transparent,
			TextColor = RhythmColors.TextPrimary,
			PlaceholderColor = RhythmColors.TextSecondary,
			Margin = new Thickness(4, 0, 0, 0)
		};
		var togglePasswordBtn = new Button
		{
			Text = LoginPageGlyphs.VisibilityOff,
			FontFamily = LoginPageGlyphs.Font,
			FontSize = 22,
			TextColor = RhythmColors.TextSecondary,
			BackgroundColor = Colors.Transparent,
			BorderWidth = 0,
			Padding = new Thickness(12, 0),
			MinimumWidthRequest = 48,
			MinimumHeightRequest = 46
		};

		togglePasswordBtn.Clicked += (_, _) =>
		{
			passwordEntry.IsPassword = !passwordEntry.IsPassword;
			togglePasswordBtn.Text = passwordEntry.IsPassword
				? LoginPageGlyphs.VisibilityOff
				: LoginPageGlyphs.Visibility;
		};

		var passwordRow = new Grid
		{
			ColumnDefinitions = new ColumnDefinitionCollection
			{
				new(GridLength.Star),
				new(GridLength.Auto)
			}
		};
		passwordRow.Children.Add(passwordEntry);
		passwordRow.Children.Add(togglePasswordBtn);
		Grid.SetColumn(togglePasswordBtn, 1);

		var card = new Border
		{
			BackgroundColor = RhythmColors.Surface2,
			StrokeThickness = 0,
			Padding = new Thickness(24, 22),
			HorizontalOptions = LayoutOptions.Center,
			VerticalOptions = LayoutOptions.Center,
			MaximumWidthRequest = 360,
			StrokeShape = new RoundRectangle { CornerRadius = 22 }
		};

		var title = new Label
		{
			Text = "Reconnecte-toi",
			FontFamily = "OpenSansSemibold",
			FontSize = 20,
			HorizontalTextAlignment = TextAlignment.Center,
			TextColor = RhythmColors.TextPrimary
		};

		var body = new Label
		{
			Text = message,
			FontSize = 14,
			HorizontalTextAlignment = TextAlignment.Center,
			TextColor = RhythmColors.TextSecondary,
			LineBreakMode = LineBreakMode.WordWrap,
			Margin = new Thickness(0, 8, 0, 4)
		};

		var error = new Label
		{
			IsVisible = false,
			FontSize = 13,
			TextColor = RhythmColors.Error,
			HorizontalTextAlignment = TextAlignment.Center,
			Margin = new Thickness(0, 2, 0, 0)
		};

		var cancelBtn = new Button
		{
			Text = "Plus tard",
			FontFamily = "OpenSansSemibold",
			FontSize = 15,
			CornerRadius = 14,
			HeightRequest = 48,
			BackgroundColor = Colors.Transparent,
			TextColor = RhythmColors.TextSecondary,
			BorderColor = RhythmColors.TextSecondary.WithAlpha(0.25f),
			BorderWidth = 1
		};

		var confirmBtn = new Button
		{
			Text = "Reconnecter",
			FontFamily = "OpenSansSemibold",
			FontSize = 15,
			CornerRadius = 14,
			HeightRequest = 48,
			BackgroundColor = RhythmColors.Accent,
			TextColor = RhythmColors.Bg
		};

		var buttonsRow = new Grid
		{
			ColumnDefinitions = new ColumnDefinitionCollection
			{
				new(GridLength.Star),
				new(GridLength.Star)
			},
			ColumnSpacing = 10
		};
		buttonsRow.Children.Add(cancelBtn);
		buttonsRow.Children.Add(confirmBtn);
		Grid.SetColumn(confirmBtn, 1);

		void Close(Result result)
		{
			DetachOverlay(page, overlay);
			tcs.TrySetResult(result);
		}

		cancelBtn.Clicked += (_, _) => Close(new Result(false, emailEntry.Text?.Trim() ?? "", ""));
		confirmBtn.Clicked += (_, _) =>
		{
			var enteredEmail = emailEntry.Text?.Trim() ?? "";
			var password = passwordEntry.Text ?? "";
			if (string.IsNullOrWhiteSpace(enteredEmail) || string.IsNullOrWhiteSpace(password))
			{
				error.Text = "E-mail et mot de passe requis.";
				error.IsVisible = true;
				return;
			}

			Close(new Result(true, enteredEmail, password));
		};

		card.Content = new VerticalStackLayout
		{
			Spacing = 12,
			Children =
			{
				title,
				body,
				FieldBorder(emailEntry),
				FieldBorder(passwordRow),
				error,
				buttonsRow
			}
		};

		overlay.Children.Add(scrim);
		overlay.Children.Add(card);

		if (!AttachOverlay(page, overlay))
		{
			tcs.TrySetResult(new Result(false, emailEntry.Text?.Trim() ?? "", ""));
			return tcs.Task;
		}

		MainThread.BeginInvokeOnMainThread(() => passwordEntry.Focus());
		return tcs.Task;
	}

	private static Border FieldBorder(View content) => new()
	{
		BackgroundColor = RhythmColors.Surface1,
		StrokeThickness = 0,
		Padding = new Thickness(12, 4),
		StrokeShape = new RoundRectangle { CornerRadius = 16 },
		Content = content
	};

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
