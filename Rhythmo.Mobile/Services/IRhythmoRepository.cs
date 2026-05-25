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
	Task UpsertExercisePersonalBestAsync(ExercisePersonalBestRow row, CancellationToken ct = default);
	Task InsertPrEventAsync(PrEventRow row, CancellationToken ct = default);
	Task<IReadOnlyList<PrEventRow>> ListPrEventsAsync(CancellationToken ct = default);
	Task<IReadOnlyList<PrEventRow>> ListRecentPrEventsAsync(int limit = 24, CancellationToken ct = default);

	Task<IReadOnlyList<ImportableUserRow>> ListImportableUsersAsync(CancellationToken ct = default);

	Task<IReadOnlyList<SessionTemplateRow>> ListSessionTemplatesByOwnerAsync(
		Guid ownerId, CancellationToken ct = default);

	Task ImportSessionTemplateAsync(
		Guid sourceSessionId, Guid targetOwnerId, CancellationToken ct = default);
}

public sealed class ImportableUserRow
{
	public Guid UserId { get; init; }
	public string DisplayName { get; init; } = "";
}
