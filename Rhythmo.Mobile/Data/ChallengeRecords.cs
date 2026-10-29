using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace Rhythmo.Mobile.Data;

[Table("cardio_activities")]
public sealed class CardioActivityRecord : BaseModel
{
	/// <summary>Postgres génère l’id. Ne pas l’envoyer.</summary>
	[PrimaryKey("id", false)]
	public Guid Id { get; set; }

	[Column("profile_id")]
	public Guid ProfileId { get; set; }

	[Column("performed_utc")]
	public DateTime PerformedUtc { get; set; }

	[Column("distance_m")]
	public double DistanceM { get; set; }

	[Column("duration_s")]
	public int DurationS { get; set; }
}

[Table("weekly_challenge_assignments")]
public sealed class WeeklyChallengeAssignmentRecord : BaseModel
{
	/// <summary>Postgres génère l’id. Ne pas l’envoyer.</summary>
	[PrimaryKey("id", false)]
	public Guid Id { get; set; }

	[Column("profile_id")]
	public Guid ProfileId { get; set; }

	[Column("iso_year")]
	public int IsoYear { get; set; }

	[Column("iso_week")]
	public int IsoWeek { get; set; }

	[Column("slot")]
	public int Slot { get; set; }

	[Column("template_id")]
	public string TemplateId { get; set; } = "";

	[Column("target_json")]
	public string? TargetJson { get; set; }

	[Column("status")]
	public string Status { get; set; } = "offered";

	[Column("started_utc")]
	public DateTime? StartedUtc { get; set; }

	[Column("expires_utc")]
	public DateTime ExpiresUtc { get; set; }
}

[Table("challenge_point_awards")]
public sealed class ChallengePointAwardRecord : BaseModel
{
	/// <summary>C’est l’id du défi, pas un identifiant aléatoire. Il faut l’envoyer.</summary>
	[PrimaryKey("assignment_id", true)]
	public Guid AssignmentId { get; set; }

	[Column("profile_id")]
	public Guid ProfileId { get; set; }

	[Column("points")]
	public int Points { get; set; }

	[Column("awarded_utc")]
	public DateTime AwardedUtc { get; set; }
}
