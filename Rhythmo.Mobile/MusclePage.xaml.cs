using Rhythmo.Mobile.Infrastructure;
using Rhythmo.Mobile.Services;
using Rhythmo.Mobile.Theme;
using Rhythmo.Shared.Ranking;

namespace Rhythmo.Mobile;

[QueryProperty(nameof(MuscleIdEncoded), "MuscleId")]
public partial class MusclePage : ContentPage
{
	private string _muscleId = "";

	public MusclePage() => InitializeComponent();

	public string MuscleIdEncoded
	{
		set => _muscleId = Uri.UnescapeDataString(value ?? "");
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		await ReloadAsync().ConfigureAwait(true);
	}

	private async Task ReloadAsync()
	{
		Root.Children.Clear();
		var muscle = MuscleIds.Muscles.FirstOrDefault(m => m.Id == _muscleId);
		if (muscle.Id is null)
			return;

		Title = muscle.NameFr;
		var repo = ServiceHelper.Services.GetRequiredService<IRhythmoRepository>();
		var profileId = ServiceHelper.Services.GetRequiredService<ActiveProfileStore>().Get();
		var exercises = await repo.ListExercisesAsync().ConfigureAwait(true);
		var defs = RankingExerciseIndex.Map(exercises);
		var ranks = (await repo.ListProfileExerciseRanksAsync(profileId).ConfigureAwait(true))
			.ToDictionary(r => r.ExerciseId);
		var attempts = await repo.ListQuestAttemptsAsync(profileId).ConfigureAwait(true);
		var today = DateOnly.FromDateTime(DateTime.Now);

		var snap = (await repo.ListMuscleRankSnapshotsAsync(profileId, MuscleIds.StandardVersionId)
			.ConfigureAwait(true)).FirstOrDefault(s => s.MuscleId == _muscleId);

		Root.Children.Add(new Label
		{
			Style = (Style)Application.Current!.Resources["RhythmH1"],
			Text = muscle.NameFr
		});
		Root.Children.Add(new Label
		{
			Style = (Style)Application.Current!.Resources["RhythmCaption"],
			Text = snap?.ValidatedRank is null
				? "Non évalué — un exercice Primary doit atteindre le seuil R1"
				: $"{RankLabels.Display(snap.ValidatedRank)} · théorique {RankLabels.Code(snap.TheoreticalRank)}"
		});

		Root.Children.Add(new Label { Style = (Style)Application.Current!.Resources["RhythmH3"], Text = "Primary" });
		foreach (var ex in exercises.Where(e =>
			         defs.TryGetValue(e.Id, out var d) && d.PrimaryMuscles.Contains(_muscleId)))
		{
			ranks.TryGetValue(ex.Id, out var rank);
			var attemptedToday = attempts.Any(a => a.ExerciseId == ex.Id && a.LocalDate == today);
			var subtitle = rank?.ValidatedRank is null
				? "Non classé"
				: $"Validé {RankLabels.Code(rank.ValidatedRank)} · théorique {RankLabels.Code(rank.TheoreticalRank)}"
				  + (rank.AvailableQuestRank is { } q && !attemptedToday ? $" · quête R{q}" : attemptedToday ? " · demain" : "");
			var id = ex.Id;
			Root.Children.Add(RankUi.RankCard(
				ex.NameFr,
				rank?.ValidatedRank,
				subtitle,
				() => _ = RankUi.GoExercise(id),
				() => _ = AdHocWorkout.StartOrResumeAsync(this, id)));
		}

		var secondaries = exercises.Where(e =>
			defs.TryGetValue(e.Id, out var d) && d.SecondaryMuscles.Contains(_muscleId) &&
			!d.PrimaryMuscles.Contains(_muscleId)).ToList();
		if (secondaries.Count == 0)
			return;

		Root.Children.Add(new Label
		{
			Style = (Style)Application.Current!.Resources["RhythmH3"],
			Text = "Secondary (non classant)"
		});
		foreach (var ex in secondaries)
			Root.Children.Add(RankUi.RankCard(ex.NameFr, null, "N’alimente pas ce ranking"));
	}
}
