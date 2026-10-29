using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Rhythmo.Mobile.Infrastructure;
using Rhythmo.Mobile.Services;
using Rhythmo.Mobile.Theme;
using Rhythmo.Shared.History;
using Rhythmo.Shared.Ranking;

namespace Rhythmo.Mobile;

public partial class HistoryPage : ContentPage
{
	private readonly TrainingPeriodStore _periods = ServiceHelper.Services.GetRequiredService<TrainingPeriodStore>();
	private bool _byExercise = true;
	private string? _groupFilter;
	private bool _loading;

	public HistoryPage()
	{
		InitializeComponent();
		HistoryRefresh.Command = new Command(() => _ = ReloadAsync());
		_periods.Changed += (_, _) => _ = ReloadAsync();
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		_ = ReloadAsync();
	}

	private void OnByExerciseClicked(object? sender, EventArgs e)
	{
		_byExercise = true;
		_ = ReloadAsync();
	}

	private void OnBySessionClicked(object? sender, EventArgs e)
	{
		_byExercise = false;
		_ = ReloadAsync();
	}

	private async Task ReloadAsync()
	{
		if (_loading)
			return;
		_loading = true;
		try
		{
			var today = DateOnly.FromDateTime(DateTime.Now);
			var window = _periods.Window(today);
			BuildPeriod(today, window);
			BuildFilters();
			StyleSegments();
			CleanBtn.IsVisible = !_byExercise;

			var repo = ServiceHelper.Services.GetRequiredService<IRhythmoRepository>();
			var profileId = ServiceHelper.Services.GetRequiredService<ActiveProfileStore>().Get();
			var since = window.Start.ToDateTime(TimeOnly.MinValue).ToUniversalTime().AddDays(-1);
			var rows = await repo.ListCompletedWorkoutsSinceAsync(profileId, since).ConfigureAwait(true);
			var catalog = await repo.ListExercisesAsync().ConfigureAwait(true);
			var prs = await repo.ListPrEventsForProfileAsync(profileId).ConfigureAwait(true);
			var byId = catalog.ToDictionary(e => e.Id);
			var agg = TrainingHistory.Build(
				rows.Select(HistoryMapping.ToHistory).ToList(),
				window,
				TimeZoneInfo.Local,
				id => Resolve(id, byId),
				prs.Select(p => new HistoryPr(p.ExerciseId, p.CompletedWorkoutId, p.Kind)).ToList(),
				_groupFilter);

			RangeLabel.Text = window.RangeLabel;
			IndicatorLabel.Text =
				$"{agg.SessionCount} séances · {agg.DistinctExerciseCount} exercices · {FormatKg(agg.VolumeKg)}";
			ContentHost.Children.Clear();
			if (_byExercise)
				BuildExerciseView(agg);
			else
				BuildSessionView(agg);
		}
		catch (Exception ex)
		{
			await UiGuard.ReportAsync(this, ex, nameof(ReloadAsync)).ConfigureAwait(true);
		}
		finally
		{
			_loading = false;
		}
	}

	private void BuildPeriod(DateOnly today, TrainingPeriodWindow window)
	{
		PeriodHost.Children.Clear();
		PeriodHost.Add(NavButton("‹", true, () => _periods.Previous(today)), 0);
		var chips = new HorizontalStackLayout { Spacing = 6, HorizontalOptions = LayoutOptions.Center };
		foreach (var kind in TrainingPeriodStore.Offered)
		{
			var selected = kind == _periods.Kind;
			var btn = new Button
			{
				Text = _periods.Chip(kind, today),
				FontSize = 13,
				Padding = new Thickness(10, 6),
				CornerRadius = 14,
				BackgroundColor = selected ? RhythmColors.Accent : RhythmColors.Surface2,
				TextColor = selected ? RhythmColors.Bg : RhythmColors.TextPrimary
			};
			var captured = kind;
			btn.Clicked += (_, _) => _periods.Select(captured);
			chips.Add(btn);
		}
		PeriodHost.Add(chips, 1);
		var next = NavButton("›", window.CanGoNext, () => _periods.Next(today));
		next.IsEnabled = window.CanGoNext;
		PeriodHost.Add(next, 2);
	}

	private static Button NavButton(string text, bool enabled, Action action)
	{
		var btn = new Button
		{
			Text = text,
			FontSize = 18,
			WidthRequest = 44,
			HeightRequest = 44,
			Padding = 0,
			BackgroundColor = RhythmColors.Surface2,
			TextColor = RhythmColors.TextPrimary,
			IsEnabled = enabled
		};
		btn.Clicked += (_, _) => action();
		return btn;
	}

	private void BuildFilters()
	{
		FilterHost.Children.Clear();
		AddFilter("Tous", null);
		foreach (var group in MuscleIds.Groups)
			AddFilter(group.NameFr, group.Id);
	}

	private void AddFilter(string label, string? groupId)
	{
		var selected = _groupFilter == groupId;
		var btn = new Button
		{
			Text = label,
			FontSize = 13,
			Padding = new Thickness(12, 6),
			CornerRadius = 14,
			BackgroundColor = selected ? RhythmColors.Accent : RhythmColors.Surface2,
			TextColor = selected ? RhythmColors.Bg : RhythmColors.TextPrimary
		};
		btn.Clicked += (_, _) =>
		{
			_groupFilter = groupId;
			_ = ReloadAsync();
		};
		FilterHost.Add(btn);
	}

	private void StyleSegments()
	{
		StyleSegment(ByExerciseBtn, _byExercise);
		StyleSegment(BySessionBtn, !_byExercise);
	}

	private static void StyleSegment(Button btn, bool selected)
	{
		btn.BackgroundColor = selected ? RhythmColors.Accent : RhythmColors.Surface2;
		btn.TextColor = selected ? RhythmColors.Bg : RhythmColors.TextPrimary;
		btn.CornerRadius = 14;
	}

	private void BuildExerciseView(TrainingHistoryAggregate agg)
	{
		if (agg.ExerciseGroups.Count == 0)
		{
			ContentHost.Add(Hint("Aucun exercice sur cette période."));
			return;
		}
		foreach (var group in agg.ExerciseGroups)
		{
			ContentHost.Add(new Label
			{
				Text = group.Name,
				FontFamily = "OpenSansSemibold",
				TextColor = RhythmColors.TextPrimary
			});
			foreach (var exercise in group.Exercises)
				ContentHost.Add(ExerciseCard(exercise));
		}
	}

	private View ExerciseCard(ExerciseHistoryRow exercise)
	{
		var stack = new VerticalStackLayout { Spacing = 6 };
		var head = new Grid
		{
			ColumnDefinitions =
			[
				new ColumnDefinition(new GridLength(48)),
				new ColumnDefinition(GridLength.Star),
				new ColumnDefinition(GridLength.Auto)
			],
			ColumnSpacing = 10
		};
		head.Add(new Image
		{
			Source = ExerciseIllustration.SourceFor(exercise.ExerciseId),
			WidthRequest = 48,
			HeightRequest = 48
		}, 0);
		head.Add(new Label
		{
			Text = exercise.Name,
			FontFamily = "OpenSansSemibold",
			TextColor = RhythmColors.TextPrimary,
			VerticalOptions = LayoutOptions.Center
		}, 1);
		head.Add(new Label
		{
			Text = FormatKg(exercise.VolumeKg),
			TextColor = RhythmColors.Accent,
			VerticalOptions = LayoutOptions.Center
		}, 2);
		stack.Add(head);
		foreach (var day in exercise.Days)
		{
			var kinds = day.PrKinds.Count == 0 ? "" : "  PR " + string.Join(" · ", day.PrKinds);
			stack.Add(new Label
			{
				Text = day.Day.ToString("d MMM", CultureInfo.GetCultureInfo("fr-FR")) + kinds,
				FontSize = 12,
				TextColor = day.PrKinds.Count == 0 ? RhythmColors.TextSecondary : RhythmColors.Accent
			});
			foreach (var line in day.Lines)
			{
				stack.Add(new Label
				{
					Text = $"{line.WeightKg.ToString("0.#", CultureInfo.InvariantCulture)} kg × {string.Join(", ", line.Reps)}",
					TextColor = RhythmColors.TextPrimary,
					FontSize = 14
				});
			}
		}
		var chart = new Button
		{
			Text = "Progression",
			Style = (Style)Application.Current!.Resources["RhythmBtnGhost"],
			HorizontalOptions = LayoutOptions.Start,
			Padding = new Thickness(0, 4)
		};
		var id = exercise.ExerciseId;
		chart.Clicked += async (_, _) =>
			await UiShellNavigate.GoAsync($"{nameof(ExerciseProgressPage)}?ExerciseId={id}").ConfigureAwait(true);
		stack.Add(chart);
		return Card(stack);
	}

	private void BuildSessionView(TrainingHistoryAggregate agg)
	{
		if (agg.Sessions.Count == 0)
		{
			ContentHost.Add(Hint("Aucune séance sur cette période."));
			return;
		}
		foreach (var session in agg.Sessions)
		{
			var text = new VerticalStackLayout
			{
				Spacing = 2,
				Children =
				{
					new Label
					{
						Text = session.Title,
						FontFamily = "OpenSansSemibold",
						TextColor = RhythmColors.TextPrimary
					},
					new Label
					{
						Text = session.HasSets
							? $"{session.Day:d MMM yyyy} · {FormatKg(session.VolumeKg)}"
							: $"{session.Day:d MMM yyyy} · Aucune série enregistrée",
						FontSize = 13,
						TextColor = RhythmColors.TextSecondary
					}
				}
			};
			var id = session.WorkoutId;
			var card = Card(text);
			var tap = new TapGestureRecognizer();
			tap.Tapped += async (_, _) =>
				await UiShellNavigate.GoAsync(
					$"{nameof(HistoryDetailPage)}?WorkoutId={Uri.EscapeDataString(id.ToString())}")
					.ConfigureAwait(true);
			card.GestureRecognizers.Add(tap);
			ContentHost.Add(card);
		}
	}

	private async void OnCleanClicked(object? sender, EventArgs e)
	{
		try
		{
			var repo = ServiceHelper.Services.GetRequiredService<IRhythmoRepository>();
			var profileId = ServiceHelper.Services.GetRequiredService<ActiveProfileStore>().Get();
			var orphaned = await repo.GetOrphanedCompletedWorkoutsAsync(profileId).ConfigureAwait(true);
			if (orphaned.Count == 0)
			{
				await DisplayAlertAsync("Historique", "Aucune entrée à supprimer.", "OK").ConfigureAwait(true);
				return;
			}
			await repo.DeleteCompletedWorkoutsAsync(orphaned.Select(o => o.Id)).ConfigureAwait(true);
			await DisplayAlertAsync("Historique", $"{orphaned.Count} entrée(s) supprimée(s).", "OK").ConfigureAwait(true);
		}
		catch (Exception ex)
		{
			await UiGuard.ReportAsync(this, ex, nameof(OnCleanClicked)).ConfigureAwait(true);
		}
		await ReloadAsync().ConfigureAwait(true);
	}

	private static HistoryExerciseInfo? Resolve(Guid id, IReadOnlyDictionary<Guid, Data.CachedExerciseRow> catalog)
	{
		if (!catalog.TryGetValue(id, out var row))
			return null;
		var def = MuscleRankingCatalog.Find(row.Category ?? "", row.NameFr);
		var muscle = def?.PrimaryMuscles.FirstOrDefault() ?? def?.SecondaryMuscles.FirstOrDefault();
		string? group = null;
		if (muscle is not null)
		{
			foreach (var item in MuscleIds.Muscles)
			{
				if (item.Id == muscle)
					group = item.GroupId;
			}
		}
		var name = string.IsNullOrWhiteSpace(row.NameFr) ? TrainingHistory.UnknownExerciseName : row.NameFr;
		return new HistoryExerciseInfo(name, group);
	}

	private static string FormatKg(double kg) =>
		kg >= 1000 ? $"{kg / 1000d:0.#} t" : $"{kg:0} kg";

	private static Border Card(View content) => new()
	{
		Padding = new Thickness(14),
		BackgroundColor = RhythmColors.Surface1,
		StrokeThickness = 0,
		StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 16 },
		Content = content
	};

	private static Label Hint(string text) => new()
	{
		Text = text,
		TextColor = RhythmColors.TextSecondary
	};
}
