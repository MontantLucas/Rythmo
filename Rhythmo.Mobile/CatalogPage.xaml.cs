using System.Collections;
using Microsoft.Extensions.DependencyInjection;
using Rhythmo.Mobile.Data;
using Rhythmo.Mobile.Services;

namespace Rhythmo.Mobile;

public partial class CatalogPage : ContentPage
{
	private readonly List<CachedExerciseRow> _all = [];

	private sealed class CatalogRowVm(Guid id, string category, string nameFr, double met)
	{
		public Guid Id { get; } = id;
		public string Category { get; } = category;
		public string NameFr { get; } = nameFr;
		public double MetApprox { get; } = met;

		public string MetaLine => "Charge métabolique indicative";

		public string MetFormatted => $"{MetApprox:N1}";
	}

	private readonly List<CatalogRowVm> _view = [];

	private string? _chipCategory;

	private bool _suppressCatalogSelector;
	private bool _catalogLoaded;

	public CatalogPage()
	{
		InitializeComponent();
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		if (_catalogLoaded)
			return;
		await ReloadDatabaseAsync(force: false).ConfigureAwait(false);
	}

	private async Task ReloadDatabaseAsync(bool force = true)
	{
		if (_catalogLoaded && !force)
			return;

		var repo = ServiceHelper.Services.GetRequiredService<IRhythmoRepository>();
		var list = await repo.ListExercisesAsync().ConfigureAwait(false);
		_all.Clear();
		_all.AddRange(list.OrderBy(e => e.Category).ThenBy(e => e.NameFr));
		_catalogLoaded = true;

		await MainThread.InvokeOnMainThreadAsync(() =>
		{
			RebuildCategoryPickerItems();
			ApplyFilter();
		}).ConfigureAwait(false);
	}

	private void RebuildCategoryPickerItems()
	{
		_suppressCatalogSelector = true;
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
			_suppressCatalogSelector = false;
		}
	}

	private void OnCatalogCategoryChanged(object? sender, EventArgs e)
	{
		if (_suppressCatalogSelector)
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

		ApplyFilter();
	}

	private void OnSearchChanged(object? sender, TextChangedEventArgs e) => ApplyFilter();

	private void ApplyFilter()
	{
		var q = (ExerciseSearchBar.Text ?? "").Trim();
		string? only = _chipCategory;

		_view.Clear();
		IEnumerable<CachedExerciseRow> rows = _all;
		if (only is not null)
			rows = rows.Where(r => string.Equals(r.Category, only, StringComparison.Ordinal));
		if (q.Length > 0)
			rows = rows.Where(r =>
				r.NameFr.Contains(q, StringComparison.CurrentCultureIgnoreCase) ||
				(r.Category ?? "").Contains(q, StringComparison.CurrentCultureIgnoreCase));

		foreach (var r in rows)
			_view.Add(new CatalogRowVm(r.Id, r.Category ?? "", r.NameFr, r.MetApprox));

		List.ItemsSource = null;
		List.ItemsSource = _view.ToList();
	}
}
