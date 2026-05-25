using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.ApplicationModel;
using Rhythmo.Mobile.Data;
using Rhythmo.Mobile.Infrastructure;
using Rhythmo.Mobile.Services;

namespace Rhythmo.Mobile;

public partial class SessionsPage : ContentPage
{
	public sealed record SessionCardVm(Guid Id, string Title, string ChipTags, string MetaLine);

	private readonly IDevErrorPresenter _dev =
		ServiceHelper.Services.GetRequiredService<IDevErrorPresenter>();

	private readonly List<SessionCardVm> _allCards = [];

	private Guid? _sheetSessionId;

	private bool _fabExpanded;

	public SessionsPage()
	{
		InitializeComponent();
		SessionsRefresh.Refreshing += async (_, _) =>
		{
			await ReloadAsync().ConfigureAwait(true);
			SessionsRefresh.IsRefreshing = false;
		};
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		await ReloadAsync().ConfigureAwait(true);
	}

	private async Task ReloadAsync()
	{
		try
		{
			var repo = ServiceHelper.Services.GetRequiredService<IRhythmoRepository>();
			var activeProfileId = ServiceHelper.Services.GetRequiredService<ActiveProfileStore>().Get();

			var tpls = await repo.ListSessionTemplatesAsync(activeProfileId).ConfigureAwait(true);
			var ids = tpls.Select(t => t.Id).ToList();
			var allExercises = await repo.ListExercisesAsync().ConfigureAwait(true);
			var cats = allExercises.ToDictionary(e => e.Id, e => e.Category);

			var sessionMeta = await Task.WhenAll(ids.Select(async id =>
			{
				var sessLines = await repo.ListSessionExercisesAsync(id).ConfigureAwait(false);
				var snap = await repo.GetSessionSnapshotAsync(id).ConfigureAwait(false);
				return (id, sessLines, snap?.Json);
			})).ConfigureAwait(false);

			await MainThread.InvokeOnMainThreadAsync(() =>
			{
				var lines = new List<SessionExerciseRow>();
				var countsBySession = new Dictionary<Guid, int>();
				var snaps = new Dictionary<Guid, string>();
				foreach (var (id, sessLines, snapJson) in sessionMeta)
				{
					lines.AddRange(sessLines);
					countsBySession[id] = sessLines.Count;
					if (snapJson is not null)
						snaps[id] = snapJson;
				}

				_allCards.Clear();
				foreach (var t in tpls)
				{
					var sessLines = lines.Where(l => l.SessionId == t.Id).ToList();
					var tags = sessLines
						.Select(l => cats.TryGetValue(l.ExerciseId, out var c) ? c : null)
						.Where(x => !string.IsNullOrWhiteSpace(x))
						.Distinct(StringComparer.OrdinalIgnoreCase)
						.Take(4)
						.Cast<string>()
						.ToList();

					var chipTags = tags.Count > 0 ? string.Join(" · ", tags) : "Mixte";

					countsBySession.TryGetValue(t.Id, out var exCount);
					var estMin = sessLines.Sum(l => l.TargetSets) * 2.5;
					var volKg = snaps.TryGetValue(t.Id, out var js)
						? WorkoutAnalytics.ComputeVolumeKgFromSessionSnapshot(js)
						: 0;
					var volStr = volKg > double.Epsilon
						? $"Vol. dernier · {(volKg >= 1000 ? $"{volKg / 1000d:0.#} t" : $"{volKg:0} kg")}"
						: "Pas encore de volume sur cette séance";

					var meta =
						$"{exCount} ex. · ~{Math.Max(1, (int)Math.Round(estMin))} min · {volStr} · MAJ {t.UpdatedUtc.ToLocalTime():d}";

					_allCards.Add(new SessionCardVm(t.Id, t.Title, chipTags, meta));
				}

				ApplySearchFilter();
			}).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			await _dev.TryShowSafeAsync(ex, nameof(ReloadAsync)).ConfigureAwait(false);
		}
	}

	private void ApplySearchFilter()
	{
		var q = (SessionSearchBar.Text ?? "").Trim();
		IEnumerable<SessionCardVm> rows = _allCards;
		if (q.Length > 0)
			rows = rows.Where(r =>
				r.Title.Contains(q, StringComparison.CurrentCultureIgnoreCase) ||
				r.ChipTags.Contains(q, StringComparison.CurrentCultureIgnoreCase));

		SessionsList.ItemsSource = rows.ToList();
		SessionsList.SelectedItem = null;
	}

	private void OnSessionSearchChanged(object? sender, TextChangedEventArgs e) => ApplySearchFilter();

	private void OnSessionSelectionChanged(object? sender, SelectionChangedEventArgs e)
	{
		if (SessionsList.SelectedItem is not SessionCardVm vm)
			return;

		_sheetSessionId = vm.Id;
		SheetTitleLabel.Text = vm.Title;
		SheetOverlay.IsVisible = true;
		SessionsList.SelectedItem = null;
	}

	private void OnSheetBackdropTapped(object? sender, EventArgs e) => CloseSheet();

	private void CloseSheet()
	{
		SheetOverlay.IsVisible = false;
		_sheetSessionId = null;
	}

	private void OnSheetClose(object? sender, EventArgs e) => CloseSheet();

	private void OnFabMainClicked(object? sender, EventArgs e)
	{
		_fabExpanded = !_fabExpanded;
		FabMenuPanel.IsVisible = _fabExpanded;
	}

	private async void OnFabNewSession(object? sender, EventArgs e)
	{
		_fabExpanded = false;
		FabMenuPanel.IsVisible = false;
		await UiShellNavigate.GoAsync($"{nameof(SessionEditPage)}").ConfigureAwait(false);
	}

	private async void OnFabQuickStart(object? sender, EventArgs e)
	{
		_fabExpanded = false;
		FabMenuPanel.IsVisible = false;
		try
		{
			var repo = ServiceHelper.Services.GetRequiredService<IRhythmoRepository>();
			var activeProfileId = ServiceHelper.Services.GetRequiredService<ActiveProfileStore>().Get();
			var latest = (await repo.ListSessionTemplatesAsync(activeProfileId).ConfigureAwait(true))
				.FirstOrDefault();
			if (latest is null)
			{
				await DisplayAlertAsync("Séances", "Crée d’abord une séance.", "OK").ConfigureAwait(false);
				return;
			}

			var cnt = await repo.CountSessionExercisesAsync(latest.Id).ConfigureAwait(true);
			if (cnt == 0)
			{
				await DisplayAlertAsync("Séances", "Cette séance est vide.", "OK").ConfigureAwait(false);
				return;
			}

			await UiShellNavigate
				.GoAsync($"{nameof(WorkoutRunnerPage)}?SessionId={Uri.EscapeDataString(latest.Id.ToString())}")
				.ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			await _dev.TryShowSafeAsync(ex, nameof(OnFabQuickStart)).ConfigureAwait(false);
		}
	}

	private async void OnFabOpenCatalog(object? sender, EventArgs e)
	{
		_fabExpanded = false;
		FabMenuPanel.IsVisible = false;
		await UiShellNavigate.GoAsync("//CatalogPage").ConfigureAwait(false);
	}

	private async void OnFabImport(object? sender, EventArgs e)
	{
		_fabExpanded = false;
		FabMenuPanel.IsVisible = false;
		await UiShellNavigate.GoAsync(nameof(ImportSessionsPage)).ConfigureAwait(false);
	}

	private async void OnSheetStart(object? sender, EventArgs e)
	{
		if (_sheetSessionId is not { } id)
			return;
		await UiShellNavigate.GoAsync($"{nameof(WorkoutRunnerPage)}?SessionId={Uri.EscapeDataString(id.ToString())}")
			.ConfigureAwait(false);
		await MainThread.InvokeOnMainThreadAsync(CloseSheet).ConfigureAwait(false);
	}

	private async void OnSheetEdit(object? sender, EventArgs e)
	{
		if (_sheetSessionId is not { } id)
			return;
		await UiShellNavigate.GoAsync($"{nameof(SessionEditPage)}?SessionId={Uri.EscapeDataString(id.ToString())}")
			.ConfigureAwait(false);
		await MainThread.InvokeOnMainThreadAsync(CloseSheet).ConfigureAwait(false);
	}

	private async void OnSheetDuplicate(object? sender, EventArgs e)
	{
		if (_sheetSessionId is not { } id)
			return;

		try
		{
			var repo = ServiceHelper.Services.GetRequiredService<IRhythmoRepository>();
			var active = ServiceHelper.Services.GetRequiredService<ActiveProfileStore>().Get();
			await repo.DuplicateSessionTemplateAsync(id, active).ConfigureAwait(true);
			CloseSheet();
			await ReloadAsync().ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			await _dev.TryShowSafeAsync(ex, nameof(OnSheetDuplicate)).ConfigureAwait(false);
		}
	}

	private async void OnSheetDelete(object? sender, EventArgs e)
	{
		if (_sheetSessionId is not { } id)
			return;

		var confirmed = await DisplayAlertAsync(
			"Supprimer la séance ?",
			"Tu perdras aussi la mémo locale « dernier run ».",
			"Supprimer",
			"Annuler").ConfigureAwait(true);

		if (!confirmed)
			return;

		try
		{
			var repo = ServiceHelper.Services.GetRequiredService<IRhythmoRepository>();
			var active = ServiceHelper.Services.GetRequiredService<ActiveProfileStore>().Get();
			var tpl = await repo.GetSessionTemplateAsync(id).ConfigureAwait(true);
			if (tpl is null || tpl.OwnerProfileId != active)
			{
				await DisplayAlertAsync("Séances", "Introuvable.", "OK").ConfigureAwait(false);
				return;
			}

			await repo.DeleteSessionTemplateAsync(id, active).ConfigureAwait(true);
			CloseSheet();
			await ReloadAsync().ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			await _dev.TryShowSafeAsync(ex, nameof(OnSheetDelete)).ConfigureAwait(false);
		}
	}
}
