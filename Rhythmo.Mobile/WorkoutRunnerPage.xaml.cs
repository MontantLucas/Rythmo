using System.Collections;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls.Shapes;
using Rhythmo.Mobile.Diagnostics;
using Rhythmo.Mobile.Data;
using Rhythmo.Mobile.Infrastructure;
using Rhythmo.Mobile.Services;
using Rhythmo.Mobile.Theme;
using Rhythmo.Shared;
using Rhythmo.Shared.Contracts;
using Rhythmo.Shared.Ranking;

namespace Rhythmo.Mobile;

[QueryProperty(nameof(SessionIdEncoded), "SessionId")]
[QueryProperty(nameof(AdHocEncoded), "AdHoc")]
[QueryProperty(nameof(StartExerciseIdEncoded), "ExerciseId")]
public partial class WorkoutRunnerPage : ContentPage, IQueryAttributable
{
	private static readonly JsonSerializerOptions JsonSnake = new()
	{
		PropertyNameCaseInsensitive = true,
		ReadCommentHandling = JsonCommentHandling.Skip,
		AllowTrailingCommas = true,
		WriteIndented = false,
	};

	private Guid _sid;
	private readonly List<ExerciseUi> _blocks = [];
	private SessionTemplateRow? _tpl;
	private ProfileRow? _profile;
	private bool _sessionUiReady;
	private bool _isAdHoc;
	private bool _adHocQuery;
	private Guid? _startExerciseId;
	private string _sessionTitle = AdHocWorkout.DefaultTitle;
	private readonly Dictionary<Guid, CachedExerciseRow> _catalogById = [];
	private Dictionary<Guid, double> _prMaxByExercise = [];
	private IReadOnlyDictionary<Guid, Rhythmo.Shared.Ranking.ExerciseRankingDef> _rankingDefs =
		new Dictionary<Guid, Rhythmo.Shared.Ranking.ExerciseRankingDef>();
	private string? _addCatalogFilterCategory;
	private bool _suppressAddSheetCategorySelector;
	private CancellationTokenSource? _catalogSearchDebounceCts;

	private int _currentExerciseIndex;
	private DateTime _draftStartedUtc;
	private CancellationTokenSource? _draftSaveCts;
	private bool _finalizeQueued;
	private readonly object _draftGate = new();

	private readonly WorkoutDraftStore _draftStore =
		ServiceHelper.Services.GetRequiredService<WorkoutDraftStore>();

	private readonly IDevErrorPresenter _dev =
		ServiceHelper.Services.GetRequiredService<IDevErrorPresenter>();

	public string SessionIdEncoded
	{
		set
		{
			if (string.IsNullOrWhiteSpace(value))
			{
				_sid = Guid.Empty;
				return;
			}

			_sid = Guid.Parse(Uri.UnescapeDataString(value));
		}
	}

	public string AdHocEncoded
	{
		set =>
			_adHocQuery = value is "1" or "true" or "True";
	}

	public string StartExerciseIdEncoded
	{
		set =>
			_startExerciseId = Guid.TryParse(Uri.UnescapeDataString(value ?? ""), out var id)
				? id
				: null;
	}

	public void ApplyQueryAttributes(IDictionary<string, object> query)
	{
		if (query.TryGetValue("AdHoc", out var adhoc) && adhoc is not null)
			AdHocEncoded = adhoc.ToString() ?? "";
		if (query.TryGetValue("SessionId", out var sid) && sid is not null)
			SessionIdEncoded = sid.ToString() ?? "";
		if (query.TryGetValue("ExerciseId", out var ex) && ex is not null)
			StartExerciseIdEncoded = ex.ToString() ?? "";
	}

	public WorkoutRunnerPage()
	{
		InitializeComponent();
		AddSheetSearchBar.TextChanged += OnAddSheetSearchChanged;
	}

	private async void OnAbandonSessionClicked(object? sender, EventArgs e)
	{
		var abandon = await DisplayAlertAsync(
			"Annuler la séance ?",
			"Tu quitteras sans enregistrer de résultat (pas d’historique ni de mise à jour des charges).",
			"Oui, annuler",
			"Non").ConfigureAwait(true);

		if (!abandon)
			return;

		var activeProfileId = ServiceHelper.Services.GetRequiredService<ActiveProfileStore>().Get();
		lock (_draftGate)
			_draftStore.Clear(activeProfileId);

		await UiShellNavigate.GoAsync("..").ConfigureAwait(false);
	}

	private sealed class ExerciseUi
	{
		public required Guid ExerciseId { get; init; }
		public required string Title { get; init; }
		public required string Category { get; init; }
		public double Met { get; init; }
		public double HintKg { get; init; }
		public double PrMaxKg { get; init; }
		public string WeightPlaceholder { get; init; } = "kg";

		public required SessionExerciseRow TemplateLine { get; init; }

		public VerticalStackLayout RowsWrapper { get; } = new() { Spacing = 12 };

		public List<(Entry Reps, Entry Kg)> RowEntries { get; } = [];

		public List<CheckBox> RowDoneBoxes { get; } = [];

		public List<double?> RowKgHints { get; } = [];

		public List<Label> RowIndexLabels { get; } = [];
	}

	private sealed record PreparedWorkout(
		DateTime Utc,
		DateOnly PerformanceLocalDate,
		IReadOnlyList<CompletedExerciseSetsDto> Exercises,
		IReadOnlyList<(Guid ExerciseId, double MaxKg, double LastKg)> ExerciseStats,
		int TotalFilledSets,
		double Calories,
		double Minutes);

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		await RefreshAsync().ConfigureAwait(true);
	}

	private async Task RefreshAsync()
	{
		try
		{
			if (_sessionUiReady)
				return;

			_blocks.Clear();
			_currentExerciseIndex = 0;
			_catalogById.Clear();

			var repo = ServiceHelper.Services.GetRequiredService<IRhythmoRepository>();
			var activeProfileId = ServiceHelper.Services.GetRequiredService<ActiveProfileStore>().Get();

			_profile = await repo.GetProfileAsync(activeProfileId).ConfigureAwait(true)
			           ?? throw new InvalidOperationException("Profil introuvable.");

			var localDraft = _draftStore.TryLoad(activeProfileId);
			_isAdHoc = _adHocQuery ||
			           (localDraft is { IsAdHoc: true } &&
			            (_sid == Guid.Empty || localDraft.SessionId == _sid));

			IReadOnlyList<SessionExerciseRow> tplLines = [];
			if (_isAdHoc)
			{
				_tpl = null;
				if (localDraft is { IsAdHoc: true } && (_sid == Guid.Empty || localDraft.SessionId == _sid))
					_sid = localDraft.SessionId;
				if (_sid == Guid.Empty)
					_sid = Guid.NewGuid();
				_sessionTitle = localDraft is { IsAdHoc: true }
					? localDraft.SessionTitle
					: AdHocWorkout.TitleForNow();
			}
			else
			{
				_tpl = await repo.GetSessionTemplateAsync(_sid).ConfigureAwait(true);
				if (_tpl is null)
				{
					await DisplayAlertAsync("Séance", "Introuvable.", "OK").ConfigureAwait(false);
					await UiShellNavigate.GoAsync("..").ConfigureAwait(false);
					return;
				}

				_sessionTitle = _tpl.Title;
				tplLines = await repo.ListSessionExercisesAsync(_sid).ConfigureAwait(true);
			}

			SessionMetaSmall.Text =
				$"{_sessionTitle} · {_profile.DisplayName} · {(_profile.BiologicalSex == BiologicalSex.Male ? "homme" : "femme")} · {FormatKg(_profile.WeightKg)}";

			var useDraft = localDraft is not null &&
			               localDraft.SessionId == _sid &&
			               localDraft.IsAdHoc == _isAdHoc;
			IReadOnlyList<WorkoutDraftExerciseDto>? draftExercises = useDraft
				? localDraft!.Exercises
				: null;

			Dictionary<Guid, List<(int reps, double weight)>>? prefs = null;
			if (!_isAdHoc && !useDraft)
			{
				var snapRow = await repo.GetSessionSnapshotAsync(_sid).ConfigureAwait(true);
				if (!string.IsNullOrWhiteSpace(snapRow?.Json))
				{
					var env = JsonSerializer.Deserialize<LastRunEnvelope>(snapRow.Json, JsonSnake);
					if (env?.Exercises is { Count: > 0 })
					{
						prefs = new Dictionary<Guid, List<(int, double)>>();
						foreach (var ex in env.Exercises)
						{
							var list = new List<(int, double)>();
							foreach (var s in ex.Sets)
								list.Add((s.Reps, s.WeightKg));
							prefs[ex.ExerciseId] = list;
						}
					}
				}
			}

			var catalog = await repo.ListExercisesAsync().ConfigureAwait(true);
			foreach (var ex in catalog)
				_catalogById[ex.Id] = ex;
			_rankingDefs = RankingExerciseIndex.Map(catalog);

			var exerciseIdsForWeights = _isAdHoc
				? (draftExercises?.Select(e => e.ExerciseId) ?? [])
					.Concat(_startExerciseId is { } startId ? [startId] : Array.Empty<Guid>())
					.Distinct()
					.ToList()
				: tplLines.Select(l => l.ExerciseId).Distinct().ToList();

			var weightRowsTask = Task.WhenAll(exerciseIdsForWeights.Select(id =>
				repo.GetLastWeightAsync(activeProfileId, id)));
			var prMaxTask = repo.ListExerciseAllTimeMaxKgAsync(activeProfileId);
			await Task.WhenAll(weightRowsTask, prMaxTask).ConfigureAwait(true);
			var weightRows = await weightRowsTask.ConfigureAwait(true);
			_prMaxByExercise = new Dictionary<Guid, double>(await prMaxTask.ConfigureAwait(true));

			var hints = new Dictionary<Guid, double>();
			for (var i = 0; i < exerciseIdsForWeights.Count; i++)
			{
				if (weightRows[i] is { } w)
					hints[exerciseIdsForWeights[i]] = w.WeightKg;
			}

			await MainThread.InvokeOnMainThreadAsync(() =>
			{
				AddExerciseBtn.IsVisible = _isAdHoc;
				RemoveExerciseBtn.IsVisible = false;
				SeedAddSheetCategoryPicker();

				if (_isAdHoc)
				{
					if (draftExercises is { Count: > 0 })
					{
						foreach (var draftEx in draftExercises)
						{
							if (!_catalogById.TryGetValue(draftEx.ExerciseId, out var cx))
								continue;
							hints.TryGetValue(cx.Id, out var hintKg);
							_prMaxByExercise.TryGetValue(cx.Id, out var prMaxKg);
							var blk = CreateExerciseBlock(cx, hintKg, prMaxKg, 3, 10);
							FillBlockSets(blk, draftEx, null, hintKg);
							_blocks.Add(blk);
						}
					}
					else if (_startExerciseId is { } firstId &&
					         _catalogById.TryGetValue(firstId, out var firstEx))
					{
						hints.TryGetValue(firstEx.Id, out var hintKg);
						_prMaxByExercise.TryGetValue(firstEx.Id, out var prMaxKg);
						var blk = CreateExerciseBlock(firstEx, hintKg, prMaxKg, 3, 10);
						FillBlockSets(blk, null, null, hintKg);
						_blocks.Add(blk);
					}
				}
				else
				{
					foreach (var line in tplLines)
					{
						if (!_catalogById.TryGetValue(line.ExerciseId, out var cx))
							continue;

						hints.TryGetValue(cx.Id, out var hintKg);
						_prMaxByExercise.TryGetValue(cx.Id, out var prMaxKg);

						var prefsList = prefs is not null && prefs.TryGetValue(cx.Id, out var pl)
							? pl
							: null;
						var draftEx = draftExercises?.FirstOrDefault(e => e.ExerciseId == cx.Id);

						var blk = CreateExerciseBlock(
							cx, hintKg, prMaxKg, line.TargetSets, line.TargetReps ?? 10, line);
						FillBlockSets(blk, draftEx, prefsList, hintKg);
						_blocks.Add(blk);
					}
				}

				_draftStartedUtc = useDraft ? localDraft!.StartedUtc : DateTime.UtcNow;
				_sessionUiReady = true;

				if (_blocks.Count > 0)
				{
					var exerciseIndex = useDraft
						? Math.Clamp(localDraft!.CurrentExerciseIndex, 0, _blocks.Count - 1)
						: 0;
					DisplayExercise(exerciseIndex);
					if (!useDraft)
						PersistDraftNow(activeProfileId);
				}
				else
					ShowEmptyAdHocState();
			}).ConfigureAwait(true);

			if (!_isAdHoc && _blocks.Count == 0)
				await DisplayAlertAsync("Séance", "Ajoute au moins un exercice depuis l’éditeur.", "OK")
					.ConfigureAwait(true);
		}
		catch (Exception ex)
		{
			await _dev.TryShowSafeAsync(ex, nameof(RefreshAsync)).ConfigureAwait(false);
		}
	}

	private void DisplayExercise(int index)
	{
		if (_blocks.Count == 0)
		{
			ShowEmptyAdHocState();
			return;
		}

		index = Math.Clamp(index, 0, _blocks.Count - 1);
		_currentExerciseIndex = index;

		var blk = _blocks[index];
		ExerciseTitleLabel.Text = blk.Title;
		ExerciseCategoryLabel.Text = blk.Category.ToUpperInvariant();
		if (blk.PrMaxKg > double.Epsilon)
		{
			ExercisePrMaxLabel.IsVisible = true;
			ExercisePrMaxLabel.Text =
				$"PR max · {blk.PrMaxKg.ToString("0.#", CultureInfo.InvariantCulture)} kg";
		}
		else
		{
			ExercisePrMaxLabel.IsVisible = false;
			ExercisePrMaxLabel.Text = "";
		}
		ExerciseCounterLabel.Text = $"Exercice {index + 1} sur {_blocks.Count}";
		ExerciseProgressBar.Progress = (index + 1d) / _blocks.Count;

		PrevExerciseBtn.IsEnabled = index > 0;
		NextExerciseBtn.IsEnabled = index < _blocks.Count - 1;
		AddSetBtn.IsEnabled = true;
		RemoveExerciseBtn.IsVisible = _isAdHoc;

		ActiveExerciseRoot.Children.Clear();
		ActiveExerciseRoot.Children.Add(BuildSetsHeader());
		ActiveExerciseRoot.Children.Add(blk.RowsWrapper);
		ScheduleDraftSave();
	}

	private void ShowEmptyAdHocState()
	{
		ExerciseTitleLabel.Text = "Ajoute un exercice";
		ExerciseCategoryLabel.Text = _isAdHoc ? "SÉANCE À LA VOLÉE" : "";
		ExercisePrMaxLabel.IsVisible = false;
		ExercisePrMaxLabel.Text = "";
		ExerciseCounterLabel.Text = "0 exercice";
		ExerciseProgressBar.Progress = 0;
		PrevExerciseBtn.IsEnabled = false;
		NextExerciseBtn.IsEnabled = false;
		AddSetBtn.IsEnabled = false;
		RemoveExerciseBtn.IsVisible = false;

		ActiveExerciseRoot.Children.Clear();
		ActiveExerciseRoot.Children.Add(new Label
		{
			Text = "Parfait entre amis : tu ajoutes les mouvements au fil de la séance. Les kcal, charges et PR seront enregistrés, sans créer de modèle dans Tes séances.",
			FontSize = 14,
			TextColor = RhythmColors.TextSecondary,
			LineBreakMode = LineBreakMode.WordWrap
		});
	}

	private ExerciseUi CreateExerciseBlock(
		CachedExerciseRow cx,
		double hintKg,
		double prMaxKg,
		int targetSets,
		int targetReps,
		SessionExerciseRow? templateLine = null)
	{
		return new ExerciseUi
		{
			ExerciseId = cx.Id,
			Title = cx.NameFr,
			Category = string.IsNullOrWhiteSpace(cx.Category) ? "Mixte" : cx.Category,
			Met = cx.MetApprox,
			HintKg = hintKg,
			PrMaxKg = prMaxKg,
			WeightPlaceholder = _rankingDefs.TryGetValue(cx.Id, out var rankDef)
				? RankingExerciseIndex.WeightPlaceholder(rankDef.LoadMode)
				: "kg",
			TemplateLine = templateLine ?? new SessionExerciseRow
			{
				Id = Guid.NewGuid(),
				SessionId = _sid,
				ExerciseId = cx.Id,
				SortOrder = _blocks.Count,
				TargetSets = Math.Clamp(targetSets, 1, 99),
				TargetReps = Math.Clamp(targetReps, 1, 999)
			}
		};
	}

	private void FillBlockSets(
		ExerciseUi blk,
		WorkoutDraftExerciseDto? draftEx,
		List<(int reps, double weight)>? prefsList,
		double hintKg)
	{
		if (draftEx is { Sets.Count: > 0 })
		{
			foreach (var ds in draftEx.Sets)
				AddSetRow(blk, ds.RepsText, ds.KgText, null, ds.IsDone);
			return;
		}

		var setCount = prefsList?.Count > 0 ? prefsList.Count : blk.TemplateLine.TargetSets;
		for (var i = 0; i < setCount; i++)
		{
			var repsTxt = prefsList is not null && i < prefsList.Count
				? prefsList[i].reps.ToString(CultureInfo.InvariantCulture)
				: (blk.TemplateLine.TargetReps?.ToString(CultureInfo.InvariantCulture) ?? "10");

			double? kgHint = null;
			if (prefsList is not null && i < prefsList.Count && prefsList[i].weight > double.Epsilon)
				kgHint = prefsList[i].weight;
			else if (hintKg > double.Epsilon)
				kgHint = hintKg;

			var kgTxt = prefsList is not null && i < prefsList.Count && prefsList[i].weight > double.Epsilon
				? prefsList[i].weight.ToString("0.#", CultureInfo.InvariantCulture)
				: "";

			AddSetRow(blk, repsTxt, kgTxt, kgHint);
		}
	}

	private static HorizontalStackLayout BuildSetsHeader()
	{
		static Label H(string text, double wReq = -1)
		{
			var l = new Label
			{
				Text = text,
				FontAttributes = FontAttributes.Bold,
				FontSize = 12,
				TextColor = RhythmColors.TextSecondary,
				VerticalOptions = LayoutOptions.Center
			};
			if (wReq > 0)
				l.WidthRequest = wReq;
			return l;
		}

		return new HorizontalStackLayout
		{
			Spacing = 12,
			Padding = new Thickness(0, 0, 0, 6),
			Children =
			{
				H("#", 28),
				H("Kg · note", 168),
				H("Reps", 72),
				H("✓", 36),
				H("", 40)
			}
		};
	}

	private void AddSetRow(
		ExerciseUi blk,
		string reps,
		string kgEntryText,
		double? lastSessionKgHint,
		bool isDone = false)
	{
		var idx = blk.RowEntries.Count + 1;
		var idxLbl = new Label
		{
			Text = idx.ToString(CultureInfo.InvariantCulture),
			WidthRequest = 28,
			HorizontalTextAlignment = TextAlignment.Center,
			VerticalOptions = LayoutOptions.Center,
			TextColor = RhythmColors.TextSecondary,
			FontAttributes = FontAttributes.Bold
		};

		var kgEntry = new Entry
		{
			Placeholder = blk.WeightPlaceholder,
			Keyboard = Keyboard.Numeric,
			Text = kgEntryText,
			WidthRequest = 88,
			HorizontalOptions = LayoutOptions.Start
		};
		var kgHintLabel = new Label
		{
			VerticalOptions = LayoutOptions.Center,
			FontSize = 12,
			TextColor = RhythmColors.TextSecondary,
			MinimumWidthRequest = 52,
			LineBreakMode = LineBreakMode.NoWrap
		};
		if (lastSessionKgHint is > double.Epsilon)
			kgHintLabel.Text =
				lastSessionKgHint.Value.ToString("0.#", CultureInfo.InvariantCulture) + " kg";

		var kgRegion = new HorizontalStackLayout
		{
			Spacing = 8,
			VerticalOptions = LayoutOptions.Center,
			MinimumWidthRequest = 168,
			Children = { kgEntry, kgHintLabel }
		};

		var repsEntry = new Entry
		{
			Placeholder = "reps",
			Keyboard = Keyboard.Numeric,
			Text = reps,
			WidthRequest = 72
		};

		var doneCb = new CheckBox { VerticalOptions = LayoutOptions.Center, IsChecked = isDone };
		doneCb.CheckedChanged += (_, _) => ScheduleDraftSave();

		repsEntry.TextChanged += (_, _) => ScheduleDraftSave();
		kgEntry.TextChanged += (_, _) => ScheduleDraftSave();

		var removeBtn = new Button
		{
			Text = "✕",
			FontSize = 13,
			Padding = new Thickness(0),
			WidthRequest = 36,
			HeightRequest = 36,
			MinimumWidthRequest = 36,
			MinimumHeightRequest = 36,
			CornerRadius = 18,
			BackgroundColor = Colors.Transparent,
			TextColor = RhythmColors.TextSecondary,
			BorderWidth = 0,
			VerticalOptions = LayoutOptions.Center,
			Margin = new Thickness(10, 0, 0, 0)
		};
		SemanticProperties.SetHint(removeBtn, "Supprimer cette série");
		removeBtn.Clicked += async (_, _) =>
		{
			var i = blk.RowEntries.FindIndex(p => ReferenceEquals(p.Kg, kgEntry));
			if (i >= 0)
				await ConfirmAndRemoveSetAsync(blk, i).ConfigureAwait(true);
		};

		var row = new HorizontalStackLayout
		{
			Spacing = 12,
			VerticalOptions = LayoutOptions.Center,
			Children = { idxLbl, kgRegion, repsEntry, doneCb, removeBtn }
		};

		blk.RowsWrapper.Children.Add(row);
		blk.RowEntries.Add((repsEntry, kgEntry));
		blk.RowDoneBoxes.Add(doneCb);
		blk.RowKgHints.Add(lastSessionKgHint);
		blk.RowIndexLabels.Add(idxLbl);
	}

	private async Task ConfirmAndRemoveSetAsync(ExerciseUi blk, int index)
	{
		if (index < 0 || index >= blk.RowEntries.Count)
			return;

		var n = index + 1;
		var confirm = await DisplayAlertAsync(
			"Supprimer cette série ?",
			$"La série {n} de « {blk.Title} » sera retirée. Tu pourras en rajouter une avec + série.",
			"Supprimer",
			"Annuler").ConfigureAwait(true);
		if (!confirm)
			return;

		blk.RowsWrapper.Children.RemoveAt(index);
		blk.RowEntries.RemoveAt(index);
		blk.RowDoneBoxes.RemoveAt(index);
		blk.RowKgHints.RemoveAt(index);
		blk.RowIndexLabels.RemoveAt(index);

		for (var i = 0; i < blk.RowIndexLabels.Count; i++)
			blk.RowIndexLabels[i].Text = (i + 1).ToString(CultureInfo.InvariantCulture);

		var profileId = ServiceHelper.Services.GetRequiredService<ActiveProfileStore>().Get();
		PersistDraftNow(profileId);
	}

	private void OnAddSetClicked(object? sender, EventArgs e)
	{
		if (_blocks.Count == 0)
			return;

		var blk = _blocks[_currentExerciseIndex];
		var lastRep = blk.TemplateLine.TargetReps?.ToString(CultureInfo.InvariantCulture) ?? "10";
		if (blk.RowEntries.Count > 0)
		{
			var lastPair = blk.RowEntries[^1];
			lastRep = string.IsNullOrWhiteSpace(lastPair.Reps.Text)
				? lastRep
				: lastPair.Reps.Text!;
		}

		double? nextHint = blk.HintKg > double.Epsilon ? blk.HintKg : null;
		if (blk.RowKgHints.Count > 0)
		{
			var lh = blk.RowKgHints[^1];
			if (lh is > double.Epsilon)
				nextHint = lh;
		}

		AddSetRow(blk, lastRep, "", nextHint);
		var profileId = ServiceHelper.Services.GetRequiredService<ActiveProfileStore>().Get();
		PersistDraftNow(profileId);
	}

	private void OnPrevExerciseClicked(object? sender, EventArgs e)
	{
		if (_blocks.Count == 0)
			return;
		DisplayExercise(_currentExerciseIndex - 1);
	}

	private void OnNextExerciseClicked(object? sender, EventArgs e)
	{
		if (_blocks.Count == 0)
			return;
		DisplayExercise(_currentExerciseIndex + 1);
	}

	private async void OnFinalizeClicked(object? sender, EventArgs e)
	{
		if (_profile is null || (!_isAdHoc && _tpl is null) || _finalizeQueued)
			return;

		FinalizeBtn.IsEnabled = false;
		FinalizeBtn.Text = "Enregistrement…";
		try
		{
			var activeProfileId =
				ServiceHelper.Services.GetRequiredService<ActiveProfileStore>().Get();
			var prepared = BuildPreparedWorkout();
			if (prepared is null)
			{
				await DisplayAlertAsync("Validation", "Aucune série complète trouvée (reps et kg positifs).", "OK")
					.ConfigureAwait(true);
				ResetFinalizeButton();
				return;
			}

			var job = new PendingWorkoutFinalize
			{
				WorkoutId = Guid.NewGuid(),
				ProfileId = activeProfileId,
				CompletedUtc = prepared.Utc,
				PerformanceLocalDate = prepared.PerformanceLocalDate,
				SessionTitle = _sessionTitle,
				IsAdHoc = _isAdHoc,
				SessionId = _sid,
				Calories = prepared.Calories,
				Minutes = prepared.Minutes,
				TotalFilledSets = prepared.TotalFilledSets,
				Stats = prepared.ExerciseStats
					.Select(s => new PendingExerciseStat
					{
						ExerciseId = s.ExerciseId,
						MaxKg = s.MaxKg,
						LastKg = s.LastKg
					})
					.ToList(),
				Exercises = prepared.Exercises.ToList()
			};

			var finalize = ServiceHelper.Services.GetRequiredService<WorkoutFinalizeService>();
			lock (_draftGate)
			{
				_finalizeQueued = true;
				_sessionUiReady = false;
				_draftSaveCts?.Cancel();
			}

			try
			{
				finalize.Enqueue(job);
			}
			catch
			{
				lock (_draftGate)
				{
					_finalizeQueued = false;
					_sessionUiReady = true;
				}

				throw;
			}

			lock (_draftGate)
				_draftStore.Clear(activeProfileId);

			await UiShellNavigate.GoAsync("..").ConfigureAwait(true);
		}
		catch (Exception ex) when (SupabaseAuthService.RequiresReauthentication(ex))
		{
			if (!_finalizeQueued)
				ResetFinalizeButton();
			await RhythmAlertDialog.ShowAsync(
				this,
				"Session expirée",
				"La séance n'a pas été enregistrée. Reconnecte-toi puis réessaie : tes charges sont toujours affichées à l'écran.",
				isError: true).ConfigureAwait(true);
		}
		catch (Exception ex)
		{
			if (!_finalizeQueued)
				ResetFinalizeButton();
			await _dev.TryShowSafeAsync(ex, nameof(OnFinalizeClicked)).ConfigureAwait(true);
		}
	}

	private void ResetFinalizeButton()
	{
		FinalizeBtn.IsEnabled = true;
		FinalizeBtn.Text = "✓ Terminer la séance";
	}

	private PreparedWorkout? BuildPreparedWorkout()
	{
		var utc = DateTime.UtcNow;
		var performanceLocalDate =
			DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(utc, TimeZoneInfo.Local));

		var dtoList = new List<CompletedExerciseSetsDto>();
		var chunkList = new List<(double Met, int SetCount)>();
		var exerciseStats = new List<(Guid ExerciseId, double MaxKg, double LastKg)>();
		var totalFilledSets = 0;

		foreach (var blk in _blocks)
		{
			var parsedSets = new List<SetDto>();
			for (var setIdx = 0; setIdx < blk.RowEntries.Count; setIdx++)
			{
				var (repE, kgE) = blk.RowEntries[setIdx];
				var repOk =
					int.TryParse(repE.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var r);
				var wOk =
					double.TryParse(kgE.Text?.Replace(',', '.'), NumberStyles.Float,
						CultureInfo.InvariantCulture,
						out var w);
				if (!repOk || !wOk || r <= 0 || w <= 0)
					continue;

				parsedSets.Add(new SetDto(r, w, setIdx + 1));
			}

			if (parsedSets.Count == 0)
				continue;

			totalFilledSets += parsedSets.Count;
			dtoList.Add(new CompletedExerciseSetsDto(blk.ExerciseId, parsedSets));
			chunkList.Add((blk.Met, parsedSets.Count));
			exerciseStats.Add((blk.ExerciseId, parsedSets.Max(s => s.WeightKg), parsedSets.Last().WeightKg));
		}

		if (dtoList.Count == 0)
			return null;

		var calories = CaloriesEstimator.EstimateSessionKcal(
			new CaloriesSubject
			{
				WeightKg = _profile!.WeightKg,
				IsFemale = _profile.BiologicalSex == BiologicalSex.Female,
				AgeYears = _profile.AgeYears,
				HeightCm = _profile.HeightCm
			},
			chunkList);
		var minutes = Math.Round(totalFilledSets * CaloriesEstimator.MinutesPerStrengthSet, 0);

		return new PreparedWorkout(
			utc,
			performanceLocalDate,
			dtoList,
			exerciseStats,
			totalFilledSets,
			calories,
			minutes);
	}

	void ScheduleDraftSave()
	{
		if (!_sessionUiReady)
			return;

		_draftSaveCts?.Cancel();
		_draftSaveCts = new CancellationTokenSource();
		var token = _draftSaveCts.Token;
		_ = Task.Run(async () =>
		{
			try
			{
				await Task.Delay(400, token).ConfigureAwait(false);
				var profileId = ServiceHelper.Services.GetRequiredService<ActiveProfileStore>().Get();
				PersistDraftNow(profileId);
			}
			catch (OperationCanceledException)
			{
				// Debounce normal.
			}
		}, token);
	}

	void PersistDraftNow(Guid profileId)
	{
		lock (_draftGate)
		{
			if (!_sessionUiReady || _finalizeQueued)
				return;

			if (_isAdHoc && _blocks.Count == 0)
			{
				_draftStore.Clear(profileId);
				return;
			}

			if (!_isAdHoc && _tpl is null)
				return;

			try
			{
				_draftStore.Save(BuildDraftEnvelope(profileId));
			}
			catch (Exception ex)
			{
				CrashLogWriter.TryAppend(nameof(PersistDraftNow), ex);
			}
		}
	}

	WorkoutDraftEnvelope BuildDraftEnvelope(Guid profileId)
	{
		var exercises = _blocks.Select(blk =>
		{
			var sets = new List<WorkoutDraftSetDto>(blk.RowEntries.Count);
			for (var i = 0; i < blk.RowEntries.Count; i++)
			{
				var (reps, kg) = blk.RowEntries[i];
				sets.Add(new WorkoutDraftSetDto(
					reps.Text ?? "",
					kg.Text ?? "",
					blk.RowDoneBoxes[i].IsChecked));
			}

			return new WorkoutDraftExerciseDto(blk.ExerciseId, sets);
		}).ToList();

		return new WorkoutDraftEnvelope(
			profileId,
			_sid,
			_sessionTitle,
			_draftStartedUtc,
			DateTime.UtcNow,
			_currentExerciseIndex,
			exercises)
		{
			IsAdHoc = _isAdHoc
		};
	}

	protected override void OnDisappearing()
	{
		_catalogSearchDebounceCts?.Cancel();
		_catalogSearchDebounceCts?.Dispose();
		_catalogSearchDebounceCts = null;
		base.OnDisappearing();
	}

	private async void OnRemoveExerciseClicked(object? sender, EventArgs e)
	{
		if (!_isAdHoc || _blocks.Count == 0)
			return;

		var blk = _blocks[_currentExerciseIndex];
		var confirm = await DisplayAlertAsync(
			"Retirer cet exercice ?",
			$"« {blk.Title} » sera enlevé de cette séance à la volée (pas encore enregistré).",
			"Retirer",
			"Annuler").ConfigureAwait(true);
		if (!confirm)
			return;

		_blocks.RemoveAt(_currentExerciseIndex);
		var profileId = ServiceHelper.Services.GetRequiredService<ActiveProfileStore>().Get();
		if (_blocks.Count == 0)
		{
			ShowEmptyAdHocState();
			PersistDraftNow(profileId);
			return;
		}

		DisplayExercise(Math.Min(_currentExerciseIndex, _blocks.Count - 1));
		PersistDraftNow(profileId);
	}

	private void OnAddExerciseClicked(object? sender, EventArgs e) => OpenAddExerciseSheet();

	private void OnAddSheetBackdropTapped(object? sender, EventArgs e) =>
		_ = CloseAddExerciseSheetAsync();

	private void OnCloseAddSheetClicked(object? sender, EventArgs e) =>
		_ = CloseAddExerciseSheetAsync();

	private async void OpenAddExerciseSheet()
	{
		AddSheetOverlay.IsVisible = true;
		AddSheetOverlay.InputTransparent = false;
		AddSheetPanel.Opacity = 0;
		AddSheetPanel.TranslationY = 96;
		RefreshAddSheetCatalog();

		await AddSheetPanel.FadeToAsync(1, 160, Easing.CubicOut);
		await AddSheetPanel.TranslateToAsync(0, 0, 220, Easing.CubicOut);
	}

	private async Task CloseAddExerciseSheetAsync()
	{
		AddSheetCategorySelector.CloseDropdown();
		await AddSheetPanel.TranslateToAsync(0, 80, 140, Easing.CubicIn);
		await AddSheetPanel.FadeToAsync(0, 100, Easing.CubicIn);
		AddSheetOverlay.IsVisible = false;
		AddSheetOverlay.InputTransparent = true;
		AddSheetPanel.TranslationY = 0;
		AddSheetPanel.Opacity = 1;
	}

	private void SeedAddSheetCategoryPicker()
	{
		_suppressAddSheetCategorySelector = true;
		try
		{
			var list = new List<string>(1 + LocalExerciseSeed.CategoryOrder.Count) { "Tous les groupes" };
			list.AddRange(LocalExerciseSeed.CategoryOrder);
			AddSheetCategorySelector.ItemsSource = list;
			AddSheetCategorySelector.SelectedIndex = 0;
			_addCatalogFilterCategory = null;
		}
		finally
		{
			_suppressAddSheetCategorySelector = false;
		}
	}

	private void OnAddSheetCategoryChanged(object? sender, EventArgs e)
	{
		if (_suppressAddSheetCategorySelector || AddSheetCategorySelector.SelectedIndex < 0)
			return;

		if (AddSheetCategorySelector.SelectedIndex <= 0)
			_addCatalogFilterCategory = null;
		else if (AddSheetCategorySelector.ItemsSource is IList list &&
		         AddSheetCategorySelector.SelectedIndex < list.Count &&
		         list[AddSheetCategorySelector.SelectedIndex] is string s)
			_addCatalogFilterCategory = s;
		else
			_addCatalogFilterCategory = null;

		if (AddSheetOverlay.IsVisible)
			RefreshAddSheetCatalog();
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
		foreach (var ex in _catalogById.Values.OrderBy(x => x.NameFr, StringComparer.CurrentCultureIgnoreCase))
		{
			if (!ExercisePassesAddFilters(ex))
				continue;

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
			var exercise = ex;
			addBtn.Clicked += async (_, _) =>
			{
				if (!addBtn.IsEnabled)
					return;
				addBtn.IsEnabled = false;
				await AddExerciseFromCatalogAsync(exercise).ConfigureAwait(true);
			};

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
				TextColor = RhythmColors.TextSecondary
			};
			var textCol = new VerticalStackLayout { Spacing = 2, Children = { name, muscle } };
			var row = new Grid
			{
				ColumnDefinitions =
				[
					new ColumnDefinition(GridLength.Star),
					new ColumnDefinition(GridLength.Auto)
				],
				ColumnSpacing = 12
			};
			row.Add(textCol, 0);
			row.Add(addBtn, 1);

			var wrap = new Border
			{
				Padding = new Thickness(12, 10),
				BackgroundColor = RhythmColors.Surface1,
				StrokeThickness = 0,
				StrokeShape = new RoundRectangle { CornerRadius = 16 },
				Content = row
			};
			AddCatalogHost.Children.Add(wrap);
		}
	}

	private async Task AddExerciseFromCatalogAsync(CachedExerciseRow cx)
	{
		var repo = ServiceHelper.Services.GetRequiredService<IRhythmoRepository>();
		var profileId = ServiceHelper.Services.GetRequiredService<ActiveProfileStore>().Get();
		var last = await repo.GetLastWeightAsync(profileId, cx.Id).ConfigureAwait(true);
		var hintKg = last?.WeightKg ?? 0;
		_prMaxByExercise.TryGetValue(cx.Id, out var prMaxKg);

		var blk = CreateExerciseBlock(cx, hintKg, prMaxKg, 3, 10);
		FillBlockSets(blk, null, null, hintKg);
		_blocks.Add(blk);

		await CloseAddExerciseSheetAsync().ConfigureAwait(true);
		DisplayExercise(_blocks.Count - 1);
		PersistDraftNow(profileId);
	}

	private static string FormatKg(double w) =>
		w.ToString("0.#", CultureInfo.InvariantCulture) + " kg";
}
