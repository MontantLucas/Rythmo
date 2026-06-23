using System.Globalization;
using System.Collections;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls.Shapes;
using Rhythmo.Mobile.Data;
using Rhythmo.Mobile.Infrastructure;
using Rhythmo.Mobile.Services;
using Rhythmo.Mobile.Theme;

namespace Rhythmo.Mobile;

[QueryProperty(nameof(SessionIdEncoded), "SessionId")]
public partial class SessionEditPage : ContentPage
{
	private sealed record SessionExerciseLineDraft(Guid LineId, Guid ExerciseId);

	private readonly IDevErrorPresenter _dev =
		ServiceHelper.Services.GetRequiredService<IDevErrorPresenter>();

	private readonly List<SessionExerciseLineDraft> _orderedLines = [];
	private readonly Dictionary<Guid, (int Sets, int Reps)> _targetsByLineId = [];
	private readonly Dictionary<Guid, (Entry SetsEntry, Entry RepsEntry)> _entryBindings = new();

	private readonly Dictionary<Guid, CachedExerciseRow> _exercisesById = new();

	private string? _addCatalogFilterCategory;

	private CancellationTokenSource? _catalogSearchDebounceCts;

	private Guid? _existingSessionId;
	private Guid? _actionTargetLineId;

	private bool _suppressAddSheetCategorySelector;

	private const string CardAutomationHint = "session-ex-card";

	public string? SessionIdEncoded
	{
		set =>
			_existingSessionId = string.IsNullOrWhiteSpace(value)
				? null
				: Guid.Parse(Uri.UnescapeDataString(value));
	}

	public SessionEditPage()
	{
		InitializeComponent();
		SeedAddSheetCategoryPicker();
		BackBtn.Clicked += (_, _) => _ = UiShellNavigate.GoAsync("..");
		FinishBtn.Clicked += async (_, _) =>
			await PersistSessionAsync(navigateAfter: true).ConfigureAwait(false);
		AddSheetSearchBar.TextChanged += OnAddSheetSearchChanged;
	}

	private void OnOpenAddSheetClicked(object? sender, EventArgs e) => OpenAddExerciseSheet();

	private void OnEmptyAddClicked(object? sender, EventArgs e) => OpenAddExerciseSheet();

	private async void OpenAddExerciseSheet()
	{
		CaptureTargetsFromBindings();
		AddSheetOverlay.IsVisible = true;
		AddSheetOverlay.InputTransparent = false;
		AddSheetPanel.Opacity = 0;
		AddSheetPanel.TranslationY = 96;
		RefreshAddSheetCatalog();

		await AddSheetPanel.FadeToAsync(1, 160, Easing.CubicOut);
		await AddSheetPanel.TranslateToAsync(0, 0, 220, Easing.CubicOut);
	}

	private void OnCloseAddSheetClicked(object? sender, EventArgs e) =>
		_ = CloseAddExerciseSheetAsync();

	private void OnAddSheetBackdropTapped(object? sender, EventArgs e) =>
		_ = CloseAddExerciseSheetAsync();

	private async Task CloseAddExerciseSheetAsync()
	{
		AddSheetCategorySelector.CloseDropdown();
		await AddSheetPanel.TranslateToAsync(0, 80, 140, Easing.CubicIn);
		await AddSheetPanel.FadeToAsync(0, 100, Easing.CubicIn);
		AddSheetOverlay.IsVisible = false;
		AddSheetOverlay.InputTransparent = true;
		AddSheetPanel.TranslationY = 0;
		AddSheetPanel.Opacity = 1;
		RenderExerciseList();
	}

	private void SeedAddSheetCategoryPicker()
	{
		_suppressAddSheetCategorySelector = true;
		try
		{
			var list = new List<string>(1 + LocalExerciseSeed.CategoryOrder.Count) { "Tous les groupes" };
			list.AddRange(LocalExerciseSeed.CategoryOrder);
			AddSheetCategorySelector.ItemsSource = list;

			var idx = 0;
			if (_addCatalogFilterCategory is { } cat)
			{
				var ix = list.IndexOf(cat);
				if (ix > 0)
					idx = ix;
				else
					_addCatalogFilterCategory = null;
			}

			AddSheetCategorySelector.SelectedIndex = idx;
		}
		finally
		{
			_suppressAddSheetCategorySelector = false;
		}
	}

	private void OnAddSheetCategoryPickerChanged(object? sender, EventArgs e)
	{
		if (_suppressAddSheetCategorySelector || AddSheetCategorySelector.SelectedIndex < 0)
			return;

		SyncAddSheetFilterFromPicker();
		if (AddSheetOverlay.IsVisible)
			RefreshAddSheetCatalog();
	}

	private void SyncAddSheetFilterFromPicker()
	{
		if (AddSheetCategorySelector.SelectedIndex <= 0)
		{
			_addCatalogFilterCategory = null;
			return;
		}

		var ix = AddSheetCategorySelector.SelectedIndex;
		if (AddSheetCategorySelector.ItemsSource is IList list && ix >= 0 && ix < list.Count &&
		    list[ix] is string s)
			_addCatalogFilterCategory = s;
	}

	private void OnAddSheetSearchChanged(object? sender, TextChangedEventArgs e)
	{
		_catalogSearchDebounceCts?.Cancel();
		_catalogSearchDebounceCts?.Dispose();
		var cts = new CancellationTokenSource();
		_catalogSearchDebounceCts = cts;
		_ = DebouncedAddSheetRefreshAsync(cts.Token);
	}

	private async Task DebouncedAddSheetRefreshAsync(CancellationToken token)
	{
		try
		{
			await Task.Delay(220, token).ConfigureAwait(false);
			await MainThread.InvokeOnMainThreadAsync(RefreshAddSheetCatalog).ConfigureAwait(false);
		}
		catch (OperationCanceledException)
		{
			// ignore
		}
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		await RebuildExerciseTogglesAsync().ConfigureAwait(true);
	}

	protected override void OnDisappearing()
	{
		_catalogSearchDebounceCts?.Cancel();
		_catalogSearchDebounceCts?.Dispose();
		_catalogSearchDebounceCts = null;
		base.OnDisappearing();
	}

	private async Task RebuildExerciseTogglesAsync()
	{
		try
		{
			_orderedLines.Clear();
			_targetsByLineId.Clear();
			_exercisesById.Clear();
			_entryBindings.Clear();

			var repo = ServiceHelper.Services.GetRequiredService<IRhythmoRepository>();
			var exercises = (await repo.ListExercisesAsync().ConfigureAwait(true))
				.OrderBy(e => e.NameFr)
				.ToList();

			foreach (var ex in exercises)
				_exercisesById[ex.Id] = ex;

			if (_existingSessionId is { } sid)
			{
				var tpl = await repo.GetSessionTemplateAsync(sid).ConfigureAwait(true);
				if (tpl is null)
				{
					await DisplayAlertAsync("Séance", "Introuvable.", "OK").ConfigureAwait(false);
					await UiShellNavigate.GoAsync("..").ConfigureAwait(false);
					return;
				}

				var profileId = ServiceHelper.Services.GetRequiredService<ActiveProfileStore>().Get();
				if (tpl.OwnerProfileId != profileId)
				{
					await DisplayAlertAsync("Séances", "Cette séance n’est pas accessible.", "OK")
						.ConfigureAwait(false);
					await UiShellNavigate.GoAsync("..").ConfigureAwait(false);
					return;
				}

				TitleEntry.Text = tpl.Title;

				var lines = await repo.ListSessionExercisesAsync(sid).ConfigureAwait(true);

				foreach (var ln in lines)
				{
					var lid = Guid.NewGuid();
					_orderedLines.Add(new SessionExerciseLineDraft(lid, ln.ExerciseId));
					_targetsByLineId[lid] = (
						Math.Clamp(ln.TargetSets, 1, 99),
						Math.Clamp(ln.TargetReps ?? 10, 1, 999));
				}
			}
			else
				TitleEntry.Text = $"Séance {DateTime.Now:yyyy-MM-dd}";

			RenderExerciseList();
			if (AddSheetOverlay.IsVisible)
				RefreshAddSheetCatalog();
		}
		catch (Exception ex)
		{
			await _dev.TryShowSafeAsync(ex, nameof(RebuildExerciseTogglesAsync)).ConfigureAwait(false);
		}
	}

	private void CaptureTargetsFromBindings()
	{
		foreach (var (lid, (setsE, repsE)) in _entryBindings)
		{
			var sets = ParseInt(setsE.Text,
				fallback: _targetsByLineId.TryGetValue(lid, out var t) ? t.Sets : 3,
				min: 1, max: 99);
			var reps = ParseInt(repsE.Text,
				fallback: _targetsByLineId.TryGetValue(lid, out var t2) ? t2.Reps : 10,
				min: 1, max: 999);
			_targetsByLineId[lid] = (sets, reps);
		}

		static int ParseInt(string? text, int fallback, int min, int max)
		{
			if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v))
				return Math.Clamp(v, min, max);
			return Math.Clamp(fallback, min, max);
		}
	}

	private bool ExercisePassesAddFilters(CachedExerciseRow ex)
	{
		if (_addCatalogFilterCategory is { } onlyCat &&
		    !string.Equals(ex.Category, onlyCat, StringComparison.Ordinal))
			return false;

		var raw = AddSheetSearchBar.Text;
		if (string.IsNullOrWhiteSpace(raw))
			return true;

		var q = raw.Trim();
		if (ex.NameFr.Contains(q, StringComparison.CurrentCultureIgnoreCase))
			return true;

		return !string.IsNullOrEmpty(ex.Category) &&
		       ex.Category.Contains(q, StringComparison.CurrentCultureIgnoreCase);
	}

	private void RefreshAddSheetCatalog()
	{
		AddCatalogHost.Children.Clear();

		foreach (var kv in _exercisesById.OrderBy(x => x.Value.NameFr, StringComparer.CurrentCultureIgnoreCase))
		{
			if (!ExercisePassesAddFilters(kv.Value))
				continue;

			var exercise = kv.Value;
			var exerciseId = kv.Key;

			var glyph = SessionMuscleGlyph.FromCategory(exercise.Category);

			var addBtn = new Button
			{
				Text = "+",
				FontSize = 22,
				FontAttributes = FontAttributes.Bold,
				TextColor = RhythmColors.Bg,
				BackgroundColor = RhythmColors.Accent,
				WidthRequest = 42,
				HeightRequest = 42,
				CornerRadius = 21,
				Padding = 0
			};
			SemanticProperties.SetDescription(addBtn, "Ajouter cet exercice");
			addBtn.Clicked += async (_, _) =>
			{
				if (!addBtn.IsEnabled)
					return;

				addBtn.IsEnabled = false;
				addBtn.BackgroundColor = RhythmColors.Success;
				addBtn.TextColor = Colors.White;
				addBtn.Text = "\u2713";
				addBtn.FontSize = 20;
				SemanticProperties.SetDescription(addBtn, "Exercice ajouté à la séance");

				CaptureTargetsFromBindings();
				var lid = Guid.NewGuid();
				_orderedLines.Add(new SessionExerciseLineDraft(lid, exerciseId));
				_targetsByLineId[lid] = (3, 10);
				RenderExerciseList();

				await addBtn.ScaleToAsync(1.12, 100, Easing.CubicOut).ConfigureAwait(true);
				await addBtn.ScaleToAsync(1, 150, Easing.SpringOut).ConfigureAwait(true);

				await Task.Delay(380).ConfigureAwait(true);

				await MainThread.InvokeOnMainThreadAsync(() =>
				{
					RefreshAddSheetCatalog();
				}).ConfigureAwait(true);
			};

			var row = BuildCatalogRow(glyph, exercise, addBtn);
			var wrap = new Border
			{
				Padding = new Thickness(12, 8),
				BackgroundColor = RhythmColors.Surface1,
				StrokeThickness = 0,
				HorizontalOptions = LayoutOptions.Fill,
				Margin = new Thickness(0, 3)
			};
			wrap.StrokeShape = new RoundRectangle { CornerRadius = 16 };
			wrap.Content = row;
			AddCatalogHost.Children.Add(wrap);
		}
	}

	private Grid BuildCatalogRow(string glyphChar, CachedExerciseRow ex, View trailing)
	{
		var circle = MuscleIconCircle(glyphChar, 42);

		var name = new Label
		{
			Text = ex.NameFr,
			FontFamily = "OpenSansSemibold",
			FontSize = 15,
			TextColor = RhythmColors.TextPrimary,
			LineBreakMode = LineBreakMode.TailTruncation,
			MaxLines = 1
		};
		var muscle = new Label
		{
			Text = ex.Category ?? "—",
			FontSize = 13,
			TextColor = RhythmColors.TextSecondary,
			LineBreakMode = LineBreakMode.TailTruncation,
			MaxLines = 1
		};
		var met = new Label
		{
			Text = $"MET≈ {ex.MetApprox:F1}",
			FontSize = 12,
			TextColor = RhythmColors.TextSecondary
		};

		var textCol = new VerticalStackLayout { Spacing = 2 };
		textCol.Children.Add(name);
		textCol.Children.Add(muscle);
		textCol.Children.Add(met);

		var grid = new Grid
		{
			ColumnDefinitions =
			[
				new ColumnDefinition(GridLength.Auto),
				new ColumnDefinition(GridLength.Star),
				new ColumnDefinition(GridLength.Auto)
			],
			ColumnSpacing = 14,
			HorizontalOptions = LayoutOptions.Fill
		};
		grid.Add(circle, 0, 0);
		grid.Add(textCol, 1, 0);
		grid.Add(trailing, 2, 0);
		grid.VerticalOptions = LayoutOptions.Center;
		return grid;
	}

	private static Border MuscleIconCircle(string glyph, double diameter)
	{
		var lbl = new Label
		{
			Text = glyph,
			HorizontalOptions = LayoutOptions.Center,
			VerticalOptions = LayoutOptions.Center,
			TextColor = RhythmColors.Accent,
			FontSize = Math.Clamp(diameter * 0.52, 16, 30),
			FontFamily = SessionMuscleGlyph.FontAlias,
			HorizontalTextAlignment = TextAlignment.Center,
			VerticalTextAlignment = TextAlignment.Center,
			LineHeight = 1
		};
		var b = new Border
		{
			BackgroundColor = RhythmColors.Surface2,
			StrokeThickness = 0,
			HeightRequest = diameter,
			WidthRequest = diameter,
			HorizontalOptions = LayoutOptions.Start,
			VerticalOptions = LayoutOptions.Center,
			Content = lbl
		};
		b.StrokeShape = new RoundRectangle { CornerRadius = (float)(diameter / 2) };
		return b;
	}

	private void RenderExerciseList()
	{
		if (!MainThread.IsMainThread)
		{
			MainThread.BeginInvokeOnMainThread(RenderExerciseList);
			return;
		}

		CaptureTargetsFromBindings();
		OrderedHost.Children.Clear();
		_entryBindings.Clear();

		var n = _orderedLines.Count;
		ExerciseCountBadge.Text = n switch
		{
			0 => "0 exercice",
			1 => "1 exercice",
			_ => $"{n} exercices",
		};

		var showList = n > 0;
		EmptySessionHint.IsVisible = !showList;
		OrderedHost.IsVisible = showList;
		ExerciseListScroll.IsVisible = showList;

		foreach (var line in _orderedLines.ToList())
		{
			if (!_exercisesById.TryGetValue(line.ExerciseId, out var ex))
			{
				_orderedLines.RemoveAll(l => l.LineId == line.LineId);
				_targetsByLineId.Remove(line.LineId);
				continue;
			}

			_targetsByLineId.TryGetValue(line.LineId, out var tup);
			if (tup == default)
				tup = (3, 10);

			var setsEntry = new Entry
			{
				Text = tup.Sets.ToString(CultureInfo.InvariantCulture),
				Keyboard = Keyboard.Numeric,
				BackgroundColor = RhythmColors.Surface2,
				TextColor = RhythmColors.TextPrimary,
				WidthRequest = 38,
				HeightRequest = 28,
				FontSize = 13,
				HorizontalTextAlignment = TextAlignment.Center,
				MaxLength = 2,
				VerticalOptions = LayoutOptions.Center
			};
			var repsEntry = new Entry
			{
				Text = tup.Reps.ToString(CultureInfo.InvariantCulture),
				Keyboard = Keyboard.Numeric,
				BackgroundColor = RhythmColors.Surface2,
				TextColor = RhythmColors.TextPrimary,
				WidthRequest = 44,
				HeightRequest = 28,
				FontSize = 13,
				HorizontalTextAlignment = TextAlignment.Center,
				MaxLength = 3,
				VerticalOptions = LayoutOptions.Center
			};
			_entryBindings[line.LineId] = (setsEntry, repsEntry);

			var nameLbl = new Label
			{
				Text = ex.NameFr,
				FontFamily = "OpenSansSemibold",
				FontSize = 16,
				TextColor = RhythmColors.TextPrimary,
				LineBreakMode = LineBreakMode.TailTruncation,
				MaxLines = 1,
				LineHeight = 1.05
			};
			var muscleMetaLbl = new Label
			{
				FormattedText = new FormattedString
				{
					Spans =
					{
						new Span
						{
							Text = ex.Category ?? "—",
							FontSize = 13,
							TextColor = RhythmColors.TextSecondary
						},
						new Span { Text = " · ", FontSize = 12, TextColor = RhythmColors.TextSecondary },
						new Span
						{
							Text = $"MET≈ {ex.MetApprox:F1}",
							FontSize = 12,
							TextColor = RhythmColors.TextSecondary
						}
					}
				},
				LineBreakMode = LineBreakMode.TailTruncation,
				MaxLines = 1,
				LineHeight = 1.05
			};

			var specsRow = new HorizontalStackLayout { Spacing = 6, Margin = new Thickness(0, 2, 0, 0) };
			specsRow.Children.Add(new Label
			{
				Text = "Séries",
				VerticalTextAlignment = TextAlignment.Center,
				FontSize = 11,
				TextColor = RhythmColors.TextSecondary
			});
			specsRow.Children.Add(setsEntry);
			specsRow.Children.Add(new Label
			{
				Text = "Reps",
				Margin = new Thickness(8, 0, 0, 0),
				VerticalTextAlignment = TextAlignment.Center,
				FontSize = 11,
				TextColor = RhythmColors.TextSecondary
			});
			specsRow.Children.Add(repsEntry);

			var mid = new VerticalStackLayout { Spacing = 1 };
			mid.Children.Add(nameLbl);
			mid.Children.Add(muscleMetaLbl);
			mid.Children.Add(specsRow);
			mid.VerticalOptions = LayoutOptions.Center;

			var lineIdCaptured = line.LineId;
			var lineIndex = _orderedLines.FindIndex(l => l.LineId == line.LineId);
			var canMoveUp = lineIndex > 0;
			var canMoveDown = lineIndex >= 0 && lineIndex < _orderedLines.Count - 1;

			var reorderRail = new VerticalStackLayout
			{
				Spacing = 4,
				WidthRequest = 44,
				VerticalOptions = LayoutOptions.Center
			};
			reorderRail.Children.Add(CreateReorderArrowButton(
				SessionMuscleGlyph.ArrowUp,
				canMoveUp,
				"Monter",
				() => MoveExercise(lineIdCaptured, -1)));
			reorderRail.Children.Add(CreateReorderArrowButton(
				SessionMuscleGlyph.ArrowDown,
				canMoveDown,
				"Descendre",
				() => MoveExercise(lineIdCaptured, 1)));

			var menuDots = new Label
			{
				Text = "⋯",
				FontSize = 22,
				HorizontalTextAlignment = TextAlignment.Center,
				TextColor = RhythmColors.TextSecondary,
				LineHeight = 1,
				Padding = new Thickness(10, 12),
				HorizontalOptions = LayoutOptions.Center,
				MinimumWidthRequest = 44,
				MinimumHeightRequest = 44
			};
			menuDots.GestureRecognizers.Add(new TapGestureRecognizer
			{
				Command = new Command(() =>
				{
					CaptureTargetsFromBindings();
					OpenExerciseActions(lineIdCaptured);
				})
			});

			var iconSize = 44d;
			var row = new Grid
			{
				ColumnDefinitions =
				[
					new ColumnDefinition(GridLength.Auto),
					new ColumnDefinition(GridLength.Auto),
					new ColumnDefinition(GridLength.Star),
					new ColumnDefinition(GridLength.Auto),
				],
				ColumnSpacing = 10,
				Padding = new Thickness(8, 8, 10, 8),
				VerticalOptions = LayoutOptions.Fill
			};
			row.Add(reorderRail, 0, 0);
			row.Add(MuscleIconCircle(SessionMuscleGlyph.FromCategory(ex.Category), iconSize), 1, 0);
			row.Add(mid, 2, 0);
			row.Add(menuDots, 3, 0);

			var card = new Border
			{
				StrokeThickness = 0,
				BackgroundColor = RhythmColors.Surface1,
				HorizontalOptions = LayoutOptions.Fill,
				MinimumHeightRequest = 80,
				Content = row
			};
			card.StrokeShape = new RoundRectangle { CornerRadius = 18 };
			card.Shadow = new Shadow
			{
				Brush = Color.FromArgb("#22000000"),
				Radius = 8,
				Offset = new Point(0, 5)
			};

			SemanticProperties.SetDescription(card, $"{CardAutomationHint}:{ex.NameFr}");

			OrderedHost.Children.Add(card);
		}
	}

	private static Border CreateReorderArrowButton(string glyph, bool enabled, string hint, Action onTap)
	{
		var icon = new Label
		{
			Text = glyph,
			FontFamily = SessionMuscleGlyph.FontAlias,
			FontSize = 22,
			HorizontalTextAlignment = TextAlignment.Center,
			VerticalTextAlignment = TextAlignment.Center,
			TextColor = enabled
				? RhythmColors.Accent
				: RhythmColors.TextSecondary.WithAlpha(0.35f)
		};

		var btn = new Border
		{
			WidthRequest = 40,
			HeightRequest = 34,
			BackgroundColor = RhythmColors.Surface2,
			Stroke = enabled
				? RhythmColors.Accent.WithAlpha(0.22f)
				: Colors.Transparent,
			StrokeThickness = enabled ? 1 : 0,
			Padding = 0,
			Content = icon
		};
		btn.StrokeShape = new RoundRectangle { CornerRadius = 12 };
		SemanticProperties.SetHint(btn, hint);

		if (enabled)
		{
			btn.GestureRecognizers.Add(new TapGestureRecognizer
			{
				Command = new Command(onTap)
			});
		}

		return btn;
	}

	private void MoveExercise(Guid lineId, int delta)
	{
		if (delta == 0)
			return;

		ReorderLineDelta(lineId, delta);
		CaptureTargetsFromBindings();
		RenderExerciseList();
	}

	private void ReorderLineDelta(Guid lineId, int deltaSlots)
	{
		if (deltaSlots == 0)
			return;

		var ix = _orderedLines.FindIndex(l => l.LineId == lineId);
		if (ix < 0)
			return;

		var jx = ix + deltaSlots;
		jx = Math.Clamp(jx, 0, _orderedLines.Count - 1);
		if (jx == ix)
			return;

		var item = _orderedLines[ix];
		_orderedLines.RemoveAt(ix);
		_orderedLines.Insert(jx, item);
	}

	private async void OpenExerciseActions(Guid lineId)
	{
		_actionTargetLineId = lineId;
		if (!_exercisesById.TryGetValue(
			    _orderedLines.First(l => l.LineId == lineId).ExerciseId,
			    out var ex))
			return;

		ActionSheetTitle.Text = ex.NameFr;
		ActionSheetSubtitle.Text = $"{ex.Category} · MET≈ {ex.MetApprox:F1}";
		ActionSheetOverlay.IsVisible = true;
		ActionSheetOverlay.InputTransparent = false;

		ActionSheetPanel.Opacity = 0;
		ActionSheetPanel.TranslationY = 80;
		await ActionSheetPanel.FadeToAsync(1, 140, Easing.CubicOut);
		await ActionSheetPanel.TranslateToAsync(0, 0, 210, Easing.CubicOut);
	}

	private void OnActionSheetBackdropTapped(object? sender, EventArgs e) =>
		_ = CloseExerciseActionsAsync();

	private async Task CloseExerciseActionsAsync()
	{
		await ActionSheetPanel.TranslateToAsync(0, 72, 120, Easing.CubicIn);
		await ActionSheetPanel.FadeToAsync(0, 90, Easing.CubicIn);
		ActionSheetOverlay.IsVisible = false;
		ActionSheetOverlay.InputTransparent = true;
		_actionTargetLineId = null;
		ActionSheetPanel.TranslationY = 0;
		ActionSheetPanel.Opacity = 1;
	}

	private async void OnActionEditClicked(object? sender, EventArgs e)
	{
		if (_actionTargetLineId is not { } lid)
			return;

		await CloseExerciseActionsAsync().ConfigureAwait(true);
		CaptureTargetsFromBindings();
		var t = _targetsByLineId[lid];

		var sTxt = await DisplayPromptAsync(
			"Séries cibles",
			"Nombre de séries (1–99)",
			initialValue: t.Sets.ToString(CultureInfo.InvariantCulture),
			keyboard: Keyboard.Numeric).ConfigureAwait(true);

		if (string.IsNullOrWhiteSpace(sTxt))
			return;

		var rTxt = await DisplayPromptAsync(
			"Répétitions cibles",
			"Reps par série (1–999)",
			initialValue: t.Reps.ToString(CultureInfo.InvariantCulture),
			keyboard: Keyboard.Numeric).ConfigureAwait(true);

		if (string.IsNullOrWhiteSpace(rTxt))
			return;

		if (!int.TryParse(sTxt.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var s) ||
		    !int.TryParse(rTxt.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var r))
			return;

		_targetsByLineId[lid] = (Math.Clamp(s, 1, 99), Math.Clamp(r, 1, 999));
		RenderExerciseList();
	}

	private async void OnActionDupClicked(object? sender, EventArgs e)
	{
		if (_actionTargetLineId is not { } lid)
			return;

		await CloseExerciseActionsAsync().ConfigureAwait(true);

		CaptureTargetsFromBindings();
		var ix = _orderedLines.FindIndex(l => l.LineId == lid);
		if (ix < 0)
			return;

		var src = _orderedLines[ix];
		var nid = Guid.NewGuid();
		var copyTargets = _targetsByLineId[lid];
		_orderedLines.Insert(ix + 1, new SessionExerciseLineDraft(nid, src.ExerciseId));
		_targetsByLineId[nid] = copyTargets;
		RenderExerciseList();
	}

	private async void OnActionDeleteClicked(object? sender, EventArgs e)
	{
		if (_actionTargetLineId is not { } lid)
			return;

		await CloseExerciseActionsAsync().ConfigureAwait(true);

		CaptureTargetsFromBindings();
		_orderedLines.RemoveAll(l => l.LineId == lid);
		_targetsByLineId.Remove(lid);
		_entryBindings.Remove(lid);
		RenderExerciseList();
	}

	private async Task FinishOrSaveClickedAsync(bool forcedFinishLabel)
	{
		// Terminer ≡ enregistrer (même validation)
		_ = forcedFinishLabel;
		await PersistSessionAsync(navigateAfter: true).ConfigureAwait(false);
	}

	private async void OnSaveClicked(object? sender, EventArgs e) =>
		await PersistSessionAsync(navigateAfter: true).ConfigureAwait(false);

	private async Task PersistSessionAsync(bool navigateAfter)
	{
		if (string.IsNullOrWhiteSpace(TitleEntry.Text))
		{
			await DisplayAlertAsync("Séance", "Donnez un titre.", "OK").ConfigureAwait(false);
			return;
		}

		CaptureTargetsFromBindings();

		if (_orderedLines.Count == 0)
		{
			await DisplayAlertAsync("Séance", "Ajoute au moins un exercice depuis le catalogue.", "OK")
				.ConfigureAwait(false);
			return;
		}

		foreach (var line in _orderedLines)
		{
			if (!_targetsByLineId.TryGetValue(line.LineId, out var tup) || tup.Sets <= 0 || tup.Reps <= 0)
			{
				await DisplayAlertAsync("Validation", "Nombre de séries et répétitions doit être positif.", "OK")
					.ConfigureAwait(false);
				return;
			}
		}

		try
		{
			var active = ServiceHelper.Services.GetRequiredService<ActiveProfileStore>().Get();
			var repo = ServiceHelper.Services.GetRequiredService<IRhythmoRepository>();

			var sid = _existingSessionId ?? Guid.NewGuid();
			var utc = DateTime.UtcNow;

			var tpl = await repo.GetSessionTemplateAsync(sid).ConfigureAwait(true);
			if (tpl is null)
			{
				tpl = new SessionTemplateRow
				{
					Id = sid,
					OwnerProfileId = active,
					Title = TitleEntry.Text.Trim(),
					CreatedUtc = utc,
					UpdatedUtc = utc
				};
			}
			else
			{
				tpl.Title = TitleEntry.Text.Trim();
				tpl.UpdatedUtc = utc;
			}

			var lineRows = new List<SessionExerciseRow>();
			for (var i = 0; i < _orderedLines.Count; i++)
			{
				var line = _orderedLines[i];
				var t = _targetsByLineId[line.LineId];
				lineRows.Add(new SessionExerciseRow
				{
					Id = Guid.NewGuid(),
					SessionId = sid,
					ExerciseId = line.ExerciseId,
					SortOrder = i,
					TargetSets = t.Sets,
					TargetReps = t.Reps
				});
			}

			await repo.SaveSessionAsync(tpl, lineRows, clearSnapshot: true).ConfigureAwait(true);

			if (!navigateAfter)
				return;

			await MainThread.InvokeOnMainThreadAsync(async () =>
			{
				await RhythmSuccessDialog.ShowAsync(this, "Séance créée avec succès").ConfigureAwait(true);
				await UiShellNavigate.GoAsync("..").ConfigureAwait(true);
			}).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			await _dev.TryShowSafeAsync(ex, nameof(PersistSessionAsync)).ConfigureAwait(false);
		}
	}
}
