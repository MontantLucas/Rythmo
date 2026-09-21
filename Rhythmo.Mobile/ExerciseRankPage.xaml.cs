using Rhythmo.Mobile.Infrastructure;
using Rhythmo.Mobile.Services;
using Rhythmo.Mobile.Theme;
using Rhythmo.Shared.Ranking;

namespace Rhythmo.Mobile;

[QueryProperty(nameof(ExerciseIdEncoded), "ExerciseId")]
public partial class ExerciseRankPage : ContentPage
{
	private Guid _exerciseId;

	public ExerciseRankPage() => InitializeComponent();

	public string ExerciseIdEncoded
	{
		set => _exerciseId = Guid.TryParse(Uri.UnescapeDataString(value ?? ""), out var id) ? id : Guid.Empty;
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		await ReloadAsync().ConfigureAwait(true);
	}

	private async Task ReloadAsync()
	{
		Root.Children.Clear();
		var repo = ServiceHelper.Services.GetRequiredService<IRhythmoRepository>();
		var profileId = ServiceHelper.Services.GetRequiredService<ActiveProfileStore>().Get();
		var exercises = await repo.ListExercisesAsync().ConfigureAwait(true);
		var ex = exercises.FirstOrDefault(e => e.Id == _exerciseId);
		if (ex is null)
			return;

		Title = ex.NameFr;
		var defs = RankingExerciseIndex.Map(exercises);
		defs.TryGetValue(_exerciseId, out var def);
		var rank = (await repo.ListProfileExerciseRanksAsync(profileId).ConfigureAwait(true))
			.FirstOrDefault(r => r.ExerciseId == _exerciseId);
		var attempts = (await repo.ListQuestAttemptsAsync(profileId).ConfigureAwait(true))
			.Where(a => a.ExerciseId == _exerciseId)
			.OrderByDescending(a => a.StartedUtc)
			.ToList();
		var today = DateOnly.FromDateTime(DateTime.Now);
		var attemptedToday = attempts.Any(a => a.LocalDate == today);

		Root.Children.Add(new Label
		{
			Style = (Style)Application.Current!.Resources["RhythmH1"],
			Text = ex.NameFr
		});
		Root.Children.Add(new Label
		{
			Style = (Style)Application.Current!.Resources["RhythmCaption"],
			Text = def is null
				? "Exercice catalogue"
				: $"{Measure(def)} · {RankingExerciseIndex.WeightPlaceholder(def.LoadMode)}"
				  + (def.IsClassifying ? $" · {def.Source} · {def.Comparability}" : " · non classifiant")
		});
		Root.Children.Add(new Label
		{
			FontFamily = "OpenSansSemibold",
			FontSize = 22,
			TextColor = RankPalette.For(rank?.ValidatedRank),
			Text = $"Validé {RankLabels.Display(rank?.ValidatedRank)}"
		});
		Root.Children.Add(new Label
		{
			TextColor = RhythmColors.TextSecondary,
			Text = rank?.TheoreticalRank is null
				? "Théorique : non classé (sous le seuil R1 ou sans perf)"
				: $"Théorique {RankLabels.Display(rank.TheoreticalRank)}"
		});

		var adHocBtn = new Button
		{
			Text = "À la volée",
			Style = (Style)Application.Current!.Resources["RhythmBtnGhost"],
			HorizontalOptions = LayoutOptions.Start
		};
		adHocBtn.Clicked += async (_, _) =>
			await AdHocWorkout.StartOrResumeAsync(this, _exerciseId).ConfigureAwait(true);
		Root.Children.Add(adHocBtn);

		if (rank?.AvailableQuestRank is { } target && !attemptedToday)
		{
			var btn = new Button { Text = $"Lancer la quête R{target}" };
			btn.Clicked += async (_, _) => await RankUi.GoQuest(_exerciseId);
			Root.Children.Add(btn);
		}
		else if (attemptedToday)
		{
			Root.Children.Add(new Label
			{
				Text = "Tentative déjà effectuée aujourd’hui — reviens demain.",
				TextColor = RhythmColors.TextSecondary
			});
		}

		Root.Children.Add(new Label { Style = (Style)Application.Current!.Resources["RhythmH3"], Text = "Historique des tentatives" });
		if (attempts.Count == 0)
		{
			Root.Children.Add(new Label { Text = "Aucune tentative.", TextColor = RhythmColors.TextSecondary });
			return;
		}

		foreach (var a in attempts.Take(20))
		{
			var line = a.WeightKg is > 0 && a.Reps is > 0
				? $"{a.WeightKg:0.#} kg × {a.Reps}"
				: "—";
			Root.Children.Add(new Label
			{
				Text = $"{a.LocalDate:d} · R{a.TargetRank} · {a.Status} · {line}",
				TextColor = RhythmColors.TextSecondary
			});
		}
	}

	private static string Measure(ExerciseRankingDef def) => def.MeasurementType switch
	{
		MeasurementType.OneRm => "Référentiel 1RM",
		MeasurementType.FiveRm => "Référentiel 5RM",
		MeasurementType.TenRm => "Référentiel 10RM",
		_ => "Sans standard"
	};
}
