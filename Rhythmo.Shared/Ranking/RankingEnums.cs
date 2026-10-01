namespace Rhythmo.Shared.Ranking;

public enum RankClassification
{
	NoPerformance = 0,
	BelowR1 = 1,
	Classified = 2
}

public enum MeasurementType
{
	OneRm = 1,
	FiveRm = 5,
	TenRm = 10
}

public enum LoadMode
{
	Total = 0,
	PerDumbbell = 1,
	AddedLoad = 2
}

public enum StandardSource
{
	StrengthLevel = 0,
	RhythmoV1Manual = 1,
	RhythmoEstimated = 2
}

public enum Comparability
{
	High = 0,
	Medium = 1,
	Low = 2
}

public enum MuscleRole
{
	Primary = 0,
	Secondary = 1
}

public enum BiologicalSexKind
{
	Male = 0,
	Female = 1
}

public enum QuestAttemptStatus
{
	InProgress = 0,
	Succeeded = 1,
	FailedExpired = 2,
	FailedUser = 3
}
