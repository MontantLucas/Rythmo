using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Controls;
using Rhythmo.Mobile.Services;
using Rhythmo.Mobile.Theme;

namespace Rhythmo.Mobile.Infrastructure;

/// <summary>Bandeau non bloquant : la séance s'enregistre pendant que l'utilisateur continue.</summary>
internal static class WorkoutSaveBanner
{
	private static Shell? _shell;
	private static WorkoutFinalizeService? _service;
	private static Grid? _overlay;
	private static Page? _host;
	private static WorkoutFinalizeUpdate? _current;
	private static bool _hooked;
	private static int _hideGeneration;
	private static bool _dismissed;

	public static void NotifyShellReady() => RunOnMain(Reattach);

	public static void Bind(Shell shell, WorkoutFinalizeService service)
	{
		_shell = shell;
		_service = service;
		shell.Navigated += (_, _) => RunOnMain(Reattach);
		if (_hooked)
			return;

		_hooked = true;
		service.Updated += (_, update) =>
		{
			RunOnMain(() =>
			{
				_current = update;
				_dismissed = false;
				Show(update);
			});
		};
	}

	private static void Reattach()
	{
		if (_dismissed || _current is null)
			return;
		Show(_current);
	}

	private static void Show(WorkoutFinalizeUpdate update)
	{
		Interlocked.Increment(ref _hideGeneration);
		var page = _shell?.CurrentPage;
		if (page is null)
			return;

		Detach();
		_overlay = BuildOverlay(update);
		if (!PageOverlay.Attach(page, _overlay))
		{
			_overlay = null;
			return;
		}

		_host = page;
		ScheduleAutoHide(update);
	}

	private static void ScheduleAutoHide(WorkoutFinalizeUpdate update)
	{
		if (update.Phase != WorkoutFinalizePhase.Saved || update.Unlocks.Count > 0)
			return;

		var generation = Volatile.Read(ref _hideGeneration);
		_ = Task.Run(async () =>
		{
			await Task.Delay(TimeSpan.FromSeconds(4)).ConfigureAwait(false);
			if (generation != Volatile.Read(ref _hideGeneration))
				return;
			RunOnMain(Dismiss);
		});
	}

	private static void Dismiss()
	{
		_dismissed = true;
		_hideGeneration++;
		Detach();
	}

	private static void Detach()
	{
		if (_host is not null && _overlay is not null)
			PageOverlay.Detach(_host, _overlay);
		_host = null;
		_overlay = null;
	}

	private static Grid BuildOverlay(WorkoutFinalizeUpdate update)
	{
		var accent = update.Unlocks.Count > 0
			? RankPalette.For(update.Unlocks[0].TargetRank)
			: update.Phase switch
			{
				WorkoutFinalizePhase.Failed => RhythmColors.Error,
				WorkoutFinalizePhase.Saved => RhythmColors.Success,
				_ => RhythmColors.Accent
			};

		var overlay = new Grid
		{
			ZIndex = 60,
			BackgroundColor = Colors.Transparent,
			HorizontalOptions = LayoutOptions.Fill,
			VerticalOptions = LayoutOptions.Start,
			RowDefinitions = [new RowDefinition(GridLength.Auto)],
			InputTransparent = false
		};

		var label = new Label
		{
			Text = update.Message,
			FontFamily = "OpenSansSemibold",
			FontSize = 14,
			TextColor = RhythmColors.TextPrimary,
			LineBreakMode = LineBreakMode.WordWrap,
			VerticalOptions = LayoutOptions.Center
		};

		var row = new Grid
		{
			ColumnDefinitions =
			[
				new ColumnDefinition(GridLength.Auto),
				new ColumnDefinition(GridLength.Star),
				new ColumnDefinition(GridLength.Auto)
			],
			ColumnSpacing = 10
		};

		if (update.Phase == WorkoutFinalizePhase.Saving)
		{
			var spinner = new ActivityIndicator
			{
				IsRunning = true,
				Color = accent,
				WidthRequest = 18,
				HeightRequest = 18,
				VerticalOptions = LayoutOptions.Center
			};
			row.Add(spinner);
		}
		else
		{
			row.Add(new Label
			{
				Text = update.Phase == WorkoutFinalizePhase.Saved ? "✓" : "!",
				FontSize = 18,
				TextColor = accent,
				VerticalOptions = LayoutOptions.Center
			});
		}

		Grid.SetColumn(label, 1);
		row.Add(label);

		if (update.Phase != WorkoutFinalizePhase.Saving)
		{
			var close = new Label
			{
				Text = "✕",
				FontSize = 16,
				TextColor = RhythmColors.TextSecondary,
				VerticalOptions = LayoutOptions.Center,
				Padding = new Thickness(6, 0)
			};
			var closeTap = new TapGestureRecognizer();
			closeTap.Tapped += (_, _) => Dismiss();
			close.GestureRecognizers.Add(closeTap);
			Grid.SetColumn(close, 2);
			row.Add(close);
		}

		var card = new Border
		{
			BackgroundColor = RhythmColors.Surface2,
			StrokeThickness = 1,
			Stroke = accent.WithAlpha(0.65f),
			Padding = new Thickness(14, 12),
			Margin = new Thickness(16, 10, 16, 0),
			HorizontalOptions = LayoutOptions.Fill,
			VerticalOptions = LayoutOptions.Start,
			StrokeShape = new RoundRectangle { CornerRadius = 16 },
			Shadow = new Shadow
			{
				Brush = Colors.Black,
				Opacity = 0.35f,
				Radius = 16,
				Offset = new Point(0, 4)
			},
			Content = row
		};

		if (update.Phase != WorkoutFinalizePhase.Saving)
		{
			var tap = new TapGestureRecognizer();
			tap.Tapped += async (_, _) => await OnTappedAsync(update).ConfigureAwait(true);
			label.GestureRecognizers.Add(tap);
		}

		overlay.Add(card);
		return overlay;
	}

	private static async Task OnTappedAsync(WorkoutFinalizeUpdate update)
	{
		var page = _shell?.CurrentPage;
		var service = _service;
		if (page is null || service is null)
			return;

		if (update.Phase == WorkoutFinalizePhase.Failed)
		{
			if (update.NeedsReauth)
			{
				var auth = ServiceHelper.Services.GetRequiredService<SupabaseAuthService>();
				var relog = await RhythmReauthDialog.ShowAsync(
					page,
					auth.CurrentUserEmail,
					"Ta session a expiré. Reconnecte-toi pour terminer l'enregistrement de la séance.")
					.ConfigureAwait(true);
				if (!relog.Confirmed)
					return;

				var (ok, err) = await auth.SignInWithPasswordAsync(relog.Email, relog.Password)
					.ConfigureAwait(false);
				if (!ok)
				{
					await RhythmAlertDialog.ShowAsync(
						page,
						"Connexion refusée",
						err ?? "Impossible de se reconnecter.",
						isError: true).ConfigureAwait(true);
					return;
				}
			}

			if (update.WorkoutId is { } id)
				service.Retry(id);
			return;
		}

		if (update.Phase != WorkoutFinalizePhase.Saved || update.Unlocks.Count == 0)
			return;

		Dismiss();
		var open = await QuestUnlockDialog.ShowAsync(page, update.Unlocks).ConfigureAwait(true);
		if (!open)
			return;

		if (update.Unlocks.Count == 1)
			await RankUi.GoQuest(update.Unlocks[0].ExerciseId).ConfigureAwait(true);
		else
			await UiShellNavigate.GoAsync(nameof(RankQuestsPage)).ConfigureAwait(true);
	}

	private static void RunOnMain(Action action)
	{
		if (MainThread.IsMainThread)
			action();
		else
			MainThread.BeginInvokeOnMainThread(action);
	}
}
