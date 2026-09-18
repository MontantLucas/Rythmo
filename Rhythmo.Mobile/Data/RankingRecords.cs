using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace Rhythmo.Mobile.Data;

[Table("profile_exercise_ranks")]
public sealed class ProfileExerciseRankRecord : BaseModel
{
	[Column("profile_id")]
	public Guid ProfileId { get; set; }

	[Column("exercise_id")]
	public Guid ExerciseId { get; set; }

	[Column("theoretical_raw")]
	public double TheoreticalRaw { get; set; }

	[Column("theoretical_rank")]
	public int? TheoreticalRank { get; set; }

	[Column("validated_rank")]
	public int? ValidatedRank { get; set; }

	[Column("has_successful_validation")]
	public bool HasSuccessfulValidation { get; set; }

	[Column("calibration_floor")]
	public int? CalibrationFloor { get; set; }

	[Column("available_quest_rank")]
	public int? AvailableQuestRank { get; set; }

	[Column("quest_unlocked_utc")]
	public DateTime? QuestUnlockedUtc { get; set; }

	[Column("bodyweight_kg_at_compute")]
	public double? BodyweightKgAtCompute { get; set; }

	[Column("sex_at_compute")]
	public int? SexAtCompute { get; set; }

	[Column("r10_used")]
	public double? R10Used { get; set; }

	[Column("standard_version_id")]
	public string? StandardVersionId { get; set; }

	[Column("updated_utc")]
	public DateTime UpdatedUtc { get; set; }
}

[Table("profile_muscle_rank_snapshots")]
public sealed class MuscleRankSnapshotRecord : BaseModel
{
	[Column("profile_id")]
	public Guid ProfileId { get; set; }

	[Column("muscle_id")]
	public string MuscleId { get; set; } = "";

	[Column("standard_version_id")]
	public string StandardVersionId { get; set; } = "";

	[Column("validated_rank")]
	public int? ValidatedRank { get; set; }

	[Column("theoretical_rank")]
	public int? TheoreticalRank { get; set; }

	[Column("evaluated_count")]
	public int EvaluatedCount { get; set; }

	[Column("updated_utc")]
	public DateTime UpdatedUtc { get; set; }
}

[Table("profile_group_rank_snapshots")]
public sealed class GroupRankSnapshotRecord : BaseModel
{
	[Column("profile_id")]
	public Guid ProfileId { get; set; }

	[Column("group_id")]
	public string GroupId { get; set; } = "";

	[Column("standard_version_id")]
	public string StandardVersionId { get; set; } = "";

	[Column("validated_raw")]
	public double? ValidatedRaw { get; set; }

	[Column("validated_rank")]
	public int? ValidatedRank { get; set; }

	[Column("evaluated_count")]
	public int EvaluatedCount { get; set; }

	[Column("total_count")]
	public int TotalCount { get; set; }

	[Column("updated_utc")]
	public DateTime UpdatedUtc { get; set; }
}

[Table("rank_quest_attempts")]
public sealed class RankQuestAttemptRecord : BaseModel
{
	/// <summary><c>true</c> = l’id client est envoyé à l’insert, sinon Postgres en génère un autre et le succès ne ferme plus la tentative.</summary>
	[PrimaryKey("id", true)]
	public Guid Id { get; set; }

	[Column("profile_id")]
	public Guid ProfileId { get; set; }

	[Column("exercise_id")]
	public Guid ExerciseId { get; set; }

	[Column("target_rank")]
	public int TargetRank { get; set; }

	[Column("started_utc")]
	public DateTime StartedUtc { get; set; }

	[Column("expires_utc")]
	public DateTime ExpiresUtc { get; set; }

	[Column("local_date")]
	public DateOnly LocalDate { get; set; }

	[Column("status")]
	public string Status { get; set; } = "in_progress";

	[Column("weight_kg")]
	public double? WeightKg { get; set; }

	[Column("reps")]
	public int? Reps { get; set; }

	[Column("standard_version_id")]
	public string? StandardVersionId { get; set; }
}
