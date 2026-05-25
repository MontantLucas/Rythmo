using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls.Shapes;
using Rhythmo.Mobile.Data;
using Rhythmo.Mobile.Infrastructure;
using Rhythmo.Mobile.Services;
using Rhythmo.Mobile.Theme;

namespace Rhythmo.Mobile;

public partial class DashboardPage : ContentPage
{
	private readonly IDevErrorPresenter _dev =
		ServiceHelper.Services.GetRequiredService<IDevErrorPresenter>();

	private Guid? _nextSessionId;

	public DashboardPage()
	{
		InitializeComponent();
		DashRefresh.Refreshing += async (_, _) =>
		{
			await ReloadAsync().ConfigureAwait(true);
			DashRefresh.IsRefreshing = false;
		};
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		UiNavigation.RunBootstrapInBackground();
		await ReloadAsync().ConfigureAwait(true);
	}

	private async Task ReloadAsync()
	{
		try
		{
			var repo = ServiceHelper.Services.GetRequiredService<IRhythmoRepository>();
			var profileId = ServiceHelper.Services.GetRequiredService<ActiveProfileStore>().Get();

			var profile = await repo.GetProfileAsync(profileId).ConfigureAwait(true)
			              ?? throw new InvalidOperationException("Profil introuvable.");

			GreetingLabel.Text =
				DateTime.Now.Hour < 18 ? $"Bonjour {profile.DisplayName}" : $"Bonsoir {profile.DisplayName}";

			var tpls = await repo.ListSessionTemplatesAsync(profileId).ConfigureAwait(true);

			if (tpls.Count > 0)
			{
				var next = tpls[0];
				_nextSessionId = next.Id;
				NextSessionTitleLabel.Text = next.Title;
				var exCount = await repo.CountSessionExercisesAsync(next.Id).ConfigureAwait(true);
				NextSessionMetaLabel.Text =
					$"{exCount} exercice(s) · MAJ {next.UpdatedUtc.ToLocalTime():d}";
				StartSessionBtn.IsEnabled = exCount > 0;
			}
			else
			{
				_nextSessionId = null;
				NextSessionTitleLabel.Text = "Aucune séance encore";
				NextSessionMetaLabel.Text = "Crée ta première séance pour démarrer.";
				StartSessionBtn.IsEnabled = false;
			}

			var cutoff = DateTime.UtcNow.AddDays(-7);
			var recentWorkouts = await repo.ListCompletedWorkoutsSinceAsync(profileId, cutoff)
				.ConfigureAwait(true);

			StatSessionsLabel.Text = recentWorkouts.Count.ToString(CultureInfo.InvariantCulture);
			double volKg = recentWorkouts.Sum(w => WorkoutAnalytics.ComputeVolumeKgFromPayload(w.PayloadJson));
			StatVolumeLabel.Text = volKg >= 1000
				? $"{volKg / 1000d:0.#} t"
				: $"{volKg:0} kg";
			StatKcalLabel.Text =
				Math.Round(recentWorkouts.Sum(w => w.CaloriesRounded)).ToString(CultureInfo.InvariantCulture);

			var workoutDays = recentWorkouts
				.Select(w => w.CompletedUtc.ToLocalTime().Date)
				.ToHashSet();
			var streak = ComputeDayStreak(workoutDays);
			StreakLabel.Text = streak <= 0
				? "À construire cette semaine"
				: $"{streak} jour(s) avec séance";

			RecentHistoryHost.Children.Clear();
			foreach (var row in recentWorkouts.Take(3))
			{
				var card = new Border
				{
					Padding = new Thickness(16, 14),
					BackgroundColor = RhythmColors.Surface1,
					StrokeThickness = 0,
					StrokeShape = new RoundRectangle { CornerRadius = 16 },
					Content = new VerticalStackLayout
					{
						Spacing = 4,
						Children =
						{
							new Label
							{
								Text = row.SessionTitle,
								FontFamily = "OpenSansSemibold",
								FontSize = 16,
								TextColor = RhythmColors.TextPrimary,
								LineBreakMode = LineBreakMode.TailTruncation
							},
							new Label
							{
								Text = WorkoutHistoryFormatter.BuildListSubtitle(row),
								FontSize = 13,
								TextColor = RhythmColors.TextSecondary
							}
						}
					}
				};
				var tap = new TapGestureRecognizer();
				var wid = row.Id;
				tap.Tapped += async (_, _) =>
					await UiShellNavigate.GoAsync(
						$"{nameof(HistoryDetailPage)}?WorkoutId={Uri.EscapeDataString(wid.ToString())}");
				card.GestureRecognizers.Add(tap);
				RecentHistoryHost.Children.Add(card);
			}
		}
		catch (Exception ex)
		{
			await _dev.TryShowSafeAsync(ex, nameof(ReloadAsync)).ConfigureAwait(false);
		}
	}

	private static int ComputeDayStreak(HashSet<DateTime> workoutDays)
	{
		var cursor = DateTime.Today;
		if (!workoutDays.Contains(cursor))
			cursor = cursor.AddDays(-1);

		var streak = 0;
		while (workoutDays.Contains(cursor))
		{
			streak++;
			cursor = cursor.AddDays(-1);
		}

		return streak;
	}

	private async void OnStartNextClicked(object? sender, EventArgs e)
	{
		if (_nextSessionId is not { } id)
			return;
		await UiShellNavigate.GoAsync($"{nameof(WorkoutRunnerPage)}?SessionId={Uri.EscapeDataString(id.ToString())}")
			.ConfigureAwait(false);
	}

	private async void OnGoSessionsClicked(object? sender, EventArgs e) =>
		await UiShellNavigate.GoAsync("//SessionsPage").ConfigureAwait(false);

	private async void OnGoStatsClicked(object? sender, EventArgs e) =>
		await UiShellNavigate.GoAsync("//StatsPage").ConfigureAwait(false);

	private async void OnQuickNewSessionClicked(object? sender, EventArgs e) =>
		await UiShellNavigate.GoAsync($"{nameof(SessionEditPage)}").ConfigureAwait(false);
}
