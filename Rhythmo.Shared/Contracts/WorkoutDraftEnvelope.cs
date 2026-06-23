namespace Rhythmo.Shared.Contracts;

/// <summary>Brouillon local d'une séance en cours (saisie partielle autorisée).</summary>
public sealed record WorkoutDraftEnvelope(
	Guid ProfileId,
	Guid SessionId,
	string SessionTitle,
	DateTime StartedUtc,
	DateTime UpdatedUtc,
	int CurrentExerciseIndex,
	IReadOnlyList<WorkoutDraftExerciseDto> Exercises);

public sealed record WorkoutDraftExerciseDto(
	Guid ExerciseId,
	IReadOnlyList<WorkoutDraftSetDto> Sets);

public sealed record WorkoutDraftSetDto(
	string RepsText,
	string KgText,
	bool IsDone);
