using Rhythmo.Mobile.Data;
using Rhythmo.Mobile.Infrastructure;
using Rhythmo.Mobile.Services;
using Rhythmo.Mobile.Theme;
using Rhythmo.Shared.Ranking;

namespace Rhythmo.Mobile;

public partial class RankQuestsPage : ContentPage
{
	public RankQuestsPage() => InitializeComponent();

	private async void OnBackClicked(object? sender, EventArgs e) =>
		await UiShellNavigate.GoAsync("..").ConfigureAwait(false);

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		await QuestResumeDialog.TryPromptIfNeededAsync().ConfigureAwait(true);
		await ReloadAsync().ConfigureAwait(true);
	}

	private async Task ReloadAsync()
	{
		Root.Children.Clear();
		var repo = ServiceHelper.Services.GetRequiredService<IRhythmoRepository>();
		var profileId = ServiceHelper.Services.GetRequiredService<ActiveProfileStore>().Get();
		await ServiceHelper.Services.GetRequiredService<RankQuestService>()
			.ExpireStaleAsync(profileId).ConfigureAwait(true);

		var exercises = (await repo.ListExercisesAsync().ConfigureAwait(true)).ToDictionary(e => e.Id);
		var defs = RankingExerciseIndex.Map(exercises.Values);
		var ranks = await repo.ListProfileExerciseRanksAsync(profileId).ConfigureAwait(true);
		var attempts = await repo.ListQuestAttemptsAsync(profileId).ConfigureAwait(true);
		var today = DateOnly.FromDateTime(DateTime.Now);
		var newCutoff = DateTime.UtcNow.AddHours(-36);

		Root.Children.Add(new Label
		{
			Style = (Style)Application.Current!.Resources["RhythmCaption"],
			Text = "Un défi à la fois."
		});

		var inProgress = attempts.FirstOrDefault(a => a.Status == "in_progress");
		var cards = new List<QuestCardModel>();
		if (inProgress is not null)
		{
			exercises.TryGetValue(inProgress.ExerciseId, out var runningEx);
			var remaining = QuestRules.Remaining(inProgress.StartedUtc, inProgress.ExpiresUtc, DateTime.UtcNow);
			var clock = remaining <= TimeSpan.Zero
				? "Reprendre"
				: $"Il reste {QuestRules.FormatRemaining(remaining)}";
			cards.Add(new QuestCardModel(
				inProgress.ExerciseId,
				runningEx?.NameFr ?? "Exercice",
				inProgress.TargetRank,
				null,
				clock,
				QuestCardState.InProgress));
		}

		foreach (var rank in ranks.Where(r => r.AvailableQuestRank is not null))
		{
			if (inProgress is not null && rank.ExerciseId == inProgress.ExerciseId)
				continue;

			exercises.TryGetValue(rank.ExerciseId, out var ex);
			defs.TryGetValue(rank.ExerciseId, out var def);
			var attemptedToday = attempts.Any(a => a.ExerciseId == rank.ExerciseId && a.LocalDate == today);
			if (attemptedToday)
				continue;

			var isNew = rank.QuestUnlockedUtc is { } unlocked && unlocked >= newCutoff;
			var objective = ObjectiveText(rank, def);
			cards.Add(new QuestCardModel(
				rank.ExerciseId,
				ex?.NameFr ?? "Exercice",
				rank.AvailableQuestRank!.Value,
				rank.ValidatedRank,
				objective,
				isNew ? QuestCardState.New : QuestCardState.Available));
		}

		foreach (var attempt in attempts.Where(a => a.LocalDate == today && a.Status == "succeeded"))
		{
			exercises.TryGetValue(attempt.ExerciseId, out var ex);
			cards.Add(new QuestCardModel(
				attempt.ExerciseId,
				ex?.NameFr ?? "Exercice",
				attempt.TargetRank,
				attempt.TargetRank - 1,
				null,
				QuestCardState.Succeeded));
		}

		foreach (var attempt in attempts.Where(a =>
			         a.LocalDate == today &&
			         (a.Status == "failed_expired" || a.Status == "failed_user")))
		{
			exercises.TryGetValue(attempt.ExerciseId, out var ex);
			cards.Add(new QuestCardModel(
				attempt.ExerciseId,
				ex?.NameFr ?? "Exercice",
				attempt.TargetRank,
				null,
				null,
				QuestCardState.Failed));
		}

		var newCount = cards.Count(c => c.State == QuestCardState.New);
		if (newCount > 0)
		{
			Root.Children.Add(new Border
			{
				Padding = new Thickness(12, 6),
				StrokeThickness = 0,
				HorizontalOptions = LayoutOptions.Start,
				BackgroundColor = RankPalette.For(cards.First(c => c.State == QuestCardState.New).TargetRank)
					.WithAlpha(0.18f),
				StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 12 },
				Content = new Label
				{
					Text = newCount == 1 ? "1 nouvelle" : $"{newCount} nouvelles",
					FontFamily = "OpenSansSemibold",
					FontSize = 13,
					TextColor = RhythmColors.TextPrimary
				}
			});
		}

		if (cards.Count == 0)
		{
			Root.Children.Add(new Label
			{
				Text = "Aucune quête pour le moment. Termine une séance pour en débloquer.",
				TextColor = RhythmColors.TextSecondary,
				LineBreakMode = LineBreakMode.WordWrap
			});
			return;
		}

		foreach (var card in cards
			         .OrderBy(c => c.State)
			         .ThenByDescending(c => c.TargetRank))
		{
			var model = card;
			Root.Children.Add(QuestCards.Build(
				model,
				model.State is QuestCardState.New or QuestCardState.Available or QuestCardState.InProgress
					? () => _ = RankUi.GoQuest(inProgress is not null ? inProgress.ExerciseId : model.ExerciseId)
					: null));
		}
	}

	private static string? ObjectiveText(ProfileExerciseRankRow rank, ExerciseRankingDef? def)
	{
		if (rank.R10Used is null or <= 0 || rank.AvailableQuestRank is null || def is null)
			return null;
		var option = QuestObjectivePlanner.ForRank(
			rank.R10Used.Value,
			rank.AvailableQuestRank.Value,
			def.MeasurementType ?? MeasurementType.FiveRm,
			def.LoadMode);
		return QuestObjectiveUi.FormatSet(option.Primary, def.LoadMode);
	}
}
