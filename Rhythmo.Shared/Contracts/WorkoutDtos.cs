namespace Rhythmo.Shared.Contracts;

public sealed record WorkoutCompletedRequest(
    DateTime CompletedAtUtc,
    double TotalCaloriesRounded,
    double? EstimatedDurationMinutes,
    string? Notes,
    IReadOnlyList<CompletedExerciseSetsDto>? Exercises);

public sealed record CompletedExerciseSetsDto(
    Guid ExerciseId,
    IReadOnlyList<SetDto> Sets);

public sealed record SetDto(
    int Reps,
    double WeightKg,
    int SetNumber = 0);
