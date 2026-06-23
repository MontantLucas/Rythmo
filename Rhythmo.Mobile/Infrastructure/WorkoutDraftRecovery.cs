using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls.Shapes;
using Rhythmo.Mobile.Services;
using Rhythmo.Mobile.Theme;
using Rhythmo.Shared.Contracts;

namespace Rhythmo.Mobile.Infrastructure;

public static class WorkoutDraftRecovery
{
	private static bool _isShowing;

	public static Task TryPromptIfNeededAsync() =>
		MainThread.InvokeOnMainThreadAsync(TryPromptIfNeededCoreAsync);

	static async Task TryPromptIfNeededCoreAsync()
	{
		if (_isShowing || IsOnWorkoutRunnerPage())
			return;

		var profiles = ServiceHelper.Services.GetRequiredService<ActiveProfileStore>();
		if (!profiles.IsAuthenticated)
			return;

		var profileId = profiles.Get();
		var store = ServiceHelper.Services.GetRequiredService<WorkoutDraftStore>();
		if (!store.HasDraft(profileId))
			return;

		var draft = store.TryLoad(profileId);
		if (draft is null || draft.ProfileId != profileId)
			return;

		var host = ResolveHostPage();
		if (host is null)
			return;

		_isShowing = true;
		try
		{
			var choice = await ShowBlockingDialogAsync(host, draft).ConfigureAwait(true);
			if (choice == RecoveryChoice.Resume)
			{
				await UiShellNavigate
					.GoAsync($"{nameof(WorkoutRunnerPage)}?SessionId={Uri.EscapeDataString(draft.SessionId.ToString())}")
					.ConfigureAwait(true);
				return;
			}

			if (choice == RecoveryChoice.CancelSession)
				store.Clear(profileId);
		}
		finally
		{
			_isShowing = false;
		}
	}

	enum RecoveryChoice
	{
		None,
		Resume,
		CancelSession
	}

	static async Task<RecoveryChoice> ShowBlockingDialogAsync(Page page, WorkoutDraftEnvelope draft)
	{
		var tcs = new TaskCompletionSource<RecoveryChoice>();
		var overlay = new Grid
		{
			InputTransparent = false,
			ZIndex = 95,
			BackgroundColor = Colors.Transparent
		};

		var scrim = new BoxView { Color = RhythmColors.Overlay };

		var localTime = TimeZoneInfo.ConvertTimeFromUtc(draft.UpdatedUtc, TimeZoneInfo.Local);
		var when = localTime.ToString("dd/MM/yyyy HH:mm", CultureInfo.CurrentCulture);

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
			Text = "Séance en cours",
			FontFamily = "OpenSansSemibold",
			FontSize = 20,
			HorizontalTextAlignment = TextAlignment.Center,
			TextColor = RhythmColors.TextPrimary
		};

		var body = new Label
		{
			Text = $"Tu as une séance non terminée : « {draft.SessionTitle} » (dernière saisie {when}).",
			FontSize = 14,
			HorizontalTextAlignment = TextAlignment.Center,
			TextColor = RhythmColors.TextSecondary,
			LineBreakMode = LineBreakMode.WordWrap,
			Margin = new Thickness(0, 8, 0, 4)
		};

		var cancelBtn = new Button
		{
			Text = "Annuler la séance",
			FontFamily = "OpenSansSemibold",
			FontSize = 15,
			CornerRadius = 14,
			HeightRequest = 48,
			BackgroundColor = Colors.Transparent,
			TextColor = RhythmColors.Error,
			BorderColor = RhythmColors.Error.WithAlpha(0.45f),
			BorderWidth = 1
		};

		var resumeBtn = new Button
		{
			Text = "Reprendre",
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
			ColumnSpacing = 10,
			Margin = new Thickness(0, 12, 0, 0)
		};
		buttonsRow.Children.Add(cancelBtn);
		buttonsRow.Children.Add(resumeBtn);
		Grid.SetColumn(resumeBtn, 1);

		void Close(RecoveryChoice result)
		{
			DetachOverlay(page, overlay);
			tcs.TrySetResult(result);
		}

		cancelBtn.Clicked += (_, _) => Close(RecoveryChoice.CancelSession);
		resumeBtn.Clicked += (_, _) => Close(RecoveryChoice.Resume);

		card.Content = new VerticalStackLayout
		{
			Spacing = 0,
			Children = { title, body, buttonsRow }
		};

		overlay.Children.Add(scrim);
		overlay.Children.Add(card);

		if (!AttachOverlay(page, overlay))
		{
			tcs.TrySetResult(RecoveryChoice.None);
			return await tcs.Task.ConfigureAwait(true);
		}

		return await tcs.Task.ConfigureAwait(true);
	}

	static bool IsOnWorkoutRunnerPage()
	{
		var page = Shell.Current?.CurrentPage;
		if (page is WorkoutRunnerPage)
			return true;

		if (page is NavigationPage { CurrentPage: WorkoutRunnerPage })
			return true;

		if (page is NavigationPage navPage)
		{
			foreach (var stacked in navPage.Navigation.NavigationStack)
			{
				if (stacked is WorkoutRunnerPage)
					return true;
			}
		}

		return false;
	}

	static Page? ResolveHostPage()
	{
		if (Shell.Current?.CurrentPage is { } shellPage)
			return shellPage;

		return Application.Current?.Windows.FirstOrDefault()?.Page;
	}

	static bool AttachOverlay(Page page, Grid overlay)
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

	static void DetachOverlay(Page page, Grid overlay)
	{
		if (page is not ContentPage cp || cp.Content is not Grid host)
			return;

		host.Children.Remove(overlay);
	}
}
