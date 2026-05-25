using Microsoft.Extensions.DependencyInjection;
using Rhythmo.Mobile.Controls;
using Rhythmo.Mobile.Data;
using Rhythmo.Mobile.Infrastructure;
using Rhythmo.Mobile.Services;
using Rhythmo.Mobile.Social;

namespace Rhythmo.Mobile;

public partial class PrFeedPage : ContentPage
{
	private enum PrWindow
	{
		Day,
		AllTime
	}

	private sealed record FilterOption(Guid? Id, string Label)
	{
		public override string ToString() => Label;
	}

	private readonly IRhythmoRepository _repo = ServiceHelper.Services.GetRequiredService<IRhythmoRepository>();
	private readonly SocialHubService _hub = ServiceHelper.Services.GetRequiredService<SocialHubService>();
	private readonly IDevErrorPresenter _dev = ServiceHelper.Services.GetRequiredService<IDevErrorPresenter>();

	private List<PrFeedItemVm> _allPrs = [];
	private List<FilterOption> _userOptions = [];
	private List<FilterOption> _exerciseOptions = [];
	private PrWindow _window = PrWindow.Day;
	private bool _loaded;

	public PrFeedPage()
	{
		InitializeComponent();
		BackBtn.Clicked += (_, _) => _ = UiShellNavigate.GoAsync("..");
		PrRefresh.Refreshing += async (_, _) =>
		{
			await LoadAsync(forceReload: true).ConfigureAwait(true);
			PrRefresh.IsRefreshing = false;
		};
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		await LoadAsync(forceReload: !_loaded).ConfigureAwait(true);
	}

	private async Task LoadAsync(bool forceReload)
	{
		try
		{
			if (forceReload)
			{
				var meId = ServiceHelper.Services.GetRequiredService<ActiveProfileStore>().Get();
				var prTask = _hub.LoadAllPrFeedAsync(_repo, meId);
				var usersTask = _repo.ListCommunityProfilesAsync();
				var exercisesTask = _repo.ListExercisesAsync();

				await Task.WhenAll(prTask, usersTask, exercisesTask).ConfigureAwait(true);

				_allPrs = prTask.Result
					.OrderByDescending(p => p.CompletedUtc)
					.ToList();
				_userOptions = BuildUserOptions(usersTask.Result);
				_exerciseOptions = BuildExerciseOptions(exercisesTask.Result);

				UserFilterSelector.ItemsSource = _userOptions;
				ExerciseFilterSelector.ItemsSource = _exerciseOptions;
				if (UserFilterSelector.SelectedIndex < 0)
					UserFilterSelector.SelectedIndex = 0;
				if (ExerciseFilterSelector.SelectedIndex < 0)
					ExerciseFilterSelector.SelectedIndex = 0;

				_loaded = true;
			}

			RenderPeriodTabs();
			RenderFeed();
		}
		catch (Exception ex)
		{
			await _dev.TryShowSafeAsync(ex, nameof(LoadAsync)).ConfigureAwait(true);
		}
	}

	private void RenderPeriodTabs()
	{
		PeriodTabsHost.Children.Clear();
		PeriodTabsHost.Children.Add(FriendsHubUi.PeriodTab(
			"Aujourd'hui",
			_window == PrWindow.Day,
			() => SetWindow(PrWindow.Day)));
		PeriodTabsHost.Children.Add(FriendsHubUi.PeriodTab(
			"All-time",
			_window == PrWindow.AllTime,
			() => SetWindow(PrWindow.AllTime)));
	}

	private void RenderFeed()
	{
		var filtered = _allPrs.AsEnumerable();

		if (_window == PrWindow.Day)
		{
			var today = DateOnly.FromDateTime(DateTime.Now);
			filtered = filtered.Where(pr => DateOnly.FromDateTime(pr.CompletedUtc.ToLocalTime()) == today);
		}

		if (TryGetSelectedId(UserFilterSelector, _userOptions) is { } userId)
			filtered = filtered.Where(pr => pr.UserId == userId);

		if (TryGetSelectedId(ExerciseFilterSelector, _exerciseOptions) is { } exerciseId)
			filtered = filtered.Where(pr => pr.ExerciseId == exerciseId);

		var list = filtered
			.OrderByDescending(pr => pr.CompletedUtc)
			.ToList();

		PrFeedHost.Children.Clear();
		PrFeedHost.Children.Add(new Label
		{
			Text = $"{list.Count} PR affiché{(list.Count > 1 ? "s" : "")}",
			FontSize = 13,
			TextColor = Theme.RhythmColors.TextSecondary,
			Margin = new Thickness(2, 0, 0, 10)
		});

		if (list.Count == 0)
		{
			PrFeedHost.Children.Add(FriendsHubUi.EmptyHint("Aucun PR pour ce filtre."));
			return;
		}

		foreach (var pr in list)
			PrFeedHost.Children.Add(FriendsHubUi.PrCard(pr));
	}

	private void SetWindow(PrWindow window)
	{
		if (_window == window)
			return;

		_window = window;
		RenderPeriodTabs();
		RenderFeed();
	}

	private void OnUserFilterChanged(object? sender, EventArgs e) => RenderFeed();

	private void OnExerciseFilterChanged(object? sender, EventArgs e) => RenderFeed();

	private static List<FilterOption> BuildUserOptions(IReadOnlyList<ProfileRow> rows) =>
	[
		new FilterOption(null, "Utilisateur"),
		.. rows
			.OrderBy(r => r.DisplayName, StringComparer.OrdinalIgnoreCase)
			.Select(r => new FilterOption(r.Id, string.IsNullOrWhiteSpace(r.DisplayName) ? "Utilisateur" : r.DisplayName))
	];

	private static List<FilterOption> BuildExerciseOptions(IReadOnlyList<CachedExerciseRow> rows) =>
	[
		new FilterOption(null, "Exercice"),
		.. rows
			.OrderBy(r => r.NameFr, StringComparer.OrdinalIgnoreCase)
			.Select(r => new FilterOption(r.Id, string.IsNullOrWhiteSpace(r.NameFr) ? "Exercice" : r.NameFr))
	];

	private static Guid? TryGetSelectedId(RhythmOptionSelector selector, IReadOnlyList<FilterOption> options)
	{
		var ix = selector.SelectedIndex;
		if (ix < 0 || ix >= options.Count)
			return null;
		return options[ix].Id;
	}
}
