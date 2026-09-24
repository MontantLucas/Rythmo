using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls.Shapes;
using Rhythmo.Mobile.Data;
using Rhythmo.Mobile.Services;
using Rhythmo.Mobile.Theme;

namespace Rhythmo.Mobile.Infrastructure;

/// <summary>
/// Un défi fermé sans décision reste en cours : popup Reprendre / Refuser (échec).
/// </summary>
internal static class QuestResumeDialog
{
	private static Task? _inFlight;

	public static Task TryPromptIfNeededAsync()
	{
		if (_inFlight is { IsCompleted: false })
			return _inFlight;
		_inFlight = MainThread.InvokeOnMainThreadAsync(TryPromptCoreAsync);
		return _inFlight;
	}

	public static Task<bool> AskAsync(Page page, string exerciseName) =>
		ShowAsync(page, exerciseName);

	private static async Task TryPromptCoreAsync()
	{
		if (IsOnQuestPage())
			return;

		var profiles = ServiceHelper.Services.GetRequiredService<ActiveProfileStore>();
		if (!profiles.IsAuthenticated)
			return;

		var profileId = profiles.Get();
		var quests = ServiceHelper.Services.GetRequiredService<RankQuestService>();
		await quests.ExpireStaleAsync(profileId).ConfigureAwait(true);

		var repo = ServiceHelper.Services.GetRequiredService<IRhythmoRepository>();
		var attempt = await repo.GetInProgressQuestAsync(profileId).ConfigureAwait(true);
		if (attempt is null)
			return;

		var host = ResolveHostPage();
		if (host is null)
			return;

		var exercises = await repo.ListExercisesAsync().ConfigureAwait(true);
		var name = exercises.FirstOrDefault(e => e.Id == attempt.ExerciseId)?.NameFr ?? "ce défi";

		var resume = await ShowAsync(host, name).ConfigureAwait(true);
		if (resume)
		{
			await RankUi.GoQuest(attempt.ExerciseId).ConfigureAwait(true);
			return;
		}

		await quests.AbandonAsync(attempt).ConfigureAwait(true);
	}

	private static async Task<bool> ShowAsync(Page page, string exerciseName)
	{
		var tcs = new TaskCompletionSource<bool>();
		var overlay = new Grid
		{
			InputTransparent = false,
			ZIndex = 96,
			BackgroundColor = Colors.Transparent
		};

		var scrim = new BoxView { Color = RhythmColors.Overlay };
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
			Text = "Défi en cours",
			FontFamily = "OpenSansSemibold",
			FontSize = 20,
			HorizontalTextAlignment = TextAlignment.Center,
			TextColor = RhythmColors.TextPrimary
		};

		var body = new Label
		{
			Text = $"Reprendre « {exerciseName} » ? Si tu refuses, le défi est considéré comme échoué.",
			FontSize = 14,
			HorizontalTextAlignment = TextAlignment.Center,
			TextColor = RhythmColors.TextSecondary,
			LineBreakMode = LineBreakMode.WordWrap,
			Margin = new Thickness(0, 8, 0, 4)
		};

		var refuseBtn = new Button
		{
			Text = "Refuser",
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

		var buttons = new Grid
		{
			ColumnDefinitions = new ColumnDefinitionCollection
			{
				new(GridLength.Star),
				new(GridLength.Star)
			},
			ColumnSpacing = 10,
			Margin = new Thickness(0, 12, 0, 0)
		};
		buttons.Children.Add(refuseBtn);
		buttons.Children.Add(resumeBtn);
		Grid.SetColumn(resumeBtn, 1);

		void Close(bool resume)
		{
			PageOverlay.Detach(page, overlay);
			tcs.TrySetResult(resume);
		}

		refuseBtn.Clicked += (_, _) => Close(false);
		resumeBtn.Clicked += (_, _) => Close(true);

		card.Content = new VerticalStackLayout
		{
			Spacing = 0,
			Children = { title, body, buttons }
		};
		overlay.Children.Add(scrim);
		overlay.Children.Add(card);

		if (!PageOverlay.Attach(page, overlay))
		{
			tcs.TrySetResult(true);
			return await tcs.Task.ConfigureAwait(true);
		}

		return await tcs.Task.ConfigureAwait(true);
	}

	private static bool IsOnQuestPage()
	{
		var page = Shell.Current?.CurrentPage;
		if (page is RankQuestPage)
			return true;
		if (page is NavigationPage { CurrentPage: RankQuestPage })
			return true;
		return false;
	}

	private static Page? ResolveHostPage()
	{
		if (Shell.Current?.CurrentPage is { } shellPage)
			return shellPage;
		return Application.Current?.Windows.FirstOrDefault()?.Page;
	}
}
