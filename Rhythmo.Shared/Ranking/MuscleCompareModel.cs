namespace Rhythmo.Shared.Ranking;

public sealed record CompareSnapshot(string Id, int? ValidatedRank);

public sealed record MuscleCompareSide(
	int EvaluatedCount,
	int MuscleCount,
	IReadOnlyDictionary<string, int?> GroupRanks,
	IReadOnlyDictionary<string, int?> MuscleRanks)
{
	public string EvaluatedLabel => $"{EvaluatedCount}/{MuscleCount} sous-muscles évalués";
}

public static class MuscleCompareModel
{
	public static MuscleCompareSide Build(
		IEnumerable<CompareSnapshot> groups,
		IEnumerable<CompareSnapshot> muscles)
	{
		var groupMap = groups.GroupBy(g => g.Id).ToDictionary(g => g.Key, g => g.First().ValidatedRank);
		var muscleMap = muscles.GroupBy(m => m.Id).ToDictionary(m => m.Key, m => m.First().ValidatedRank);

		var groupRanks = new Dictionary<string, int?>();
		foreach (var group in MuscleIds.Groups)
			groupRanks[group.Id] = groupMap.TryGetValue(group.Id, out var rank) ? rank : null;

		var muscleRanks = new Dictionary<string, int?>();
		var evaluated = 0;
		foreach (var muscle in MuscleIds.Muscles)
		{
			int? rank = muscleMap.TryGetValue(muscle.Id, out var value) ? value : null;
			muscleRanks[muscle.Id] = rank;
			if (rank is not null)
				evaluated++;
		}

		return new MuscleCompareSide(evaluated, MuscleIds.Muscles.Count, groupRanks, muscleRanks);
	}
}
