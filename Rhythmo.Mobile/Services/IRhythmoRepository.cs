using Rhythmo.Mobile.Data;

namespace Rhythmo.Mobile.Services;

public interface IRhythmoRepository
{
	Task EnsureBuiltinExercisesAsync(CancellationToken ct = default);

	Task<ProfileRow?> GetProfileAsync(Guid userId, CancellationToken ct = default);
	Task<IReadOnlyList<ProfileRow>> ListCommunityProfilesAsync(CancellationToken ct = default);
	Task SaveProfileAsync(ProfileRow profile, CancellationToken ct = default);

	Task<IReadOnlyList<CachedExerciseRow>> ListExercisesAsync(CancellationToken ct = default);

	Task<IReadOnlyList<SessionTemplateRow>> ListSessionTemplatesAsync(Guid ownerId, CancellationToken ct = default);
	Task<SessionTemplateRow?> GetSessionTemplateAsync(Guid sessionId, CancellationToken ct = default);
	Task<int> CountSessionExercisesAsync(Guid sessionId, CancellationToken ct = default);
	Task<IReadOnlyList<SessionExerciseRow>> ListSessionExercisesAsync(Guid sessionId, CancellationToken ct = default);
	Task SaveSessionAsync(SessionTemplateRow template, IReadOnlyList<SessionExerciseRow> lines, bool clearSnapshot, CancellationToken ct = default);
	Task DeleteSessionTemplateAsync(Guid sessionId, Guid ownerId, CancellationToken ct = default);
	Task DuplicateSessionTemplateAsync(Guid sourceId, Guid ownerId, CancellationToken ct = default);

	Task<SessionLastSnapshotRow?> GetSessionSnapshotAsync(Guid sessionId, CancellationToken ct = default);
	Task UpsertSessionSnapshotAsync(SessionLastSnapshotRow snapshot, CancellationToken ct = default);
	Task DeleteSessionSnapshotAsync(Guid sessionId, CancellationToken ct = default);

	Task<IReadOnlyList<CompletedWorkoutRow>> ListCompletedWorkoutsAsync(Guid profileId, CancellationToken ct = default);
	Task<IReadOnlyList<CompletedWorkoutRow>> ListCompletedWorkoutsSinceAsync(Guid profileId, DateTime sinceUtc, CancellationToken ct = default);
	Task<CompletedWorkoutRow?> GetCompletedWorkoutAsync(Guid id, Guid profileId, CancellationToken ct = default);
	Task<Guid> AddCompletedWorkoutAsync(CompletedWorkoutRow row, CancellationToken ct = default);
	Task DeleteCompletedWorkoutAsync(Guid id, Guid profileId, CancellationToken ct = default);
	Task<IReadOnlyList<CompletedWorkoutRow>> GetOrphanedCompletedWorkoutsAsync(Guid profileId, CancellationToken ct = default);
	Task DeleteCompletedWorkoutsAsync(IEnumerable<Guid> ids, CancellationToken ct = default);
	Task DeleteAllPerformanceDailyAsync(Guid profileId, CancellationToken ct = default);

	Task<ExerciseLastWeightRow?> GetLastWeightAsync(Guid profileId, Guid exerciseId, CancellationToken ct = default);
	Task UpsertLastWeightAsync(ExerciseLastWeightRow row, CancellationToken ct = default);

	Task UpsertDailyMaxKgAsync(Guid profileId, Guid exerciseId, DateOnly date, double maxKg, CancellationToken ct = default);
	Task<IReadOnlyList<ExercisePerformanceDailyRow>> ListPerformanceDailyAsync(
		Guid profileId, Guid exerciseId, CancellationToken ct = default);
	Task<IReadOnlyList<Guid>> ListExercisesWithPerformanceAsync(Guid profileId, CancellationToken ct = default);

	Task<ExercisePersonalBestRow?> GetExercisePersonalBestAsync(
		Guid profileId, Guid exerciseId, CancellationToken ct = default);
	Task<IReadOnlyDictionary<Guid, double>> ListExerciseAllTimeMaxKgAsync(
		Guid profileId, CancellationToken ct = default);
	Task UpsertExercisePersonalBestAsync(ExercisePersonalBestRow row, CancellationToken ct = default);
	Task InsertPrEventAsync(PrEventRow row, CancellationToken ct = default);
	Task<IReadOnlyList<PrEventRow>> ListPrEventsAsync(CancellationToken ct = default);
	Task<IReadOnlyList<PrEventRow>> ListRecentPrEventsAsync(int limit = 24, CancellationToken ct = default);

	Task<IReadOnlyList<ImportableUserRow>> ListImportableUsersAsync(CancellationToken ct = default);

	Task<IReadOnlyList<SessionTemplateRow>> ListSessionTemplatesByOwnerAsync(
		Guid ownerId, CancellationToken ct = default);

	Task ImportSessionTemplateAsync(
		Guid sourceSessionId, Guid targetOwnerId, CancellationToken ct = default);

	Task<IReadOnlyList<ProfileExerciseRankRow>> ListProfileExerciseRanksAsync(
		Guid profileId, CancellationToken ct = default);
	Task UpsertProfileExerciseRankAsync(ProfileExerciseRankRow row, CancellationToken ct = default);

	Task<IReadOnlyList<MuscleRankSnapshotRow>> ListMuscleRankSnapshotsAsync(
		Guid profileId, string versionId, CancellationToken ct = default);
	Task UpsertMuscleRankSnapshotAsync(MuscleRankSnapshotRow row, CancellationToken ct = default);

	Task<IReadOnlyList<GroupRankSnapshotRow>> ListGroupRankSnapshotsAsync(
		Guid profileId, string versionId, CancellationToken ct = default);
	Task UpsertGroupRankSnapshotAsync(GroupRankSnapshotRow row, CancellationToken ct = default);

	Task<IReadOnlyList<RankQuestAttemptRow>> ListQuestAttemptsAsync(
		Guid profileId, CancellationToken ct = default);
	Task<RankQuestAttemptRow?> GetInProgressQuestAsync(Guid profileId, CancellationToken ct = default);
	Task InsertQuestAttemptAsync(RankQuestAttemptRow row, CancellationToken ct = default);
	Task UpdateQuestAttemptAsync(RankQuestAttemptRow row, CancellationToken ct = default);
	Task<bool> HasQuestAttemptOnLocalDateAsync(
		Guid profileId, Guid exerciseId, DateOnly localDate, CancellationToken ct = default);

	Task<IReadOnlyList<PrEventRow>> ListPrEventsForProfileAsync(Guid profileId, CancellationToken ct = default);
	Task<IReadOnlyList<ExercisePersonalBestRow>> ListExercisePersonalBestsAsync(
		Guid profileId, CancellationToken ct = default);

	Task<IReadOnlyList<CardioActivityRow>> ListCardioActivitiesSinceAsync(
		Guid profileId, DateTime sinceUtc, CancellationToken ct = default);
	Task AddCardioActivityAsync(CardioActivityRow row, CancellationToken ct = default);
	Task DeleteCardioActivityAsync(Guid id, Guid profileId, CancellationToken ct = default);

	Task<IReadOnlyList<WeeklyChallengeAssignmentRow>> ListChallengeAssignmentsAsync(
		Guid profileId, int isoYear, int isoWeek, CancellationToken ct = default);
	Task InsertChallengeAssignmentAsync(WeeklyChallengeAssignmentRow row, CancellationToken ct = default);
	Task UpdateChallengeProgressAsync(
		Guid assignmentId,
		string status,
		string? targetJson,
		DateTime? startedUtc,
		CancellationToken ct = default);
	Task<IReadOnlyList<ChallengePointAwardRow>> ListChallengeAwardsAsync(
		Guid profileId, CancellationToken ct = default);
	Task<bool> TryInsertChallengeAwardAsync(ChallengePointAwardRow row, CancellationToken ct = default);
	Task UpdateChallengeAwardAsync(Guid assignmentId, int points, CancellationToken ct = default);
}

public sealed class ImportableUserRow
{
	public Guid UserId { get; init; }
	public string DisplayName { get; init; } = "";
}
