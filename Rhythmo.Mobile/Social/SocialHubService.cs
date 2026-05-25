using System.Globalization;
using Rhythmo.Mobile.Data;
using Rhythmo.Mobile.Services;
using Rhythmo.Mobile.Theme;
using Rhythmo.Shared.Contracts;

namespace Rhythmo.Mobile.Social;

public sealed class SocialHubService
{
	private static readonly TimeSpan LiveWindow = TimeSpan.FromMinutes(90);
	private static readonly TimeSpan SnapshotCacheTtl = TimeSpan.FromSeconds(50);

	private SocialHubSnapshot? _cachedSnapshot;
	private LeaderboardPeriod _cachedPeriod;
	private Guid _cachedMeId;
	private DateTime _cachedAtUtc;

	public void InvalidateCache()
	{
		_cachedSnapshot = null;
		_cachedAtUtc = DateTime.MinValue;
	}

	public async Task<SocialHubSnapshot> BuildAsync(
		IRhythmoRepository repo,
		Guid meId,
		LeaderboardPeriod period,
		CancellationToken ct = default)
	{
		if (_cachedSnapshot is not null &&
		    _cachedPeriod == period &&
		    _cachedMeId == meId &&
		    DateTime.UtcNow - _cachedAtUtc < SnapshotCacheTtl)
			return _cachedSnapshot;

		var circle = await BuildCircleAsync(repo, meId, ct).ConfigureAwait(false);
		var exerciseNames = (await repo.ListExercisesAsync(ct).ConfigureAwait(false))
			.ToDictionary(static e => e.Id, static e => e.NameFr ?? "Exercice");

		var (since, prevStart, prevEnd) = PeriodBounds(period);
		var loadSince = period == LeaderboardPeriod.AllTime ? DateTime.MinValue : prevStart;

		var workoutBatches = await Task.WhenAll(circle.Select(m => LoadMemberWorkoutsAsync(
			repo, m, period, loadSince, ct))).ConfigureAwait(false);

		var allWorkouts = new List<(Guid UserId, string Name, CompletedWorkoutRow Row)>();
		foreach (var batch in workoutBatches)
			allWorkouts.AddRange(batch);

		var currentWorkouts = period == LeaderboardPeriod.AllTime
			? allWorkouts
			: allWorkouts.Where(w => w.Row.CompletedUtc >= since).ToList();
		var previousWorkouts = period == LeaderboardPeriod.AllTime
			? []
			: allWorkouts.Where(w => w.Row.CompletedUtc >= prevStart && w.Row.CompletedUtc < prevEnd).ToList();

		var sexByUser = circle.ToDictionary(m => m.UserId, m => m.BiologicalSex);

		var prsTask = LoadPrFeedAsync(repo, circle, exerciseNames, ct);
		var liveTask = BuildLiveAsync(repo, circle, meId, ct);

		var sessionLb = BuildSessionLeaderboard(circle, currentWorkouts, previousWorkouts, meId, period);
		var volumeLb = await Task.Run(
			() => BuildVolumeLeaderboard(circle, currentWorkouts, previousWorkouts, meId, period),
			ct).ConfigureAwait(false);
		var today = BuildTodayActivities(currentWorkouts, sexByUser);
		var streakSummary = BuildStreakSummary(meId, allWorkouts);
		var badges = await Task.Run(
			() => BuildBadges(circle.FirstOrDefault(m => m.UserId == meId), currentWorkouts, exerciseNames),
			ct).ConfigureAwait(false);

		var prs = BuildRecentPrSummary(await prsTask.ConfigureAwait(false));
		var live = await liveTask.ConfigureAwait(false);

		var snapshot = new SocialHubSnapshot
		{
			SessionLeaderboard = sessionLb,
			VolumeLeaderboard = volumeLb,
			RecentPrs = prs,
			TodayActivities = today,
			Badges = badges,
			LiveSessions = live,
			StreakSummary = streakSummary
		};

		_cachedSnapshot = snapshot;
		_cachedPeriod = period;
		_cachedMeId = meId;
		_cachedAtUtc = DateTime.UtcNow;
		return snapshot;
	}

	private static async Task<List<(Guid UserId, string Name, CompletedWorkoutRow Row)>> LoadMemberWorkoutsAsync(
		IRhythmoRepository repo,
		SocialCircleMember member,
		LeaderboardPeriod period,
		DateTime loadSince,
		CancellationToken ct)
	{
		var list = period == LeaderboardPeriod.AllTime
			? await repo.ListCompletedWorkoutsAsync(member.UserId, ct).ConfigureAwait(false)
			: await repo.ListCompletedWorkoutsSinceAsync(member.UserId, loadSince, ct).ConfigureAwait(false);

		return list.Select(w => (member.UserId, member.DisplayName, w)).ToList();
	}

	private static async Task<IReadOnlyList<SocialCircleMember>> BuildCircleAsync(
		IRhythmoRepository repo, Guid meId, CancellationToken ct)
	{
		var profiles = await repo.ListCommunityProfilesAsync(ct).ConfigureAwait(false);
		return profiles
			.Select(p => new SocialCircleMember
			{
				UserId = p.Id,
				DisplayName = p.Id == meId
					? (string.IsNullOrWhiteSpace(p.DisplayName) ? "Moi" : p.DisplayName)
					: (string.IsNullOrWhiteSpace(p.DisplayName) ? "Utilisateur" : p.DisplayName),
				WeightKg = p.WeightKg > 0 ? p.WeightKg : 75,
				BiologicalSex = p.BiologicalSex
			})
			.OrderByDescending(m => m.UserId == meId)
			.ThenBy(m => m.DisplayName, StringComparer.OrdinalIgnoreCase)
			.ToList();
	}

	private static (DateTime since, DateTime prevStart, DateTime prevEnd) PeriodBounds(LeaderboardPeriod period)
	{
		var now = DateTime.UtcNow;
		return period switch
		{
			LeaderboardPeriod.Week => (now.AddDays(-7), now.AddDays(-14), now.AddDays(-7)),
			LeaderboardPeriod.Month => (now.AddDays(-30), now.AddDays(-60), now.AddDays(-30)),
			_ => (DateTime.MinValue, DateTime.MinValue, DateTime.MinValue)
		};
	}

	private static List<LeaderboardEntryVm> BuildSessionLeaderboard(
		IReadOnlyList<SocialCircleMember> circle,
		List<(Guid UserId, string Name, CompletedWorkoutRow Row)> workouts,
		List<(Guid UserId, string Name, CompletedWorkoutRow Row)> previousWorkouts,
		Guid meId,
		LeaderboardPeriod period)
	{
		var counts = circle.ToDictionary(
			m => m.UserId,
			m => workouts.Count(w => w.UserId == m.UserId));
		var prevCounts = circle.ToDictionary(
			m => m.UserId,
			m => previousWorkouts.Count(w => w.UserId == m.UserId));

		var prevAsDouble = prevCounts.ToDictionary(kv => kv.Key, kv => (double)kv.Value);
		return RankLeaderboard(
			circle,
			circle.Select(m => (m.UserId, m.DisplayName, (double)counts.GetValueOrDefault(m.UserId, 0))),
			prevAsDouble,
			meId,
			period,
			static v => ((int)v).ToString(CultureInfo.InvariantCulture),
			static v => v);
	}

	private static List<LeaderboardEntryVm> BuildVolumeLeaderboard(
		IReadOnlyList<SocialCircleMember> circle,
		List<(Guid UserId, string Name, CompletedWorkoutRow Row)> workouts,
		List<(Guid UserId, string Name, CompletedWorkoutRow Row)> previousWorkouts,
		Guid meId,
		LeaderboardPeriod period)
	{
		var volumes = circle.ToDictionary(m => m.UserId, _ => 0d);
		var prevVolumes = circle.ToDictionary(m => m.UserId, _ => 0d);
		foreach (var w in workouts)
		{
			if (volumes.ContainsKey(w.UserId))
				volumes[w.UserId] += WorkoutAnalytics.ComputeVolumeKgFromPayload(w.Row.PayloadJson);
		}

		foreach (var w in previousWorkouts)
		{
			if (prevVolumes.ContainsKey(w.UserId))
				prevVolumes[w.UserId] += WorkoutAnalytics.ComputeVolumeKgFromPayload(w.Row.PayloadJson);
		}

		return RankLeaderboard(
			circle,
			circle.Select(m => (m.UserId, m.DisplayName, volumes.GetValueOrDefault(m.UserId, 0))),
			prevVolumes,
			meId,
			period,
			FormatTonnes,
			static v => v);
	}

	private static List<LeaderboardEntryVm> RankLeaderboard(
		IReadOnlyList<SocialCircleMember> circle,
		IEnumerable<(Guid Id, string Name, double Value)> rows,
		IReadOnlyDictionary<Guid, double> previousValues,
		Guid meId,
		LeaderboardPeriod period,
		Func<double, string> formatMetric,
		Func<double, double> orderBy)
	{
		var sexById = circle.ToDictionary(m => m.UserId, m => m.BiologicalSex);
		var ordered = rows
			.OrderByDescending(r => orderBy(r.Value))
			.ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
			.ToList();

		var result = new List<LeaderboardEntryVm>();
		for (var i = 0; i < ordered.Count; i++)
		{
			var r = ordered[i];
			if (r.Value <= 0 && i > 0)
				continue;

			var prev = previousValues.GetValueOrDefault(r.Id, 0);
			var trend = period == LeaderboardPeriod.AllTime
				? LeaderboardTrend.Flat
				: r.Value > prev + 0.01 ? LeaderboardTrend.Up
				: r.Value < prev - 0.01 ? LeaderboardTrend.Down
				: LeaderboardTrend.Flat;

			result.Add(new LeaderboardEntryVm
			{
				Rank = i + 1,
				UserId = r.Id,
				DisplayName = r.Name,
				BiologicalSex = sexById.GetValueOrDefault(r.Id, BiologicalSex.Male),
				PrimaryMetric = formatMetric(r.Value),
				IsMe = r.Id == meId,
				Trend = trend
			});
		}

		return result.Take(4).ToList();
	}

	private static string FormatVolume(double kg) =>
		kg >= 1000 ? $"{kg / 1000d:0.#} t" : $"{kg:0} kg";

	private static string FormatTonnes(double kg) => (kg / 1000d).ToString("0.#");

	public async Task<IReadOnlyList<PrFeedItemVm>> LoadAllPrFeedAsync(
		IRhythmoRepository repo,
		Guid meId,
		CancellationToken ct = default)
	{
		var circle = await BuildCircleAsync(repo, meId, ct).ConfigureAwait(false);
		var exerciseNames = (await repo.ListExercisesAsync(ct).ConfigureAwait(false))
			.ToDictionary(static e => e.Id, static e => e.NameFr ?? "Exercice");
		return await LoadPrFeedAsync(repo, circle, exerciseNames, ct).ConfigureAwait(false);
	}

	private static async Task<IReadOnlyList<PrFeedItemVm>> LoadPrFeedAsync(
		IRhythmoRepository repo,
		IReadOnlyList<SocialCircleMember> circle,
		IReadOnlyDictionary<Guid, string> exerciseNames,
		CancellationToken ct)
	{
		var circleIds = circle.Select(m => m.UserId).ToHashSet();
		var nameByUser = circle.ToDictionary(m => m.UserId, m => m.DisplayName);
		var sexByUser = circle.ToDictionary(m => m.UserId, m => m.BiologicalSex);

		var rows = await repo.ListPrEventsAsync(ct).ConfigureAwait(false);
		var feed = new List<PrFeedItemVm>();

		foreach (var e in rows)
		{
			if (!circleIds.Contains(e.ProfileId))
				continue;

			exerciseNames.TryGetValue(e.ExerciseId, out var exName);
			var kind = ParsePrKind(e.Kind);
			feed.Add(new PrFeedItemVm
			{
				UserId = e.ProfileId,
				ExerciseId = e.ExerciseId,
				BiologicalSex = sexByUser.GetValueOrDefault(e.ProfileId, BiologicalSex.Male),
				DisplayName = nameByUser.GetValueOrDefault(e.ProfileId, "?"),
				ExerciseName = string.IsNullOrWhiteSpace(exName) ? "Mouvement" : exName,
				PerformanceLine = e.PerformanceLine,
				RelativeTime = FormatRelative(e.AchievedUtc),
				Kind = kind,
				CompletedUtc = e.AchievedUtc
			});

		}

		return feed;
	}

	private static IReadOnlyList<PrFeedItemVm> BuildRecentPrSummary(IReadOnlyList<PrFeedItemVm> allPrs)
	{
		var result = new List<PrFeedItemVm>();
		var perUserCount = new Dictionary<Guid, int>();
		foreach (var pr in allPrs.OrderByDescending(p => p.CompletedUtc))
		{
			var count = perUserCount.GetValueOrDefault(pr.UserId);
			if (count >= 2)
				continue;

			result.Add(pr);
			perUserCount[pr.UserId] = count + 1;
			if (result.Count >= 6)
				break;
		}

		return result;
	}

	private static PrKind ParsePrKind(string kind) =>
		kind.Equals("reps", StringComparison.OrdinalIgnoreCase) ? PrKind.Reps
		: kind.Equals("volume", StringComparison.OrdinalIgnoreCase) ? PrKind.Volume
		: PrKind.Weight;

	private static List<TodayActivityVm> BuildTodayActivities(
		List<(Guid UserId, string Name, CompletedWorkoutRow Row)> workouts,
		IReadOnlyDictionary<Guid, BiologicalSex> sexByUser)
	{
		var today = DateOnly.FromDateTime(DateTime.Now);
		return workouts
			.Where(w => DateOnly.FromDateTime(w.Row.CompletedUtc.ToLocalTime()) == today)
			.OrderByDescending(w => w.Row.CompletedUtc)
			.Select(w =>
			{
				var snap = CompletedWorkoutSnapshot.DeserializeRequestSnapshot(w.Row.PayloadJson);
				var mins = snap?.EstimatedDurationMinutes is { } m and > 0
					? (int)m
					: EstimateMinutes(snap);
				var title = string.IsNullOrWhiteSpace(w.Row.SessionTitle) ? "Séance" : w.Row.SessionTitle.Trim();
				var vol = WorkoutAnalytics.ComputeVolumeKgFromPayload(w.Row.PayloadJson);
				return new TodayActivityVm
				{
					BiologicalSex = sexByUser.GetValueOrDefault(w.UserId, BiologicalSex.Male),
					DisplayName = w.Name,
					WorkoutTitle = title,
					DurationMinutes = mins,
					VolumeText = FormatVolume(vol)
				};
			})
			.ToList();
	}

	private static int EstimateMinutes(WorkoutCompletedRequest? snap)
	{
		var sets = snap?.Exercises?.Sum(e => e.Sets?.Count ?? 0) ?? 0;
		return Math.Max(1, (int)Math.Round(sets * 3.0));
	}

	private static StreakSummaryVm? BuildStreakSummary(
		Guid meId,
		List<(Guid UserId, string Name, CompletedWorkoutRow Row)> allWorkouts)
	{
		var days = allWorkouts
			.Where(w => w.UserId == meId)
			.Select(w => DateOnly.FromDateTime(w.Row.CompletedUtc.ToLocalTime()))
			.ToHashSet();

		if (days.Count == 0)
			return null;

		return new StreakSummaryVm
		{
			ActiveDays = ComputeDayStreak(days),
			RegularWeeks = ComputeWeekStreak(days)
		};
	}

	private static int ComputeDayStreak(HashSet<DateOnly> workoutDays)
	{
		if (workoutDays.Count == 0)
			return 0;

		var cursor = DateOnly.FromDateTime(DateTime.Today);
		if (!workoutDays.Contains(cursor))
			cursor = cursor.AddDays(-1);

		var streak = 0;
		while (workoutDays.Contains(cursor))
		{
			streak++;
			cursor = cursor.AddDays(-1);
		}

		return streak;
	}

	private static int ComputeWeekStreak(HashSet<DateOnly> workoutDays)
	{
		if (workoutDays.Count == 0)
			return 0;

		var cursor = StartOfWeek(DateOnly.FromDateTime(DateTime.Today));
		var streak = 0;
		while (workoutDays.Any(d => StartOfWeek(d) == cursor))
		{
			streak++;
			cursor = cursor.AddDays(-7);
		}

		return streak;
	}

	private static DateOnly StartOfWeek(DateOnly d)
	{
		var dow = (int)d.DayOfWeek;
		var mondayOffset = dow == 0 ? 6 : dow - 1;
		return d.AddDays(-mondayOffset);
	}

	private static List<BadgeVm> BuildBadges(
		SocialCircleMember? me,
		List<(Guid UserId, string Name, CompletedWorkoutRow Row)> workouts,
		IReadOnlyDictionary<Guid, string> exerciseNames)
	{
		if (me is null)
			return BadgeCatalog([]);

		var mine = workouts.Where(w => w.UserId == me.UserId).Select(w => w.Row).ToList();
		var earned = new List<BadgeVm>();
		var count = mine.Count;

		if (count >= 100)
			earned.Add(MakeBadge("100 séances", SocialGlyph.Event, "Légendaire", RhythmColors.Violet));
		else if (count >= 10)
			earned.Add(MakeBadge("10 séances", SocialGlyph.Event, "Rare", RhythmColors.Accent));

		if (me.WeightKg > 0)
		{
			if (GetMaxKgForKeyword(mine, exerciseNames, "couché", "bench") >= me.WeightKg)
				earned.Add(MakeBadge("Bench 1× BW", SocialGlyph.FitnessCenter, "Épique", RhythmColors.Gold));
			if (GetMaxKgForKeyword(mine, exerciseNames, "squat") >= me.WeightKg * 2)
				earned.Add(MakeBadge("Squat 2× BW", SocialGlyph.FitnessCenter, "Épique", RhythmColors.Gold));
			if (GetMaxKgForKeyword(mine, exerciseNames, "terre", "deadlift") >= me.WeightKg * 2.5)
				earned.Add(MakeBadge("Deadlift 2,5× BW", SocialGlyph.Bolt, "Légendaire", RhythmColors.Violet));
		}

		var weekVol = mine
			.Where(w => w.CompletedUtc >= DateTime.UtcNow.AddDays(-7))
			.Sum(w => WorkoutAnalytics.ComputeVolumeKgFromPayload(w.PayloadJson));
		if (weekVol >= 10_000)
			earned.Add(MakeBadge("10k volume / semaine", SocialGlyph.TrendUp, "Rare", RhythmColors.Accent));

		return BadgeCatalog(earned);
	}

	private static BadgeVm MakeBadge(string title, string glyph, string rarity, Color color) =>
		new()
		{
			Title = title,
			IconGlyph = glyph,
			RarityLabel = rarity,
			RarityColor = color
		};

	private static List<BadgeVm> BadgeCatalog(IReadOnlyList<BadgeVm> earned)
	{
		var titles = earned.Select(e => e.Title).ToHashSet(StringComparer.OrdinalIgnoreCase);
		var all = new List<BadgeVm>(earned);
		void Locked(string title, string glyph, string rarity, Color color)
		{
			if (!titles.Contains(title))
				all.Add(new BadgeVm
				{
					Title = title,
					IconGlyph = glyph,
					RarityLabel = rarity,
					RarityColor = color,
					IsLocked = true
				});
		}

		Locked("100 séances", SocialGlyph.Event, "Légendaire", RhythmColors.Violet);
		Locked("Bench 1× BW", SocialGlyph.FitnessCenter, "Épique", RhythmColors.Gold);
		Locked("Squat 2× BW", SocialGlyph.FitnessCenter, "Épique", RhythmColors.Gold);
		Locked("Deadlift 2,5× BW", SocialGlyph.Bolt, "Légendaire", RhythmColors.Violet);
		Locked("10k volume / semaine", SocialGlyph.TrendUp, "Rare", RhythmColors.Accent);

		return all;
	}

	private static double GetMaxKgForKeyword(
		IReadOnlyList<CompletedWorkoutRow> workouts,
		IReadOnlyDictionary<Guid, string> names,
		params string[] keywords)
	{
		double max = 0;
		foreach (var w in workouts)
		{
			var snap = CompletedWorkoutSnapshot.DeserializeRequestSnapshot(w.PayloadJson);
			if (snap?.Exercises is null)
				continue;
			foreach (var ex in snap.Exercises)
			{
				if (!names.TryGetValue(ex.ExerciseId, out var nm))
					continue;
				if (!keywords.Any(k => nm.Contains(k, StringComparison.OrdinalIgnoreCase)))
					continue;
				if (ex.Sets is null)
					continue;
				foreach (var s in ex.Sets)
					max = Math.Max(max, s.WeightKg);
			}
		}

		return max;
	}

	private static async Task<IReadOnlyList<LiveTrainingVm>> BuildLiveAsync(
		IRhythmoRepository repo,
		IReadOnlyList<SocialCircleMember> circle,
		Guid meId,
		CancellationToken ct)
	{
		var cutoff = DateTime.UtcNow - LiveWindow;
		var found = await Task.WhenAll(circle.Select(m => FindLiveForMemberAsync(repo, m, meId, cutoff, ct)))
			.ConfigureAwait(false);

		return found.Where(l => l is not null).Cast<LiveTrainingVm>().OrderBy(l => l.DisplayName).ToList();
	}

	private static async Task<LiveTrainingVm?> FindLiveForMemberAsync(
		IRhythmoRepository repo,
		SocialCircleMember member,
		Guid meId,
		DateTime cutoff,
		CancellationToken ct)
	{
		var templates = await repo.ListSessionTemplatesByOwnerAsync(member.UserId, ct).ConfigureAwait(false);
		foreach (var t in templates.Take(8))
		{
			var snap = await repo.GetSessionSnapshotAsync(t.Id, ct).ConfigureAwait(false);
			if (snap is null || snap.SavedUtc < cutoff)
				continue;

			return new LiveTrainingVm
			{
				BiologicalSex = member.BiologicalSex,
				DisplayName = member.UserId == meId ? "Moi" : member.DisplayName,
				SessionTitle = string.IsNullOrWhiteSpace(t.Title) ? "Séance" : t.Title,
				ElapsedMinutes = (int)Math.Max(1, (DateTime.UtcNow - snap.SavedUtc).TotalMinutes)
			};
		}

		return null;
	}

	private static string FormatRelative(DateTime utc)
	{
		var diff = DateTime.UtcNow - utc;
		if (diff.TotalMinutes < 1)
			return "à l'instant";
		if (diff.TotalMinutes < 60)
			return $"il y a {(int)diff.TotalMinutes} min";
		if (diff.TotalHours < 24)
			return $"il y a {(int)diff.TotalHours}h";
		if (diff.TotalDays < 7)
			return $"il y a {(int)diff.TotalDays} j";
		return utc.ToLocalTime().ToString("d MMM", CultureInfo.GetCultureInfo("fr-FR"));
	}

}
