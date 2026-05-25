using Microsoft.Extensions.DependencyInjection;
using Rhythmo.Mobile.Infrastructure;
using Rhythmo.Mobile.Services;
using Rhythmo.Mobile.Social;

namespace Rhythmo.Mobile;

public partial class FriendsPage : ContentPage
{
	private readonly IRhythmoRepository _repo = ServiceHelper.Services.GetRequiredService<IRhythmoRepository>();
	private readonly SocialHubService _hub =
		ServiceHelper.Services.GetRequiredService<SocialHubService>();
	private readonly IDevErrorPresenter _dev = ServiceHelper.Services.GetRequiredService<IDevErrorPresenter>();

	private LeaderboardPeriod _period = LeaderboardPeriod.Week;
	private SocialHubSnapshot? _snapshot;
	private int _reloadGeneration;

	public FriendsPage()
	{
		InitializeComponent();
		FriendsRefresh.Refreshing += async (_, _) =>
		{
			_hub.InvalidateCache();
			await ReloadAsync().ConfigureAwait(true);
			FriendsRefresh.IsRefreshing = false;
		};
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		await ReloadAsync().ConfigureAwait(true);
	}

	private async Task ReloadAsync()
	{
		var generation = Interlocked.Increment(ref _reloadGeneration);
		try
		{
			var meId = ServiceHelper.Services.GetRequiredService<ActiveProfileStore>().Get();
			var snapshot = await _hub.BuildAsync(_repo, meId, _period).ConfigureAwait(true);
			if (generation != _reloadGeneration)
				return;

			_snapshot = snapshot;
			await MainThread.InvokeOnMainThreadAsync(RenderAll).ConfigureAwait(true);
		}
		catch (Exception ex)
		{
			if (generation == _reloadGeneration)
				await _dev.TryShowSafeAsync(ex, nameof(ReloadAsync)).ConfigureAwait(true);
		}
	}

	private void RenderAll()
	{
		if (_snapshot is null)
			return;

		LeaderboardHost.Content = FriendsHubUi.LeaderboardSection(
			BuildPeriodTabs(),
			_snapshot.SessionLeaderboard,
			_snapshot.VolumeLeaderboard,
			ShowProfileNameAsync);

		PrSectionHeaderHost.Content = FriendsHubUi.SectionHeader(
			"Nouveaux PR",
			"Voir tout",
			() => _ = UiShellNavigate.GoAsync(nameof(PrFeedPage)),
			() => _ = UiShellNavigate.GoAsync(nameof(PrFeedPage)));
		RenderHost(PrFeedHost, _snapshot.RecentPrs.Count == 0
			? [FriendsHubUi.EmptyHint("Aucun PR récent dans la communauté.")]
			: _snapshot.RecentPrs.Select(FriendsHubUi.PrCard).Cast<View>().ToList());

		var todayViews = _snapshot.TodayActivities.Count == 0
			? [FriendsHubUi.EmptyHint("Aucune séance terminée aujourd'hui.")]
			: _snapshot.TodayActivities.Select(FriendsHubUi.TodayRow).Cast<View>().ToList();
		var liveViews = _snapshot.LiveSessions.Count == 0
			? [FriendsHubUi.EmptyHint("Personne en séance.")]
			: _snapshot.LiveSessions.Select(FriendsHubUi.LiveRow).Cast<View>().ToList();
		ActivityHost.Content = FriendsHubUi.ActivityColumns(todayViews, liveViews);

		StreaksHost.Content = _snapshot.StreakSummary is { } s
			? FriendsHubUi.StreakSummaryCard(s)
			: FriendsHubUi.EmptyHint("Les streaks apparaissent après quelques jours actifs.");

		BadgesSectionHeaderHost.Content = FriendsHubUi.SectionHeader("Badges");
		BadgesHost.Content = _snapshot.Badges.Count == 0
			? FriendsHubUi.EmptyHint("Badges — continue à t'entraîner.")
			: FriendsHubUi.BadgesCarousel(_snapshot.Badges);
	}

	private IReadOnlyList<View> BuildPeriodTabs() =>
	[
		FriendsHubUi.PeriodTab("Semaine", _period == LeaderboardPeriod.Week, () => SetPeriod(LeaderboardPeriod.Week)),
		FriendsHubUi.PeriodTab("Mois", _period == LeaderboardPeriod.Month, () => SetPeriod(LeaderboardPeriod.Month)),
		FriendsHubUi.PeriodTab("All-time", _period == LeaderboardPeriod.AllTime, () => SetPeriod(LeaderboardPeriod.AllTime))
	];

	private async void SetPeriod(LeaderboardPeriod period)
	{
		if (_period == period)
			return;
		_period = period;
		_hub.InvalidateCache();
		await ReloadAsync().ConfigureAwait(true);
	}

	private async void ShowProfileNameAsync(string name)
	{
		await DisplayAlertAsync(name, null, "OK").ConfigureAwait(true);
	}

	private static void RenderHost(VerticalStackLayout host, IReadOnlyList<View> children)
	{
		host.Children.Clear();
		foreach (var c in children)
			host.Children.Add(c);
	}
}
