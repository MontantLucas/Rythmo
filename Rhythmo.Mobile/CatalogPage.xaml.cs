using System.Collections;
using Microsoft.Extensions.DependencyInjection;
using Rhythmo.Mobile.Data;
using Rhythmo.Mobile.Infrastructure;
using Rhythmo.Mobile.Services;
using Rhythmo.Mobile.Theme;
using Rhythmo.Shared.Ranking;

namespace Rhythmo.Mobile;

public partial class CatalogPage : ContentPage
{
	private readonly List<CachedExerciseRow> _all = [];
	private readonly Dictionary<Guid, ExerciseRankingDef> _defs = [];
	private readonly Dictionary<Guid, int?> _userRanks = [];

	private sealed class CatalogFilterOption(string? id, string label)
	{
		public string? Id { get; } = id;
		public override string ToString() => label;
	}

	private sealed class CatalogRowVm
	{
		public Guid Id { get; init; }
		public string Category { get; init; } = "";
		public string NameFr { get; init; } = "";
		public string MetFormatted { get; init; } = "";
		public bool HasPrimary { get; init; }
		public bool HasSecondary { get; init; }
		public bool ShowNonClassifying { get; init; }
		public IReadOnlyList<string> PrimaryMuscles { get; init; } = [];
		public IReadOnlyList<string> SecondaryMuscles { get; init; } = [];
		public bool HasUserRank { get; init; }
		public string RankText { get; init; } = "";
		public Color RankColor { get; init; } = RankPalette.Unevaluated;
	}

	private readonly List<CatalogRowVm> _view = [];

	private string? _chipCategory;
	private string? _chipMuscleId;
	private bool _suppressSelectors;
	private bool _catalogLoaded;

	public CatalogPage()
	{
		InitializeComponent();
	}

	public void SetEmbedded(bool embedded)
	{
		CatalogIntro.IsVisible = !embedded;
		if (Content is Grid grid)
			grid.Padding = embedded
				? new Thickness(0, 4, 0, 0)
				: new Thickness(20, 16);
	}

	public async Task AppearAsync()
	{
		if (_catalogLoaded)
		{
			await RefreshRanksAsync().ConfigureAwait(false);
			return;
		}

		await ReloadDatabaseAsync().ConfigureAwait(false);
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		await AppearAsync().ConfigureAwait(false);
	}

	private async Task ReloadDatabaseAsync()
	{
		var repo = ServiceHelper.Services.GetRequiredService<IRhythmoRepository>();
		var list = await repo.ListExercisesAsync().ConfigureAwait(false);
		_all.Clear();
		_all.AddRange(list.OrderBy(e => e.Category).ThenBy(e => e.NameFr));
		_defs.Clear();
		foreach (var pair in RankingExerciseIndex.Map(_all))
			_defs[pair.Key] = pair.Value;
		_catalogLoaded = true;
		await LoadRanksAsync(repo).ConfigureAwait(false);

		await MainThread.InvokeOnMainThreadAsync(() =>
		{
			RebuildCategoryPickerItems();
			RebuildSubcategoryPickerItems();
			ApplyFilter();
		}).ConfigureAwait(false);
	}

	private async Task RefreshRanksAsync()
	{
		var repo = ServiceHelper.Services.GetRequiredService<IRhythmoRepository>();
		await LoadRanksAsync(repo).ConfigureAwait(false);
		await MainThread.InvokeOnMainThreadAsync(ApplyFilter).ConfigureAwait(false);
	}

	private async Task LoadRanksAsync(IRhythmoRepository repo)
	{
		_userRanks.Clear();
		try
		{
			var profileId = ServiceHelper.Services.GetRequiredService<ActiveProfileStore>().Get();
			var ranks = await repo.ListProfileExerciseRanksAsync(profileId).ConfigureAwait(false);
			foreach (var r in ranks)
				_userRanks[r.ExerciseId] = r.ValidatedRank;
		}
		catch
		{
			// Le rang n’est qu’un indicateur secondaire.
		}
	}

	private void RebuildCategoryPickerItems()
	{
		_suppressSelectors = true;
		try
		{
			var items = new List<string> { "Toutes les catégories" };
			items.AddRange(LocalExerciseSeed.CategoryOrder.Where(cat =>
				_all.Exists(e => string.Equals(e.Category, cat, StringComparison.Ordinal))));

			CatalogCategorySelector.ItemsSource = items;

			if (_chipCategory is { } preserved && items.Contains(preserved))
				CatalogCategorySelector.SelectedIndex = items.IndexOf(preserved);
			else
			{
				_chipCategory = null;
				CatalogCategorySelector.SelectedIndex = 0;
			}
		}
		finally
		{
			_suppressSelectors = false;
		}
	}

	private void RebuildSubcategoryPickerItems()
	{
		_suppressSelectors = true;
		try
		{
			var muscleIds = CollectMuscleIdsForCategory(_chipCategory);
			var items = new List<CatalogFilterOption>
			{
				new(null, "Toutes les sous-catégories")
			};
			foreach (var muscle in MuscleIds.Muscles.Where(m => muscleIds.Contains(m.Id)))
				items.Add(new CatalogFilterOption(muscle.Id, muscle.NameFr));

			CatalogSubcategorySelector.ItemsSource = items;

			var keep = items.FindIndex(o => o.Id == _chipMuscleId);
			if (keep <= 0)
			{
				_chipMuscleId = null;
				CatalogSubcategorySelector.SelectedIndex = 0;
			}
			else
				CatalogSubcategorySelector.SelectedIndex = keep;
		}
		finally
		{
			_suppressSelectors = false;
		}
	}

	private HashSet<string> CollectMuscleIdsForCategory(string? category)
	{
		var ids = new HashSet<string>(StringComparer.Ordinal);
		foreach (var ex in _all)
		{
			if (category is not null &&
			    !string.Equals(ex.Category, category, StringComparison.Ordinal))
				continue;
			if (!_defs.TryGetValue(ex.Id, out var def))
				continue;
			foreach (var id in def.PrimaryMuscles)
				ids.Add(id);
			foreach (var id in def.SecondaryMuscles)
				ids.Add(id);
		}

		return ids;
	}

	private void OnCatalogCategoryChanged(object? sender, EventArgs e)
	{
		if (_suppressSelectors)
			return;

		if (CatalogCategorySelector.SelectedIndex < 0)
			return;

		if (CatalogCategorySelector.SelectedIndex == 0)
			_chipCategory = null;
		else if (CatalogCategorySelector.ItemsSource is IList list &&
		         CatalogCategorySelector.SelectedIndex < list.Count &&
		         list[CatalogCategorySelector.SelectedIndex] is string s)
			_chipCategory = s;
		else
			_chipCategory = null;

		RebuildSubcategoryPickerItems();
		ApplyFilter();
	}

	private void OnCatalogSubcategoryChanged(object? sender, EventArgs e)
	{
		if (_suppressSelectors)
			return;

		if (CatalogSubcategorySelector.SelectedItem is CatalogFilterOption opt)
			_chipMuscleId = opt.Id;
		else
			_chipMuscleId = null;

		ApplyFilter();
	}

	private void OnSearchChanged(object? sender, TextChangedEventArgs e) => ApplyFilter();

	private void ApplyFilter()
	{
		var q = (ExerciseSearchBar.Text ?? "").Trim();
		string? only = _chipCategory;
		string? muscle = _chipMuscleId;

		_view.Clear();
		IEnumerable<CachedExerciseRow> rows = _all;
		if (only is not null)
			rows = rows.Where(r => string.Equals(r.Category, only, StringComparison.Ordinal));
		if (q.Length > 0)
			rows = rows.Where(r =>
				r.NameFr.Contains(q, StringComparison.CurrentCultureIgnoreCase) ||
				(r.Category ?? "").Contains(q, StringComparison.CurrentCultureIgnoreCase) ||
				MatchesMuscleName(r, q));
		if (muscle is not null)
			rows = rows.Where(r => HasMuscle(r.Id, muscle));

		foreach (var r in rows)
			_view.Add(ToVm(r));

		List.ItemsSource = null;
		List.ItemsSource = _view.ToList();
	}

	private bool MatchesMuscleName(CachedExerciseRow row, string q)
	{
		if (!_defs.TryGetValue(row.Id, out var def))
			return false;
		return NamesOf(def.PrimaryMuscles).Any(n => n.Contains(q, StringComparison.CurrentCultureIgnoreCase)) ||
		       NamesOf(def.SecondaryMuscles).Any(n => n.Contains(q, StringComparison.CurrentCultureIgnoreCase));
	}

	private bool HasMuscle(Guid exerciseId, string muscleId)
	{
		if (!_defs.TryGetValue(exerciseId, out var def))
			return false;
		return def.PrimaryMuscles.Contains(muscleId) || def.SecondaryMuscles.Contains(muscleId);
	}

	private CatalogRowVm ToVm(CachedExerciseRow r)
	{
		_defs.TryGetValue(r.Id, out var def);
		var primary = NamesOf(def?.PrimaryMuscles);
		var secondary = NamesOf(def?.SecondaryMuscles);
		_userRanks.TryGetValue(r.Id, out var rank);
		var hasRank = rank is >= 1 and <= 10;
		return new CatalogRowVm
		{
			Id = r.Id,
			Category = r.Category ?? "",
			NameFr = r.NameFr,
			MetFormatted = $"{r.MetApprox:N1}",
			HasPrimary = primary.Count > 0,
			HasSecondary = secondary.Count > 0,
			ShowNonClassifying = primary.Count == 0,
			PrimaryMuscles = primary,
			SecondaryMuscles = secondary,
			HasUserRank = hasRank,
			RankText = hasRank ? RankLabels.Code(rank) : "",
			RankColor = RankPalette.For(rank)
		};
	}

	private static List<string> NamesOf(IReadOnlyList<string>? ids)
	{
		if (ids is null || ids.Count == 0)
			return [];
		return MuscleIds.Muscles
			.Where(m => ids.Contains(m.Id))
			.Select(m => m.NameFr)
			.ToList();
	}

	private async void OnExerciseCardTapped(object? sender, TappedEventArgs e)
	{
		if ((sender as BindableObject)?.BindingContext is not CatalogRowVm vm)
			return;
		await RankUi.GoExercise(vm.Id).ConfigureAwait(false);
	}

	private async void OnStartAdHocClicked(object? sender, EventArgs e)
	{
		try
		{
			await AdHocWorkout.StartOrResumeAsync(this).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			await ServiceHelper.Services.GetRequiredService<IDevErrorPresenter>()
				.TryShowSafeAsync(ex, nameof(OnStartAdHocClicked)).ConfigureAwait(false);
		}
	}

	private async void OnStartFromExerciseClicked(object? sender, EventArgs e)
	{
		if ((sender as BindableObject)?.BindingContext is not CatalogRowVm vm)
			return;

		try
		{
			await AdHocWorkout.StartOrResumeAsync(this, vm.Id).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			await ServiceHelper.Services.GetRequiredService<IDevErrorPresenter>()
				.TryShowSafeAsync(ex, nameof(OnStartFromExerciseClicked)).ConfigureAwait(false);
		}
	}
}
