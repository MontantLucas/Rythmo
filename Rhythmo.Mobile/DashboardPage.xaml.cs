using Microsoft.Extensions.DependencyInjection;
using Rhythmo.Mobile.Controls.Anatomy;
using Rhythmo.Mobile.Infrastructure;
using Rhythmo.Mobile.Services;
using Rhythmo.Mobile.Theme;
using Rhythmo.Shared.Ranking;

namespace Rhythmo.Mobile;

public partial class DashboardPage : ContentPage
{
	private readonly IDevErrorPresenter _dev =
		ServiceHelper.Services.GetRequiredService<IDevErrorPresenter>();

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
		await QuestResumeDialog.TryPromptIfNeededAsync().ConfigureAwait(true);
		await ReloadAsync().ConfigureAwait(true);
	}

	private async Task ReloadAsync()
	{
		try
		{
			var repo = ServiceHelper.Services.GetRequiredService<IRhythmoRepository>();
			var profileId = ServiceHelper.Services.GetRequiredService<ActiveProfileStore>().Get();
			await LoadBodyAsync(repo, profileId).ConfigureAwait(true);
		}
		catch (Exception ex)
		{
			await _dev.TryShowSafeAsync(ex, nameof(ReloadAsync)).ConfigureAwait(false);
		}
	}

	private async Task LoadBodyAsync(IRhythmoRepository repo, Guid profileId)
	{
		try
		{
			await ServiceHelper.Services.GetRequiredService<RankQuestService>()
				.ExpireStaleAsync(profileId).ConfigureAwait(true);
			await ServiceHelper.Services.GetRequiredService<MuscleRankingService>()
				.RefreshAsync(profileId).ConfigureAwait(true);
		}
		catch
		{
			// Non bloquant : la home affiche les snapshots déjà persistés.
		}

		var snaps = await repo.ListGroupRankSnapshotsAsync(profileId, MuscleIds.StandardVersionId)
			.ConfigureAwait(true);
		var byId = snaps.ToDictionary(s => s.GroupId);

		var visuals = new List<BodyGroupVisual>(MuscleIds.Groups.Count);
		foreach (var group in MuscleIds.Groups)
		{
			byId.TryGetValue(group.Id, out var snap);
			var total = snap?.TotalCount > 0
				? snap.TotalCount
				: MuscleIds.Muscles.Count(m => m.GroupId == group.Id);
			visuals.Add(new BodyGroupVisual(
				group.Id,
				group.NameFr,
				snap?.ValidatedRank,
				snap?.EvaluatedCount ?? 0,
				total));
		}

		BodyMap.SetGroups(visuals);
		await BindQuestBadgeAsync(repo, profileId).ConfigureAwait(true);
	}

	private async Task BindQuestBadgeAsync(IRhythmoRepository repo, Guid profileId)
	{
		var ranks = await repo.ListProfileExerciseRanksAsync(profileId).ConfigureAwait(true);
		var attempts = await repo.ListQuestAttemptsAsync(profileId).ConfigureAwait(true);
		var today = DateOnly.FromDateTime(DateTime.Now);
		var count = ranks.Count(r =>
			r.AvailableQuestRank is not null &&
			!attempts.Any(a => a.ExerciseId == r.ExerciseId && a.LocalDate == today));
		QuestCountBadge.IsVisible = count > 0;
		QuestCountLabel.Text = count.ToString();
	}

	private async void OnGoQuestsClicked(object? sender, EventArgs e) =>
		await UiShellNavigate.GoAsync(nameof(RankQuestsPage)).ConfigureAwait(false);

	private async void OnGroupOpened(object? sender, string groupId) =>
		await RankUi.GoGroup(groupId).ConfigureAwait(false);
}
