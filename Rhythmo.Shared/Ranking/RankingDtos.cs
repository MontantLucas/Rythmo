namespace Rhythmo.Shared.Ranking;

public sealed record ExerciseRankResult(
	Guid ExerciseId,
	RankClassification Classification,
	double TheoreticalRaw,
	int? TheoreticalRank,
	int? ValidatedRank,
	bool HasSuccessfulValidation,
	int? CalibrationFloor,
	int? AvailableQuestRank,
	double R10Used,
	string StandardVersionId);

public sealed record RankStandardDto(
	Guid ExerciseId,
	MeasurementType MeasurementType,
	LoadMode LoadMode,
	StandardSource Source,
	Comparability Comparability,
	string Confidence,
	double R10Male70,
	double R10Male100,
	double R10Female60,
	double R10Female90);

public sealed record QuestStateDto(
	Guid ExerciseId,
	int TargetRank,
	bool IsAvailable,
	bool AttemptedToday,
	string ExerciseNameFr);
