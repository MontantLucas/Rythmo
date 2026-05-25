namespace Rhythmo.Shared.Contracts;

/// <summary>Serialized JSON envelope for « dernier run » per session template.</summary>
public sealed record LastRunEnvelope(IReadOnlyList<CompletedExerciseSetsDto>? Exercises);
