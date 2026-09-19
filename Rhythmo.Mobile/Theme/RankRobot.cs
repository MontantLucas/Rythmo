using Rhythmo.Shared.Ranking;

namespace Rhythmo.Mobile.Theme;

internal static class RankRobot
{
	public static string FileName(int? rank, bool back = false)
	{
		var n = rank is >= ExerciseRankCalculator.MinRank and <= ExerciseRankCalculator.MaxRank
			? rank.Value
			: 1;
		return back ? $"robot_r{n}_back.svg" : $"robot_r{n}.svg";
	}
}
