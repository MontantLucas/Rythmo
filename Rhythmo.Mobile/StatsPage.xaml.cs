using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Rhythmo.Mobile.Charting;
using Rhythmo.Mobile.Data;
using Rhythmo.Mobile.Infrastructure;
using Rhythmo.Mobile.Services;

namespace Rhythmo.Mobile;

public partial class StatsPage : ContentPage
{
	private sealed record HistRow(Guid Id, string SessionTitle, string SubLine);

	private const string CleanPickAll = "Tout vider";
	private const string CleanPickOrphans = "Vider uniquement les séances qui n’existent plus";

	private readonly IDevErrorPresenter _dev =
		ServiceHelper.Services.GetRequiredService<IDevErrorPresenter>();

	private readonly WeightProgressDrawable _progressDrawable = new();

	private int _tabIndex;
	private bool _suppressProgressExerciseSelector;
	private Guid? _stickyProgressExerciseId;
	private List<Guid> _progressExerciseIds = [];

	public StatsPage()
	{
		InitializeComponent();
		HistoryList.SelectionChanged += HistoryListOnSelectionChanged;
		ProgressExerciseSelector.SelectedIndexChanged += OnProgressExerciseSelected;
		ProgressChart.Drawable = _progressDrawable;

		StatsRefresh.Refreshing += async (_, _) =>
		{
			await ReloadAsync().ConfigureAwait(true);
			StatsRefresh.IsRefreshing = false;
		};
		ShowTab(0);
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		ShowTab(_tabIndex);
		await ReloadAsync().ConfigureAwait(true);
	}

	private static Style? LookupButtonStyle(string key)
	{
		var app = Application.Current;
		if (app?.Resources.TryGetValue(key, out var o) == true && o is Style sty)
			return sty;
		foreach (var md in app?.Resources.MergedDictionaries ?? Enumerable.Empty<ResourceDictionary>())
		{
			if (md.TryGetValue(key, out var o2) && o2 is Style sty2)
				return sty2;
		}

		return null;
	}

	private async Task ReloadAsync()
	{
		try
		{
			var repo = ServiceHelper.Services.GetRequiredService<IRhythmoRepository>();
			var profileId =
				ServiceHelper.Services.GetRequiredService<ActiveProfileStore>().Get();

			var hist = await repo.ListCompletedWorkoutsAsync(profileId).ConfigureAwait(false);

			var overview = await Task.Run(() =>
			{
				double volTotal = hist.Sum(w => WorkoutAnalytics.ComputeVolumeKgFromPayload(w.PayloadJson));
				var volText = volTotal >= 1000 ? $"{volTotal / 1000d:0.#} t" : $"{volTotal:0} kg";
				var kcal = $"{Math.Round(hist.Sum(w => w.CaloriesRounded))} kcal · cloud";
				var ui = hist.Select(c =>
					new HistRow(c.Id, c.SessionTitle, WorkoutHistoryFormatter.BuildListSubtitle(c))).ToList();
				return (
					VolText: volText,
					CountText: hist.Count.ToString(System.Globalization.CultureInfo.InvariantCulture),
					KcalText: kcal,
					Rows: ui);
			}).ConfigureAwait(false);

			await MainThread.InvokeOnMainThreadAsync(async () =>
			{
				OverviewVolumeLabel.Text = overview.VolText;
				OverviewCountLabel.Text = overview.CountText;
				OverviewKcalLabel.Text = overview.KcalText;
				HistoryList.ItemsSource = overview.Rows;
				HistoryList.SelectedItem = null;

				await LoadProgressPickerAsync(repo, profileId).ConfigureAwait(true);
			}).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			await _dev.TryShowSafeAsync(ex, nameof(ReloadAsync)).ConfigureAwait(false);
		}
	}

	private async void HistoryListOnSelectionChanged(object? sender, SelectionChangedEventArgs e)
	{
		if (e.CurrentSelection.FirstOrDefault() is not HistRow row)
			return;

		try
		{
			await MainThread.InvokeOnMainThreadAsync(async () =>
			{
				if (sender is CollectionView cv)
					cv.SelectedItem = null;

				await UiShellNavigate.GoAsync(
						$"{nameof(HistoryDetailPage)}?WorkoutId={Uri.EscapeDataString(row.Id.ToString())}")
					.ConfigureAwait(true);
			}).ConfigureAwait(true);
		}
		catch (Exception ex)
		{
			await _dev.TryShowSafeAsync(ex, nameof(HistoryListOnSelectionChanged)).ConfigureAwait(true);
		}
	}

	private void OnTabOverview(object? sender, EventArgs e) => ShowTab(0);

	private void OnTabSessions(object? sender, EventArgs e) => ShowTab(1);

	private void OnTabProgress(object? sender, EventArgs e) => ShowTab(2);

	private void ShowTab(int idx)
	{
		_tabIndex = idx;
		OverviewPanel.IsVisible = idx == 0;
		SessionsPanel.IsVisible = idx == 1;
		ProgressPanel.IsVisible = idx == 2;

		var secondary = LookupButtonStyle("RhythmBtnSecondary");
		var ghost = LookupButtonStyle("RhythmBtnGhost");
		if (secondary is null || ghost is null)
			return;

		TabOverviewBtn.Style = idx == 0 ? secondary : ghost;
		TabSessionsBtn.Style = idx == 1 ? secondary : ghost;
		TabProgressBtn.Style = idx == 2 ? secondary : ghost;

		if (idx == 2)
			InvalidateProgressChart();
	}

	/// <summary>
	/// Le GraphicsView du graphique ne doit pas être invalidé tant que l’onglet Progression n’est pas visible :
	/// sur WinUI le layout peut être 0×0 et le rendu plante.
	/// </summary>
	private void InvalidateProgressChart()
	{
		if (_tabIndex == 2 && ProgressPanel.IsVisible)
			ProgressChart.Invalidate();
	}

	private async void OnCleanHistoryClicked(object? sender, EventArgs e)
	{
		var pick = await DisplayActionSheetAsync(
			"Choisir un mode de nettoyage (historique local de ce profil).",
			"Annuler",
			null,
			CleanPickAll,
			CleanPickOrphans).ConfigureAwait(true);

		if (pick is null || pick == "Annuler")
			return;

		try
		{
			var repo = ServiceHelper.Services.GetRequiredService<IRhythmoRepository>();
			var profileId =
				ServiceHelper.Services.GetRequiredService<ActiveProfileStore>().Get();

			if (pick == CleanPickAll)
			{
				var confirm = await DisplayAlertAsync(
					"Tout supprimer ?",
					"Tous les résumés terminés et la courbe progression (points par jour) pour ce profil seront effacés. Irréversible.",
					"Supprimer tout",
					"Annuler").ConfigureAwait(true);
				if (!confirm)
					return;

				var rows = await repo.ListCompletedWorkoutsAsync(profileId).ConfigureAwait(true);
				await repo.DeleteAllPerformanceDailyAsync(profileId).ConfigureAwait(true);
				await repo.DeleteCompletedWorkoutsAsync(rows.Select(r => r.Id)).ConfigureAwait(true);

				await DisplayAlertAsync("Historique", $"{rows.Count} entrée(s) supprimée(s).", "OK")
					.ConfigureAwait(true);
			}
			else if (pick == CleanPickOrphans)
			{
				var confirm = await DisplayAlertAsync(
					"Nettoyer les entrées sans séance ?",
					"Seront retirées : les lignes faites après une séance supprimée, et les anciennes lignes sans séance reliée dont le titre ne correspond plus à aucune de tes séances.",
					"Supprimer ces lignes",
					"Annuler").ConfigureAwait(true);
				if (!confirm)
					return;

				var orphaned = await repo.GetOrphanedCompletedWorkoutsAsync(profileId).ConfigureAwait(true);
				if (orphaned.Count == 0)
				{
					await DisplayAlertAsync("Historique", "Aucune entrée à supprimer.", "OK")
						.ConfigureAwait(true);
					return;
				}

				await repo.DeleteCompletedWorkoutsAsync(orphaned.Select(o => o.Id)).ConfigureAwait(true);

				await DisplayAlertAsync("Historique", $"{orphaned.Count} entrée(s) supprimée(s).", "OK")
					.ConfigureAwait(true);
			}
		}
		catch (Exception ex)
		{
			await _dev.TryShowSafeAsync(ex, nameof(OnCleanHistoryClicked)).ConfigureAwait(false);
		}

		await ReloadAsync().ConfigureAwait(false);
	}

	private async void OnProgressExerciseSelected(object? sender, EventArgs e)
	{
		if (_suppressProgressExerciseSelector)
			return;

		var ix = ProgressExerciseSelector.SelectedIndex;
		_stickyProgressExerciseId = ix >= 0 && ix < _progressExerciseIds.Count
			? _progressExerciseIds[ix]
			: null;

		try
		{
			var repo = ServiceHelper.Services.GetRequiredService<IRhythmoRepository>();
			await RefreshProgressChartAsync(
					repo,
					ServiceHelper.Services.GetRequiredService<ActiveProfileStore>().Get())
				.ConfigureAwait(true);
		}
		catch (Exception ex)
		{
			await _dev.TryShowSafeAsync(ex, nameof(OnProgressExerciseSelected)).ConfigureAwait(true);
		}
	}

	private async Task LoadProgressPickerAsync(IRhythmoRepository repo, Guid profileId)
	{
		var exercisedIds = await repo.ListExercisesWithPerformanceAsync(profileId).ConfigureAwait(true);
		var all = await repo.ListExercisesAsync().ConfigureAwait(true);
		var progressList = all
			.Where(c => exercisedIds.Contains(c.Id))
			.OrderBy(c => c.NameFr)
			.Select(c => (Id: c.Id, Name: string.IsNullOrWhiteSpace(c.NameFr) ? "—" : c.NameFr))
			.ToList();

		_progressExerciseIds = progressList.Select(p => p.Id).ToList();
		var names = progressList.Select(p => p.Name).ToList();

		_suppressProgressExerciseSelector = true;
		try
		{
			ProgressExerciseSelector.ItemsSource = names;

			var want = _stickyProgressExerciseId;
			if (want.HasValue)
			{
				var ix = _progressExerciseIds.IndexOf(want.Value);
				ProgressExerciseSelector.SelectedIndex = ix >= 0 ? ix : names.Count > 0 ? 0 : -1;
			}
			else
				ProgressExerciseSelector.SelectedIndex = names.Count > 0 ? 0 : -1;

			var selIx = ProgressExerciseSelector.SelectedIndex;
			_stickyProgressExerciseId = selIx >= 0 && selIx < _progressExerciseIds.Count
				? _progressExerciseIds[selIx]
				: null;
		}
		finally
		{
			_suppressProgressExerciseSelector = false;
		}

		await RefreshProgressChartAsync(repo, profileId).ConfigureAwait(true);
	}

	private async Task RefreshProgressChartAsync(IRhythmoRepository repo, Guid profileId)
	{
		var ix = ProgressExerciseSelector.SelectedIndex;
		if (ix < 0 || ix >= _progressExerciseIds.Count)
		{
			_progressDrawable.SetSeries([]);
			InvalidateProgressChart();
			return;
		}

		var pts = await repo.ListPerformanceDailyAsync(profileId, _progressExerciseIds[ix]).ConfigureAwait(true);
		var list = pts.Select(static x => (x.PerformanceDate, x.MaxWeightKg)).ToList();
		_progressDrawable.SetSeries(list);
		InvalidateProgressChart();
	}
}
