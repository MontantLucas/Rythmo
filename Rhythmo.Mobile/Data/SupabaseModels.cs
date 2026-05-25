using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace Rhythmo.Mobile.Data;

[Table("profiles")]
public sealed class ProfileRecord : BaseModel
{
	[PrimaryKey("id", false)]
	public Guid Id { get; set; }

	[Column("display_name")]
	public string DisplayName { get; set; } = "";

	[Column("biological_sex")]
	public int BiologicalSex { get; set; }

	[Column("weight_kg")]
	public double WeightKg { get; set; }

	[Column("height_cm")]
	public double? HeightCm { get; set; }

	[Column("age_years")]
	public int? AgeYears { get; set; }
}

[Table("exercises")]
public sealed class ExerciseRecord : BaseModel
{
	/// <summary><c>false</c> = l’id est fourni à l’insert (catalogue intégré, GUID stables).</summary>
	[PrimaryKey("id", false)]
	public Guid Id { get; set; }

	[Column("name_fr")]
	public string NameFr { get; set; } = "";

	[Column("category")]
	public string? Category { get; set; }

	[Column("met_approx")]
	public double MetApprox { get; set; }

	[Column("is_builtin")]
	public bool IsBuiltin { get; set; }

	[Column("created_by")]
	public Guid? CreatedBy { get; set; }
}

[Table("session_templates")]
public sealed class SessionTemplateRecord : BaseModel
{
	[PrimaryKey("id")]
	public Guid Id { get; set; }

	[Column("owner_id")]
	public Guid OwnerId { get; set; }

	[Column("title")]
	public string Title { get; set; } = "";

	[Column("created_utc")]
	public DateTime CreatedUtc { get; set; }

	[Column("updated_utc")]
	public DateTime UpdatedUtc { get; set; }
}

[Table("session_exercises")]
public sealed class SessionExerciseRecord : BaseModel
{
	[PrimaryKey("id")]
	public Guid Id { get; set; }

	[Column("session_id")]
	public Guid SessionId { get; set; }

	[Column("exercise_id")]
	public Guid ExerciseId { get; set; }

	[Column("sort_order")]
	public int SortOrder { get; set; }

	[Column("target_sets")]
	public int TargetSets { get; set; }

	[Column("target_reps")]
	public int? TargetReps { get; set; }
}

[Table("session_last_snapshots")]
public sealed class SessionLastSnapshotRecord : BaseModel
{
	[PrimaryKey("session_id")]
	public Guid SessionId { get; set; }

	[Column("json")]
	public string Json { get; set; } = "{}";

	[Column("saved_utc")]
	public DateTime SavedUtc { get; set; }
}

[Table("completed_workouts")]
public sealed class CompletedWorkoutRecord : BaseModel
{
	/// <summary><c>false</c> = l’id client est envoyé à l’insert (aligné sur <see cref="ExerciseRecord"/>).</summary>
	[PrimaryKey("id", false)]
	public Guid Id { get; set; }

	[Column("profile_id")]
	public Guid ProfileId { get; set; }

	[Column("completed_utc")]
	public DateTime CompletedUtc { get; set; }

	[Column("calories_rounded")]
	public double CaloriesRounded { get; set; }

	[Column("session_title")]
	public string SessionTitle { get; set; } = "";

	[Column("source_session_template_id")]
	public Guid? SourceSessionTemplateId { get; set; }

	[Column("payload_json")]
	public string? PayloadJson { get; set; }
}

[Table("exercise_last_weights")]
public sealed class ExerciseLastWeightRecord : BaseModel
{
	[Column("profile_id")]
	public Guid ProfileId { get; set; }

	[Column("exercise_id")]
	public Guid ExerciseId { get; set; }

	[Column("weight_kg")]
	public double WeightKg { get; set; }

	[Column("updated_utc")]
	public DateTime UpdatedUtc { get; set; }
}

[Table("exercise_performance_daily")]
public sealed class ExercisePerformanceDailyRecord : BaseModel
{
	[Column("profile_id")]
	public Guid ProfileId { get; set; }

	[Column("exercise_id")]
	public Guid ExerciseId { get; set; }

	[Column("performance_date")]
	public DateOnly PerformanceDate { get; set; }

	[Column("max_weight_kg")]
	public double MaxWeightKg { get; set; }
}

[Table("exercise_personal_bests")]
public sealed class ExercisePersonalBestRecord : BaseModel
{
	[Column("profile_id")]
	public Guid ProfileId { get; set; }

	[Column("exercise_id")]
	public Guid ExerciseId { get; set; }

	[Column("max_kg")]
	public double MaxKg { get; set; }

	[Column("max_reps")]
	public int MaxReps { get; set; }

	[Column("max_session_volume")]
	public double MaxSessionVolume { get; set; }

	[Column("updated_utc")]
	public DateTime UpdatedUtc { get; set; }
}

[Table("pr_events")]
public sealed class PrEventRecord : BaseModel
{
	[PrimaryKey("id", false)]
	public Guid Id { get; set; }

	[Column("profile_id")]
	public Guid ProfileId { get; set; }

	[Column("exercise_id")]
	public Guid ExerciseId { get; set; }

	[Column("kind")]
	public string Kind { get; set; } = "weight";

	[Column("weight_kg")]
	public double? WeightKg { get; set; }

	[Column("reps")]
	public int? Reps { get; set; }

	[Column("performance_line")]
	public string PerformanceLine { get; set; } = "";

	[Column("completed_workout_id")]
	public Guid? CompletedWorkoutId { get; set; }

	[Column("achieved_utc")]
	public DateTime AchievedUtc { get; set; }
}
