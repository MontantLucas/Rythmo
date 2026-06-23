using System.Globalization;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.ApplicationModel;
using Rhythmo.Mobile.Diagnostics;
using Rhythmo.Mobile.Data;
using Rhythmo.Mobile.Infrastructure;
using Rhythmo.Mobile.Services;
using Rhythmo.Mobile.Social;
using Rhythmo.Mobile.Theme;
using Rhythmo.Shared;
using Rhythmo.Shared.Contracts;

namespace Rhythmo.Mobile;

[QueryProperty(nameof(SessionIdEncoded), "SessionId")]
public partial class WorkoutRunnerPage : ContentPage
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

	private int _currentExerciseIndex;
	private DateTime _draftStartedUtc;
	private CancellationTokenSource? _draftSaveCts;

	private readonly WorkoutDraftStore _draftStore =
		ServiceHelper.Services.GetRequiredService<WorkoutDraftStore>();

	private readonly IDevErrorPresenter _dev =
		ServiceHelper.Services.GetRequiredService<IDevErrorPresenter>();

	public string SessionIdEncoded
	{
		set =>
			_sid = Guid.Parse(Uri.UnescapeDataString(value ?? throw new ArgumentNullException(nameof(value))));
	}

	public WorkoutRunnerPage()
	{
		InitializeComponent();
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

		public required SessionExerciseRow TemplateLine { get; init; }

		public VerticalStackLayout RowsWrapper { get; } = new() { Spacing = 12 };

		public List<(Entry Reps, Entry Kg)> RowEntries { get; } = [];

		public List<CheckBox> RowDoneBoxes { get; } = [];

		public List<double?> RowKgHints { get; } = [];
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
			if (_sessionUiReady && _tpl is not null)
				return;

			_blocks.Clear();
			_currentExerciseIndex = 0;

			var repo = ServiceHelper.Services.GetRequiredService<IRhythmoRepository>();
			var activeProfileId = ServiceHelper.Services.GetRequiredService<ActiveProfileStore>().Get();

			_tpl = await repo.GetSessionTemplateAsync(_sid).ConfigureAwait(true);

			if (_tpl is null)
			{
				await DisplayAlertAsync("Séance", "Introuvable.", "OK").ConfigureAwait(false);
				await UiShellNavigate.GoAsync("..").ConfigureAwait(false);
				return;
			}

			_profile = await repo.GetProfileAsync(activeProfileId).ConfigureAwait(true)
			           ?? throw new InvalidOperationException("Profil introuvable.");

			SessionMetaSmall.Text =
				$"{_tpl.Title} · {_profile.DisplayName} · {(_profile.BiologicalSex == BiologicalSex.Male ? "homme" : "femme")} · {FormatKg(_profile.WeightKg)}";

			SessionLastSnapshotRow? snapRow = await repo.GetSessionSnapshotAsync(_sid).ConfigureAwait(true);
			var localDraft = _draftStore.TryLoad(activeProfileId);
			var useDraft = localDraft is not null && localDraft.SessionId == _sid;
			Dictionary<Guid, WorkoutDraftExerciseDto>? draftByExercise = useDraft
				? localDraft!.Exercises.ToDictionary(e => e.ExerciseId)
				: null;

			Dictionary<Guid, List<(int reps, double weight)>>? prefs = null;
			if (!useDraft && !string.IsNullOrWhiteSpace(snapRow?.Json))
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

			var allWeights = new Dictionary<Guid, double>();
			var tplLines = await repo.ListSessionExercisesAsync(_sid).ConfigureAwait(true);
			var weightRows = await Task.WhenAll(tplLines.Select(line =>
				repo.GetLastWeightAsync(activeProfileId, line.ExerciseId))).ConfigureAwait(true);
			for (var i = 0; i < tplLines.Count; i++)
			{
				if (weightRows[i] is { } w)
					allWeights[tplLines[i].ExerciseId] = w.WeightKg;
			}

			var hints = allWeights;
			var exerciseById = (await repo.ListExercisesAsync().ConfigureAwait(true))
				.ToDictionary(x => x.Id);

			await MainThread.InvokeOnMainThreadAsync(() =>
			{
				foreach (var line in tplLines)
				{
					if (!exerciseById.TryGetValue(line.ExerciseId, out var cx))
						continue;

					hints.TryGetValue(cx.Id, out var hintKg);

					var prefsList = !useDraft && prefs is not null && prefs.TryGetValue(cx.Id, out var pl)
						? pl
						: null;

					var draftEx = useDraft && draftByExercise!.TryGetValue(cx.Id, out var dex) ? dex : null;

					var blk = new ExerciseUi
					{
						ExerciseId = cx.Id,
						Title = cx.NameFr,
						Category = string.IsNullOrWhiteSpace(cx.Category) ? "Mixte" : cx.Category,
						Met = cx.MetApprox,
						HintKg = hintKg,
						TemplateLine = line
					};

					if (draftEx is { Sets.Count: > 0 })
					{
						for (var i = 0; i < draftEx.Sets.Count; i++)
						{
							var ds = draftEx.Sets[i];
							AddSetRow(blk, ds.RepsText, ds.KgText, null, ds.IsDone);
						}
					}
					else
					{
						var setCount = prefsList?.Count > 0 ? prefsList.Count : line.TargetSets;
						for (var i = 0; i < setCount; i++)
						{
							var repsTxt = prefsList is not null && i < prefsList.Count
								? prefsList[i].reps.ToString(CultureInfo.InvariantCulture)
								: (line.TargetReps?.ToString(CultureInfo.InvariantCulture) ?? "10");

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

					_blocks.Add(blk);
				}

				if (_blocks.Count > 0)
				{
					_draftStartedUtc = useDraft ? localDraft!.StartedUtc : DateTime.UtcNow;
					var exerciseIndex = useDraft
						? Math.Clamp(localDraft!.CurrentExerciseIndex, 0, _blocks.Count - 1)
						: 0;
					DisplayExercise(exerciseIndex);
					if (!useDraft)
						PersistDraftNow(activeProfileId);
				}
				else
				{
					ExerciseProgressBar.Progress = 0;
					ExerciseCounterLabel.Text = "0 / 0";
					ExerciseTitleLabel.Text = "";
					ExerciseCategoryLabel.Text = "";
				}

				_sessionUiReady = _blocks.Count > 0;
			}).ConfigureAwait(true);

			if (_blocks.Count == 0)
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
			return;

		index = Math.Clamp(index, 0, _blocks.Count - 1);
		_currentExerciseIndex = index;

		var blk = _blocks[index];
		ExerciseTitleLabel.Text = blk.Title;
		ExerciseCategoryLabel.Text = blk.Category.ToUpperInvariant();
		ExerciseCounterLabel.Text = $"Exercice {index + 1} sur {_blocks.Count}";
		ExerciseProgressBar.Progress = (index + 1d) / _blocks.Count;

		PrevExerciseBtn.IsEnabled = index > 0;
		NextExerciseBtn.IsEnabled = index < _blocks.Count - 1;

		ActiveExerciseRoot.Children.Clear();
		ActiveExerciseRoot.Children.Add(BuildSetsHeader());
		ActiveExerciseRoot.Children.Add(blk.RowsWrapper);
		ScheduleDraftSave();
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
				H("✓", 36)
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
			Placeholder = "kg",
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

		var row = new HorizontalStackLayout
		{
			Spacing = 12,
			VerticalOptions = LayoutOptions.Center,
			Children = { idxLbl, kgRegion, repsEntry, doneCb }
		};

		blk.RowsWrapper.Children.Add(row);
		blk.RowEntries.Add((repsEntry, kgEntry));
		blk.RowDoneBoxes.Add(doneCb);
		blk.RowKgHints.Add(lastSessionKgHint);
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
		if (_tpl is null || _profile is null)
			return;

		FinalizeBtn.IsEnabled = false;
		try
		{
			var repo = ServiceHelper.Services.GetRequiredService<IRhythmoRepository>();
			var auth = ServiceHelper.Services.GetRequiredService<SupabaseAuthService>();
			var activeProfileId =
				ServiceHelper.Services.GetRequiredService<ActiveProfileStore>().Get();
			var prepared = BuildPreparedWorkout();
			if (prepared is null)
			{
				await DisplayAlertAsync("Validation", "Aucune série complète trouvée (reps et kg positifs).", "OK")
					.ConfigureAwait(true);
				FinalizeBtn.IsEnabled = true;
				return;
			}

			if (!await EnsureSessionForSaveAsync(auth).ConfigureAwait(true))
			{
				FinalizeBtn.IsEnabled = true;
				return;
			}

			await SavePreparedWorkoutAsync(repo, activeProfileId, prepared).ConfigureAwait(false);

			_sessionUiReady = false;
			var kcalRounded = Math.Round(prepared.Calories);

			await MainThread.InvokeOnMainThreadAsync(async () =>
			{
				await RhythmSuccessDialog.ShowAsync(
					this,
					$"Séance enregistrée · {kcalRounded} kcal (indicatif).")
					.ConfigureAwait(true);
				await UiShellNavigate.GoAsync("..").ConfigureAwait(true);
			}).ConfigureAwait(true);
		}
		catch (Exception ex) when (SupabaseAuthService.RequiresReauthentication(ex))
		{
			await MainThread.InvokeOnMainThreadAsync(async () =>
			{
				FinalizeBtn.IsEnabled = true;
				await RhythmAlertDialog.ShowAsync(
					this,
					"Session expirée",
					"La séance n'a pas été enregistrée. Reconnecte-toi puis réessaie : tes charges sont toujours affichées à l'écran.",
					isError: true).ConfigureAwait(true);
			}).ConfigureAwait(true);
		}
		catch (Exception ex)
		{
			await MainThread.InvokeOnMainThreadAsync(async () =>
			{
				FinalizeBtn.IsEnabled = true;
				await _dev.TryShowSafeAsync(ex, nameof(OnFinalizeClicked)).ConfigureAwait(true);
			}).ConfigureAwait(true);
		}
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

	private async Task<bool> EnsureSessionForSaveAsync(SupabaseAuthService auth)
	{
		try
		{
			await auth.EnsureSessionFreshAsync().ConfigureAwait(false);
			if (auth.IsSignedIn)
				return true;
		}
		catch (Exception ex) when (SupabaseAuthService.RequiresReauthentication(ex))
		{
			CrashLogWriter.TryAppend(nameof(EnsureSessionForSaveAsync), ex);
		}

		while (true)
		{
			var relog = await MainThread.InvokeOnMainThreadAsync(() =>
				RhythmReauthDialog.ShowAsync(this, auth.CurrentUserEmail)).ConfigureAwait(true);
			if (!relog.Confirmed)
				return false;

			var (ok, err) = await auth.SignInWithPasswordAsync(relog.Email, relog.Password).ConfigureAwait(false);
			if (!ok)
			{
				await MainThread.InvokeOnMainThreadAsync(async () =>
				{
					await RhythmAlertDialog.ShowAsync(
						this,
						"Connexion refusée",
						err ?? "Impossible de se reconnecter.",
						isError: true).ConfigureAwait(true);
				}).ConfigureAwait(true);
				continue;
			}

			try
			{
				await auth.EnsureSessionFreshAsync().ConfigureAwait(false);
				if (auth.IsSignedIn)
					return true;
			}
			catch (Exception ex) when (SupabaseAuthService.RequiresReauthentication(ex))
			{
				CrashLogWriter.TryAppend(nameof(EnsureSessionForSaveAsync) + ".RefreshAfterRelog", ex);
			}

			await MainThread.InvokeOnMainThreadAsync(async () =>
			{
				await RhythmAlertDialog.ShowAsync(
					this,
					"Session indisponible",
					"La reconnexion n'a pas suffi. Réessaie dans un instant.",
					isError: true).ConfigureAwait(true);
			}).ConfigureAwait(true);
		}
	}

	private async Task SavePreparedWorkoutAsync(
		IRhythmoRepository repo,
		Guid activeProfileId,
		PreparedWorkout prepared)
	{
		foreach (var stat in prepared.ExerciseStats)
		{
			await repo.UpsertDailyMaxKgAsync(
				activeProfileId,
				stat.ExerciseId,
				prepared.PerformanceLocalDate,
				stat.MaxKg).ConfigureAwait(false);

			await repo.UpsertLastWeightAsync(new ExerciseLastWeightRow
			{
				ProfileId = activeProfileId,
				ExerciseId = stat.ExerciseId,
				WeightKg = stat.LastKg,
				UpdatedUtc = DateTime.UtcNow
			}).ConfigureAwait(false);
		}

		var remote = new WorkoutCompletedRequest(
			prepared.Utc,
			prepared.Calories,
			prepared.Minutes,
			null,
			prepared.Exercises);
		var workoutId = Guid.NewGuid();
		var savedWorkoutId = await repo.AddCompletedWorkoutAsync(new CompletedWorkoutRow
		{
			Id = workoutId,
			ProfileId = activeProfileId,
			CompletedUtc = prepared.Utc,
			CaloriesRounded = prepared.Calories,
			SessionTitle = _tpl!.Title,
			SourceSessionTemplateId = _sid,
			PayloadJson = CompletedWorkoutSnapshot.SerializeRequest(remote)
		}).ConfigureAwait(false);

		try
		{
			var prService = ServiceHelper.Services.GetRequiredService<PersonalRecordService>();
			await prService.ProcessCompletedWorkoutAsync(
				repo,
				activeProfileId,
				savedWorkoutId,
				prepared.Utc,
				prepared.Exercises,
				prepared.TotalFilledSets).ConfigureAwait(false);
		}
		catch (Exception prEx) when (prEx is not OperationCanceledException)
		{
			await _dev.TryShowSafeAsync(prEx, nameof(OnFinalizeClicked) + ".Pr").ConfigureAwait(false);
		}

		ServiceHelper.Services.GetRequiredService<SocialHubService>().InvalidateCache();

		var json = JsonSerializer.Serialize(new LastRunEnvelope(prepared.Exercises), JsonSnake);
		await repo.UpsertSessionSnapshotAsync(new SessionLastSnapshotRow
		{
			SessionId = _sid,
			Json = json,
			SavedUtc = prepared.Utc
		}).ConfigureAwait(false);

		_draftStore.Clear(activeProfileId);
	}

	void ScheduleDraftSave()
	{
		if (_tpl is null || !_sessionUiReady)
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
		if (_tpl is null || !_sessionUiReady)
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
			_tpl!.Title,
			_draftStartedUtc,
			DateTime.UtcNow,
			_currentExerciseIndex,
			exercises);
	}

	private static string FormatKg(double w) =>
		w.ToString("0.#", CultureInfo.InvariantCulture) + " kg";
}
