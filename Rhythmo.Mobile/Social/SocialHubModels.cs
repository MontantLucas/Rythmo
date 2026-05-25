using Rhythmo.Mobile.Data;

using Rhythmo.Mobile.Theme;



namespace Rhythmo.Mobile.Social;



public enum LeaderboardPeriod { Week, Month, AllTime }



public enum PrKind { Weight, Reps, Volume }



public enum LeaderboardTrend { Up, Down, Flat }



public sealed class SocialCircleMember

{

	public Guid UserId { get; init; }

	public string DisplayName { get; init; } = "";

	public double WeightKg { get; init; }

	public BiologicalSex BiologicalSex { get; init; } = BiologicalSex.Male;

}



public sealed class LeaderboardEntryVm

{

	public int Rank { get; init; }

	public Guid UserId { get; init; }

	public string DisplayName { get; init; } = "";

	public BiologicalSex BiologicalSex { get; init; } = BiologicalSex.Male;

	public string PrimaryMetric { get; init; } = "";

	public bool IsMe { get; init; }

	public LeaderboardTrend Trend { get; init; } = LeaderboardTrend.Flat;

}



public sealed class PrFeedItemVm

{

	public Guid UserId { get; init; }

	public Guid ExerciseId { get; init; }

	public BiologicalSex BiologicalSex { get; init; } = BiologicalSex.Male;

	public string DisplayName { get; init; } = "";

	public string ExerciseName { get; init; } = "";

	public string PerformanceLine { get; init; } = "";

	public string RelativeTime { get; init; } = "";

	public PrKind Kind { get; init; }

	public DateTime CompletedUtc { get; init; }

}



public sealed class TodayActivityVm

{

	public BiologicalSex BiologicalSex { get; init; } = BiologicalSex.Male;

	public string DisplayName { get; init; } = "";

	public string WorkoutTitle { get; init; } = "";

	public int DurationMinutes { get; init; }

	public string VolumeText { get; init; } = "";

}



public sealed class BadgeVm

{

	public string IconGlyph { get; init; } = SocialGlyph.Bolt;

	public string Title { get; init; } = "";

	public string RarityLabel { get; init; } = "Rare";

	public Color RarityColor { get; init; } = RhythmColors.Accent;

	public bool IsLocked { get; init; }

}



public sealed class StreakSummaryVm

{

	public int ActiveDays { get; init; }

	public int RegularWeeks { get; init; }

}



public sealed class LiveTrainingVm

{

	public BiologicalSex BiologicalSex { get; init; } = BiologicalSex.Male;

	public string DisplayName { get; init; } = "";

	public string SessionTitle { get; init; } = "";

	public int ElapsedMinutes { get; init; }

}



public sealed class SocialHubSnapshot

{

	public IReadOnlyList<LeaderboardEntryVm> SessionLeaderboard { get; init; } = [];

	public IReadOnlyList<LeaderboardEntryVm> VolumeLeaderboard { get; init; } = [];

	public IReadOnlyList<PrFeedItemVm> RecentPrs { get; init; } = [];

	public IReadOnlyList<TodayActivityVm> TodayActivities { get; init; } = [];

	public IReadOnlyList<BadgeVm> Badges { get; init; } = [];

	public IReadOnlyList<LiveTrainingVm> LiveSessions { get; init; } = [];

	public StreakSummaryVm? StreakSummary { get; init; }

}


