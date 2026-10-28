namespace Rhythmo.Mobile.Data;

public enum BiologicalSex
{
    Male = 0,
    Female = 1
}

public sealed class ProfileRow
{
    public Guid Id { get; set; }
    public string DisplayName { get; set; } = "";
    public BiologicalSex BiologicalSex { get; set; } = BiologicalSex.Male;
    public double WeightKg { get; set; }
    public double? HeightCm { get; set; }
    public int? AgeYears { get; set; }
}

public sealed class CachedExerciseRow
{
    public Guid Id { get; set; }
    public string NameFr { get; set; } = "";
    public string? Category { get; set; }
    public double MetApprox { get; set; }
}

public sealed class SessionTemplateRow
{
    public Guid Id { get; set; }
    public Guid OwnerProfileId { get; set; }
    public string Title { get; set; } = "";
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
}

public sealed class SessionExerciseRow
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public Guid ExerciseId { get; set; }
    public int SortOrder { get; set; }
    public int TargetSets { get; set; } = 3;
    public int? TargetReps { get; set; } = 10;
}

public sealed class SessionLastSnapshotRow
{
    public Guid SessionId { get; set; }
    public string Json { get; set; } = "{}";
    public DateTime SavedUtc { get; set; }
}

public sealed class CompletedWorkoutRow
{
    public Guid Id { get; set; }
    public Guid ProfileId { get; set; }
    public DateTime CompletedUtc { get; set; }
    public double CaloriesRounded { get; set; }
    public string SessionTitle { get; set; } = "";

    /// <summary>Identifiant de la séance (template) au moment de la fin du run — null pour les anciennes entrées.</summary>
    public Guid? SourceSessionTemplateId { get; set; }

    /// <summary>Serialized <see cref="Rhythmo.Shared.Contracts.WorkoutCompletedRequest"/> JSON (camelCase).</summary>
    public string? PayloadJson { get; set; }
}

public sealed class ExerciseLastWeightRow
{
    public Guid ProfileId { get; set; }
    public Guid ExerciseId { get; set; }
    public double WeightKg { get; set; }
    public DateTime UpdatedUtc { get; set; }
}

/// <summary>Max poids (kg) enregistré pour un exercice un jour donné (date locale du passage en salle).</summary>
public sealed class ExercisePerformanceDailyRow
{
	public Guid ProfileId { get; set; }
	public Guid ExerciseId { get; set; }
	public DateOnly PerformanceDate { get; set; }
	public double MaxWeightKg { get; set; }
}

public sealed class ExercisePersonalBestRow
{
	public Guid ProfileId { get; set; }
	public Guid ExerciseId { get; set; }
	public double MaxKg { get; set; }
	public int MaxReps { get; set; }
	public double MaxSessionVolume { get; set; }
	public DateTime UpdatedUtc { get; set; }
}

public sealed class PrEventRow
{
	public Guid Id { get; set; }
	public Guid ProfileId { get; set; }
	public Guid ExerciseId { get; set; }
	public string Kind { get; set; } = "weight";
	public double? WeightKg { get; set; }
	public int? Reps { get; set; }
	public string PerformanceLine { get; set; } = "";
	public Guid? CompletedWorkoutId { get; set; }
	public DateTime AchievedUtc { get; set; }
}

public sealed class ProfileExerciseRankRow
{
	public Guid ProfileId { get; set; }
	public Guid ExerciseId { get; set; }
	public double TheoreticalRaw { get; set; }
	public int? TheoreticalRank { get; set; }
	public int? ValidatedRank { get; set; }
	public bool HasSuccessfulValidation { get; set; }
	public int? CalibrationFloor { get; set; }
	public int? AvailableQuestRank { get; set; }
	public DateTime? QuestUnlockedUtc { get; set; }
	public double? BodyweightKgAtCompute { get; set; }
	public int? SexAtCompute { get; set; }
	public double? R10Used { get; set; }
	public string? StandardVersionId { get; set; }
	public DateTime UpdatedUtc { get; set; }
}

public sealed class MuscleRankSnapshotRow
{
	public Guid ProfileId { get; set; }
	public string MuscleId { get; set; } = "";
	public string StandardVersionId { get; set; } = "";
	public int? ValidatedRank { get; set; }
	public int? TheoreticalRank { get; set; }
	public int EvaluatedCount { get; set; }
	public DateTime UpdatedUtc { get; set; }
}

public sealed class GroupRankSnapshotRow
{
	public Guid ProfileId { get; set; }
	public string GroupId { get; set; } = "";
	public string StandardVersionId { get; set; } = "";
	public double? ValidatedRaw { get; set; }
	public int? ValidatedRank { get; set; }
	public int EvaluatedCount { get; set; }
	public int TotalCount { get; set; }
	public DateTime UpdatedUtc { get; set; }
}

public sealed class RankQuestAttemptRow
{
	public Guid Id { get; set; }
	public Guid ProfileId { get; set; }
	public Guid ExerciseId { get; set; }
	public int TargetRank { get; set; }
	public DateTime StartedUtc { get; set; }
	public DateTime ExpiresUtc { get; set; }
	public DateOnly LocalDate { get; set; }
	public string Status { get; set; } = "in_progress";
	public double? WeightKg { get; set; }
	public int? Reps { get; set; }
	public string? StandardVersionId { get; set; }
}
