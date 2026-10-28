using Microsoft.Extensions.DependencyInjection;
using Rhythmo.Mobile.Charting;
using Rhythmo.Mobile.Infrastructure;
using Rhythmo.Mobile.Services;
using Rhythmo.Mobile.Theme;
using Rhythmo.Shared.History;
using Rhythmo.Shared.Ranking;

namespace Rhythmo.Mobile;

public partial class StatsPage : ContentPage
{
	private readonly TrainingPeriodStore _periods = ServiceHelper.Services.GetRequiredService<TrainingPeriodStore>();
	private readonly MuscleRadarDrawable _radar = new();
	private bool _loading;

	public StatsPage()
	{
		InitializeComponent();
		SelfRadar.Drawable = _radar;
		StatsRefresh.Command = new Command(() => _ = ReloadAsync());
		_periods.Changed += (_, _) => _ = ReloadAsync();
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		_ = ReloadAsync();
	}

	private async void OnCompareTapped(object? sender, TappedEventArgs e) =>
		await UiShellNavigate.GoAsync(nameof(MuscleComparePage)).ConfigureAwait(true);

	private async void OnHistoryTapped(object? sender, TappedEventArgs e) =>
		await UiShellNavigate.GoAsync(nameof(HistoryPage)).ConfigureAwait(true);

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
			RangeLabel.Text = window.RangeLabel;

			var repo = ServiceHelper.Services.GetRequiredService<IRhythmoRepository>();
			var profileId = ServiceHelper.Services.GetRequiredService<ActiveProfileStore>().Get();
			var since = window.Start.ToDateTime(TimeOnly.MinValue).ToUniversalTime().AddDays(-1);
			var rows = await repo.ListCompletedWorkoutsSinceAsync(profileId, since).ConfigureAwait(true);
			var catalog = await repo.ListExercisesAsync().ConfigureAwait(true);
			var byId = catalog.ToDictionary(e => e.Id);
			var agg = TrainingHistory.Build(
				rows.Select(HistoryMapping.ToHistory).ToList(),
				window,
				TimeZoneInfo.Local,
				id =>
				{
					if (!byId.TryGetValue(id, out var row))
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
					return new HistoryExerciseInfo(row.NameFr, group);
				},
				[]);
			SessionsLabel.Text = agg.SessionCount.ToString();
			ExercisesLabel.Text = agg.DistinctExerciseCount.ToString();
			VolumeLabel.Text = agg.VolumeKg >= 1000 ? $"{agg.VolumeKg / 1000d:0.#} t" : $"{agg.VolumeKg:0} kg";

			var snaps = await repo.ListGroupRankSnapshotsAsync(profileId, MuscleIds.StandardVersionId).ConfigureAwait(true);
			var side = MuscleCompareModel.Build(
				snaps.Select(s => new CompareSnapshot(s.GroupId, s.ValidatedRank)),
				[]);
			_radar.Self = MuscleIds.Groups.Select(g => side.GroupRanks[g.Id]).ToList();
			_radar.Other = new int?[6];
			SelfRadar.Invalidate();
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
		PeriodHost.Add(Arrow("‹", true, () => _periods.Previous(today)), 0);
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
		var next = Arrow("›", window.CanGoNext, () => _periods.Next(today));
		next.IsEnabled = window.CanGoNext;
		PeriodHost.Add(next, 2);
	}

	private static Button Arrow(string text, bool enabled, Action action)
	{
		var btn = new Button
		{
			Text = text,
			WidthRequest = 44,
			HeightRequest = 44,
			FontSize = 18,
			Padding = 0,
			BackgroundColor = RhythmColors.Surface2,
			TextColor = RhythmColors.TextPrimary,
			IsEnabled = enabled
		};
		btn.Clicked += (_, _) => action();
		return btn;
	}
}
