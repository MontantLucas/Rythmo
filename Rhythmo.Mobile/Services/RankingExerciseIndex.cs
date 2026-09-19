using Rhythmo.Mobile.Data;
using Rhythmo.Shared.Ranking;

namespace Rhythmo.Mobile.Services;

public static class RankingExerciseIndex
{
	public static IReadOnlyDictionary<Guid, ExerciseRankingDef> Map(IEnumerable<CachedExerciseRow> exercises)
	{
		var map = new Dictionary<Guid, ExerciseRankingDef>();
		foreach (var ex in exercises)
		{
			var def = MuscleRankingCatalog.Find(ex.Category ?? "", ex.NameFr);
			if (def is not null)
				map[ex.Id] = def;
		}

		return map;
	}

	public static string WeightPlaceholder(LoadMode mode) => mode switch
	{
		LoadMode.PerDumbbell => "kg / haltère",
		LoadMode.AddedLoad => "kg ajoutés",
		_ => "kg"
	};
}
