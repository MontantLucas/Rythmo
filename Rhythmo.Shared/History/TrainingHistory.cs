using Rhythmo.Shared.Ranking;

namespace Rhythmo.Shared.History;

public sealed record HistorySet(int Reps, double WeightKg);

public sealed record HistoryExerciseBlock(Guid ExerciseId, IReadOnlyList<HistorySet> Sets);

public sealed record HistoryWorkout(
	Guid Id,
	DateTime CompletedUtc,
	string Title,
	IReadOnlyList<HistoryExerciseBlock> Blocks);

public sealed record HistoryExerciseInfo(string NameFr, string? GroupId);

public sealed record HistoryPr(Guid ExerciseId, Guid? CompletedWorkoutId, string Kind);

public sealed record SetGroupLine(double WeightKg, IReadOnlyList<int> Reps, double VolumeKg);

public sealed record ExerciseDayLine(
	DateOnly Day,
	Guid WorkoutId,
	IReadOnlyList<SetGroupLine> Lines,
	IReadOnlyList<string> PrKinds);

public sealed record ExerciseHistoryRow(
	Guid ExerciseId,
	string Name,
	string? GroupId,
	double VolumeKg,
	IReadOnlyList<ExerciseDayLine> Days);

public sealed record ExerciseHistoryGroup(
	string? GroupId,
	string Name,
	IReadOnlyList<ExerciseHistoryRow> Exercises);

public sealed record SessionHistoryCard(
	Guid WorkoutId,
	string Title,
	DateOnly Day,
	double VolumeKg,
	bool HasSets);

public sealed record TrainingHistoryAggregate(
	int SessionCount,
	int DistinctExerciseCount,
	double VolumeKg,
	IReadOnlyList<ExerciseHistoryGroup> ExerciseGroups,
	IReadOnlyList<SessionHistoryCard> Sessions);

public static class TrainingHistory
{
	public const string UnclassifiedName = "Non classé";
	public const string UnknownExerciseName = "Exercice inconnu";

	public static TrainingHistoryAggregate Build(
		IReadOnlyList<HistoryWorkout> workouts,
		TrainingPeriodWindow window,
		TimeZoneInfo zone,
		Func<Guid, HistoryExerciseInfo?> resolve,
		IReadOnlyList<HistoryPr>? personalRecords = null,
		string? groupFilter = null)
	{
		var prs = personalRecords ?? [];
		var inWindow = workouts
			.Where(w => window.Contains(LocalDate(w.CompletedUtc, zone)))
			.ToList();

		var exerciseRows = new Dictionary<Guid, MutableExercise>();
		var sessions = new List<SessionHistoryCard>();

		foreach (var workout in inWindow.OrderByDescending(w => w.CompletedUtc))
		{
			var day = LocalDate(workout.CompletedUtc, zone);
			var matchingVolume = 0d;
			var matchingExerciseIds = new HashSet<Guid>();
			var hasAnySet = false;
			var dayLines = new Dictionary<Guid, List<(double Kg, int Reps)>>();

			foreach (var block in workout.Blocks)
			{
				if (block.Sets.Count == 0)
					continue;
				hasAnySet = true;
				var info = resolve(block.ExerciseId);
				var groupId = info?.GroupId;
				if (!Matches(groupId, groupFilter))
					continue;

				matchingExerciseIds.Add(block.ExerciseId);
				if (!exerciseRows.TryGetValue(block.ExerciseId, out var row))
				{
					row = new MutableExercise(
						block.ExerciseId,
						info?.NameFr ?? UnknownExerciseName,
						groupId);
					exerciseRows[block.ExerciseId] = row;
				}

				if (!dayLines.TryGetValue(block.ExerciseId, out var sets))
				{
					sets = [];
					dayLines[block.ExerciseId] = sets;
				}

				foreach (var set in block.Sets)
				{
					var vol = set.Reps * set.WeightKg;
					matchingVolume += vol;
					row.Volume += vol;
					sets.Add((set.WeightKg, set.Reps));
				}
			}

			foreach (var (exerciseId, sets) in dayLines)
			{
				var kinds = prs
					.Where(p => p.CompletedWorkoutId == workout.Id && p.ExerciseId == exerciseId)
					.Select(p => p.Kind)
					.Distinct(StringComparer.Ordinal)
					.ToList();
				exerciseRows[exerciseId].Days.Add(new ExerciseDayLine(
					day,
					workout.Id,
					GroupSets(sets),
					kinds));
			}

			var includeSession = groupFilter is null
				? true
				: matchingExerciseIds.Count > 0;
			if (!includeSession)
				continue;

			sessions.Add(new SessionHistoryCard(
				workout.Id,
				string.IsNullOrWhiteSpace(workout.Title) ? "Séance" : workout.Title,
				day,
				matchingVolume,
				groupFilter is null ? hasAnySet : matchingExerciseIds.Count > 0));
		}

		var groups = exerciseRows.Values
			.GroupBy(e => e.GroupId)
			.Select(g => new ExerciseHistoryGroup(
				g.Key,
				GroupName(g.Key),
				g.OrderBy(e => e.Name, StringComparer.CurrentCulture)
					.Select(e => new ExerciseHistoryRow(
						e.Id,
						e.Name,
						e.GroupId,
						e.Volume,
						e.Days.OrderByDescending(d => d.Day).ToList()))
					.ToList()))
			.OrderBy(g => GroupSort(g.GroupId))
			.ToList();

		var sessionCount = sessions.Count;
		var distinct = exerciseRows.Count;
		var volume = exerciseRows.Values.Sum(e => e.Volume);
		return new TrainingHistoryAggregate(sessionCount, distinct, volume, groups, sessions);
	}

	public static DateOnly LocalDate(DateTime completedUtc, TimeZoneInfo zone)
	{
		var utc = completedUtc.Kind switch
		{
			DateTimeKind.Utc => completedUtc,
			DateTimeKind.Local => completedUtc.ToUniversalTime(),
			_ => DateTime.SpecifyKind(completedUtc, DateTimeKind.Utc)
		};
		return DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(utc, zone));
	}

	public static bool Contains(this TrainingPeriodWindow window, DateOnly day) =>
		day >= window.Start && day <= window.End;

	private static bool Matches(string? groupId, string? filter) =>
		filter is null || groupId == filter;

	private static IReadOnlyList<SetGroupLine> GroupSets(List<(double Kg, int Reps)> sets)
	{
		return sets
			.GroupBy(s => s.Kg)
			.Select(g => new SetGroupLine(g.Key, g.Select(s => s.Reps).ToList(), g.Sum(s => s.Reps * s.Kg)))
			.ToList();
	}

	private static string GroupName(string? groupId)
	{
		if (groupId is null)
			return UnclassifiedName;
		foreach (var group in MuscleIds.Groups)
		{
			if (group.Id == groupId)
				return group.NameFr;
		}
		return UnclassifiedName;
	}

	private static int GroupSort(string? groupId)
	{
		if (groupId is null)
			return 99;
		foreach (var group in MuscleIds.Groups)
		{
			if (group.Id == groupId)
				return group.Sort;
		}
		return 99;
	}

	private sealed class MutableExercise(Guid id, string name, string? groupId)
	{
		public Guid Id { get; } = id;
		public string Name { get; } = name;
		public string? GroupId { get; } = groupId;
		public double Volume { get; set; }
		public List<ExerciseDayLine> Days { get; } = [];
	}
}
