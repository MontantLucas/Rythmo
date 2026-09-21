using System.Globalization;
using Microsoft.Maui.Controls.Shapes;
using Rhythmo.Mobile.Data;
using Rhythmo.Mobile.Infrastructure;
using Rhythmo.Mobile.Services;
using Rhythmo.Mobile.Social;
using Rhythmo.Mobile.Theme;
using Rhythmo.Shared.Ranking;

namespace Rhythmo.Mobile;

[QueryProperty(nameof(ExerciseIdEncoded), "ExerciseId")]
public partial class RankQuestPage : ContentPage
{
	private Guid _exerciseId;
	private RankQuestAttemptRow? _attempt;
	private IDispatcherTimer? _timer;
	private QuestPrepState? _prep;
	private bool _loaded;
	private bool _leaving;
	private bool _settling;

	private sealed record QuestPrepState(
		string ExerciseName,
		int? ValidatedRank,
		int SelectedTargetRank,
		IReadOnlyList<QuestTargetOption> Targets,
		LoadMode LoadMode,
		double R10Used)
	{
		public QuestTargetOption Selected =>
			Targets.First(t => t.Rank == SelectedTargetRank);
	}

	private readonly List<(double Kg, int Reps, bool Reached)> _logged = [];

	public RankQuestPage()
	{
		InitializeComponent();
		Shell.SetBackButtonBehavior(this, new BackButtonBehavior
		{
			Command = new Command(async () => await LeaveAsync())
		});
	}

	private async void OnNavBackClicked(object? sender, EventArgs e) =>
		await LeaveAsync().ConfigureAwait(false);

	protected override bool OnBackButtonPressed()
	{
		_ = LeaveAsync();
		return true;
	}

	private async Task LeaveAsync()
	{
		if (_leaving || _settling)
			return;

		if (_attempt is null || !IsActive(_attempt))
		{
			await ExitAsync().ConfigureAwait(false);
			return;
		}

		var resume = await QuestResumeDialog
			.AskAsync(this, _prep?.ExerciseName ?? "ce défi")
			.ConfigureAwait(true);
		if (resume)
			return;

		_timer?.Stop();
		await ServiceHelper.Services.GetRequiredService<RankQuestService>()
			.AbandonAsync(_attempt).ConfigureAwait(true);
		_attempt = null;
		await ExitAsync().ConfigureAwait(false);
	}

	private async Task ExitAsync()
	{
		if (_leaving)
			return;
		_leaving = true;
		await UiShellNavigate.GoAsync("..").ConfigureAwait(false);
	}

	private static bool IsActive(RankQuestAttemptRow attempt) =>
		string.Equals(attempt.Status, "in_progress", StringComparison.Ordinal);

	public string ExerciseIdEncoded
	{
		set => _exerciseId = Guid.TryParse(Uri.UnescapeDataString(value ?? ""), out var id) ? id : Guid.Empty;
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		if (_attempt is not null)
		{
			StartTimer();
			return;
		}

		if (_loaded && _prep is not null)
			return;

		await LoadAsync().ConfigureAwait(true);
	}

	protected override void OnDisappearing()
	{
		_timer?.Stop();
		base.OnDisappearing();
	}

	private async Task LoadAsync()
	{
		var profileId = ServiceHelper.Services.GetRequiredService<ActiveProfileStore>().Get();
		var quests = ServiceHelper.Services.GetRequiredService<RankQuestService>();
		await quests.ExpireStaleAsync(profileId).ConfigureAwait(true);

		var repo = ServiceHelper.Services.GetRequiredService<IRhythmoRepository>();
		var inProgress = await repo.GetInProgressQuestAsync(profileId).ConfigureAwait(true);
		if (inProgress is not null && inProgress.ExerciseId != _exerciseId)
			_exerciseId = inProgress.ExerciseId;

		var exercises = await repo.ListExercisesAsync().ConfigureAwait(true);
		var ex = exercises.FirstOrDefault(e => e.Id == _exerciseId);
		var defs = RankingExerciseIndex.Map(exercises);
		defs.TryGetValue(_exerciseId, out var def);
		var ranks = await repo.ListProfileExerciseRanksAsync(profileId).ConfigureAwait(true);
		var rank = ranks.FirstOrDefault(r => r.ExerciseId == _exerciseId);

		if (ex is null || def is null || !def.IsClassifying || rank?.R10Used is null or <= 0)
		{
			await RhythmAlertDialog.ShowAsync(
				this,
				"Quête indisponible",
				"Les conditions ne sont plus réunies.",
				isError: true).ConfigureAwait(true);
			await UiShellNavigate.GoAsync("..").ConfigureAwait(false);
			return;
		}

		var measurement = def.MeasurementType ?? MeasurementType.FiveRm;
		var targetRank = inProgress?.TargetRank
		                 ?? rank.AvailableQuestRank
		                 ?? QuestRules.Availability(rank.TheoreticalRank, rank.ValidatedRank).TargetRank;
		if (targetRank is null)
		{
			await RhythmAlertDialog.ShowAsync(
				this,
				"Quête indisponible",
				"Aucune cible disponible pour cet exercice.",
				isError: true).ConfigureAwait(true);
			await UiShellNavigate.GoAsync("..").ConfigureAwait(false);
			return;
		}

		var targets = new[]
		{
			QuestObjectivePlanner.ForRank(rank.R10Used.Value, targetRank.Value, measurement, def.LoadMode)
		};
		_prep = new QuestPrepState(
			ex.NameFr,
			rank.ValidatedRank,
			targetRank.Value,
			targets,
			def.LoadMode,
			rank.R10Used.Value);
		BindPrep();
		KgEntry.Placeholder = RankingExerciseIndex.WeightPlaceholder(def.LoadMode);
		_loaded = true;

		if (inProgress is not null)
		{
			_attempt = inProgress;
			ShowActive();
			StartTimer();
			return;
		}

		ShowPrep();
	}

	private void BindPrep()
	{
		if (_prep is null)
			return;

		var target = _prep.Selected;
		var code = RankLabels.Code(target.Rank);
		var title = RankLabels.Title(target.Rank);
		var rankColor = RankPalette.For(target.Rank);
		PrepRankCodeLabel.Text = code;
		PrepRankCodeLabel.TextColor = rankColor;
		PrepExerciseLabel.Text = _prep.ExerciseName.ToUpperInvariant();
		Title = _prep.ExerciseName;
		PrepRankTitleLabel.Text = title.ToUpperInvariant();
		PrepRankTitleLabel.TextColor = rankColor;
		PrepObjectiveLabel.Text = QuestObjectiveUi.FormatSet(target.Primary, _prep.LoadMode);
		PrepLoadHintLabel.IsVisible = false;

		PrepEquivalencesHost.Children.Clear();
		var showAlts = target.Rank < QuestRules.ExactObjectiveFromRank && target.Equivalences.Count > 0;
		PrepEquivalencesHost.IsVisible = showAlts;
		PrepFlexCaption.IsVisible = showAlts;
		if (showAlts)
		{
			PrepEquivalencesHost.Children.Add(new Label
			{
				Style = (Style)Application.Current!.Resources["RhythmCaption"],
				Text = "Autre possibilité"
			});
			foreach (var eq in target.Equivalences)
			{
				PrepEquivalencesHost.Children.Add(new Label
				{
					FontSize = 16,
					TextColor = RhythmColors.TextPrimary,
					Text = QuestObjectiveUi.FormatSet(eq, _prep.LoadMode)
				});
			}
		}

		ActiveExerciseLabel.Text = _prep.ExerciseName.ToUpperInvariant();
		ActiveRankLabel.Text = code;
		ActiveRankLabel.TextColor = rankColor;
		ActiveObjectiveLabel.Text = QuestObjectiveUi.FormatSet(target.Primary, _prep.LoadMode);

		ActiveEquivalencesHost.Children.Clear();
		ActiveEquivalencesHost.IsVisible = showAlts;
		if (showAlts)
		{
			ActiveEquivalencesHost.Children.Add(new Label
			{
				Style = (Style)Application.Current!.Resources["RhythmCaption"],
				Text = "Autre possibilité"
			});
			foreach (var eq in target.Equivalences)
			{
				ActiveEquivalencesHost.Children.Add(new Label
				{
					FontSize = 15,
					TextColor = RhythmColors.TextPrimary,
					Text = QuestObjectiveUi.FormatSet(eq, _prep.LoadMode)
				});
			}
		}
	}

	private void ShowPrep()
	{
		PrepHost.IsVisible = true;
		ActiveHost.IsVisible = false;
	}

	private void ShowActive()
	{
		PrepHost.IsVisible = false;
		ActiveHost.IsVisible = true;
	}

	private async void OnStartQuestClicked(object? sender, EventArgs e)
	{
		if (_attempt is not null)
			return;

		StartQuestBtn.IsEnabled = false;
		try
		{
			var profileId = ServiceHelper.Services.GetRequiredService<ActiveProfileStore>().Get();
			var result = await ServiceHelper.Services.GetRequiredService<RankQuestService>()
				.TryStartAsync(profileId, _exerciseId).ConfigureAwait(true);
			if (!result.Ok || result.Attempt is null)
			{
				await RhythmAlertDialog.ShowAsync(
					this,
					"Quête indisponible",
					result.Error ?? "Les conditions ne sont plus réunies.",
					isError: true).ConfigureAwait(true);
				return;
			}

			_attempt = result.Attempt;
			ShowActive();
			StartTimer();
		}
		finally
		{
			StartQuestBtn.IsEnabled = true;
		}
	}

	private void StartTimer()
	{
		_timer?.Stop();
		_timer = Dispatcher.CreateTimer();
		_timer.Interval = TimeSpan.FromSeconds(1);
		_timer.Tick += (_, _) => _ = TickAsync();
		_timer.Start();
		_ = TickAsync();
	}

	private async Task TickAsync()
	{
		if (_attempt is null)
			return;
		var remaining = QuestRules.Remaining(_attempt.StartedUtc, _attempt.ExpiresUtc, DateTime.UtcNow);
		if (remaining <= TimeSpan.Zero)
		{
			_timer?.Stop();
			TimerLabel.Text = "00:00";
			await ServiceHelper.Services.GetRequiredService<RankQuestService>()
				.FailAsync(_attempt, "failed_expired").ConfigureAwait(true);
			_attempt = null;
			await RhythmAlertDialog.ShowAsync(this, "Temps écoulé", "Quête échouée. Réessaie demain.", isError: true)
				.ConfigureAwait(true);
			await ExitAsync().ConfigureAwait(false);
			return;
		}

		TimerLabel.Text = QuestRules.FormatRemaining(remaining);
	}

	private void OnLogSetClicked(object? sender, EventArgs e)
	{
		if (!TryReadSet(out var kg, out var reps))
			return;
		var reached = _prep is not null &&
		              QuestRules.CompletesDisplayedObjective(
			              _prep.SelectedTargetRank,
			              _prep.Selected.Primary,
			              kg,
			              reps,
			              _prep.R10Used);
		_logged.Add((kg, reps, reached));

		RenderLoggedSets();
		if (_logged.Any(s => s.Reached))
		{
			SuccessBtn.BackgroundColor = RhythmColors.Success;
			SuccessBtn.TextColor = RhythmColors.Bg;
		}
		KgEntry.Text = "";
		RepsEntry.Text = "";
	}

	private void RenderLoggedSets()
	{
		LoggedSetsHost.Children.Clear();
		LoggedEmptyLabel.IsVisible = _logged.Count == 0;
		foreach (var set in _logged)
		{
			var reached = set.Reached;
			var lines = new VerticalStackLayout { Spacing = 2 };
			lines.Children.Add(new Label
			{
				FontFamily = "OpenSansSemibold",
				FontSize = 16,
				TextColor = reached ? RhythmColors.Success : RhythmColors.TextPrimary,
				Text = QuestObjectiveUi.FormatSet(set.Kg, set.Reps, _prep?.LoadMode ?? LoadMode.Total, includeRepsWord: false)
			});
			if (reached)
			{
				lines.Children.Add(new Label
				{
					FontFamily = "OpenSansSemibold",
					FontSize = 13,
					TextColor = RhythmColors.Success,
					Text = "Objectif atteint"
				});
			}

			LoggedSetsHost.Children.Add(new Border
			{
				Padding = new Thickness(14, 12),
				StrokeThickness = reached ? 1.5 : 0,
				Stroke = reached ? RhythmColors.Success : Colors.Transparent,
				BackgroundColor = reached ? Color.FromArgb("#1A22C55E") : RhythmColors.Surface2,
				StrokeShape = new RoundRectangle { CornerRadius = 14 },
				Content = lines
			});
		}
	}

	private async void OnSuccessClicked(object? sender, EventArgs e)
	{
		if (_settling || _attempt is null)
			return;
		var win = _logged.LastOrDefault(s => s.Reached);
		if (!win.Reached)
		{
			await RhythmAlertDialog.ShowAsync(this, "Série manquante", "Enregistre au moins une série qui atteint l’objectif.")
				.ConfigureAwait(true);
			return;
		}

		var heaviest = _logged.OrderByDescending(s => s.Kg).ThenByDescending(s => s.Reps).First();

		_settling = true;
		_timer?.Stop();
		_attempt.Status = "succeeded";
		try
		{
			var quests = ServiceHelper.Services.GetRequiredService<RankQuestService>();
			var ok = await quests.TryCompleteSuccessAsync(_attempt, win.Kg, win.Reps).ConfigureAwait(true);
			if (!ok)
			{
				_attempt.Status = "in_progress";
				_settling = false;
				await RhythmAlertDialog.ShowAsync(
					this,
					"Seuil non atteint",
					"Cette série ne valide pas encore le rang ciblé.",
					isError: true).ConfigureAwait(true);
				return;
			}

			try
			{
				var repo = ServiceHelper.Services.GetRequiredService<IRhythmoRepository>();
				var profileId = ServiceHelper.Services.GetRequiredService<ActiveProfileStore>().Get();
				await ServiceHelper.Services.GetRequiredService<PersonalRecordService>()
					.ProcessQuestSetAsync(repo, profileId, _exerciseId, heaviest.Kg, heaviest.Reps)
					.ConfigureAwait(true);
			}
			catch
			{
				// PR non bloquant
			}

			_attempt = null;
			await RhythmSuccessDialog.ShowAsync(this, "Rang validé. Prochaine quête demain.").ConfigureAwait(true);
			await ExitAsync().ConfigureAwait(false);
		}
		finally
		{
			_settling = false;
		}
	}

	private async void OnAbandonClicked(object? sender, EventArgs e)
	{
		if (_attempt is null)
			return;
		_timer?.Stop();
		await ServiceHelper.Services.GetRequiredService<RankQuestService>()
			.AbandonAsync(_attempt).ConfigureAwait(true);
		_attempt = null;
		await ExitAsync().ConfigureAwait(false);
	}

	private bool TryReadSet(out double kg, out int reps)
	{
		kg = 0;
		reps = 0;
		return double.TryParse(KgEntry.Text?.Replace(",", "."), NumberStyles.Float,
			       CultureInfo.InvariantCulture, out kg) && kg > 0
		       && int.TryParse(RepsEntry.Text, out reps) && reps > 0;
	}
}
