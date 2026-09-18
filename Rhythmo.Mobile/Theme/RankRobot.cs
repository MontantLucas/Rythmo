using Rhythmo.Shared.Ranking;

namespace Rhythmo.Mobile.Theme;

internal static class RankRobot
{
	public static string FileName(int? rank)
	{
		var n = rank is >= ExerciseRankCalculator.MinRank and <= ExerciseRankCalculator.MaxRank
			? rank.Value
			: 1;
		return $"robot_r{n}.svg";
	}
}
