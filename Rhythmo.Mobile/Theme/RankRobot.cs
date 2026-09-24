using Microsoft.Maui.Graphics;
using Microsoft.Maui.Graphics.Platform;
using Microsoft.Maui.Storage;
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

	public static async Task<Microsoft.Maui.Graphics.IImage?> LoadAsync(int? rank, bool back, CancellationToken ct = default)
	{
		var logical = FileName(rank, back);
		var baseName = Path.GetFileNameWithoutExtension(logical);

		foreach (var name in new[] { $"{baseName}.png", logical, baseName })
		{
			ct.ThrowIfCancellationRequested();
			try
			{
				await using var stream = await FileSystem.OpenAppPackageFileAsync(name).ConfigureAwait(false);
				return PlatformImage.FromStream(stream);
			}
			catch
			{
				// Essaye le candidat suivant.
			}
		}

		// Windows unpackaged : Resizetizer produit robot_r4.scale-200.png à côté de l'exe.
		try
		{
			var dir = AppContext.BaseDirectory;
			var match = PickBestScale(Directory.EnumerateFiles(dir, $"{baseName}.scale-*.png"));
			if (match is null)
				return null;

			ct.ThrowIfCancellationRequested();
			await using var stream = File.OpenRead(match);
			return PlatformImage.FromStream(stream);
		}
		catch
		{
			return null;
		}
	}

	private static string? PickBestScale(IEnumerable<string> files)
	{
		string? best = null;
		var bestScale = -1;
		foreach (var path in files)
		{
			var name = Path.GetFileNameWithoutExtension(path);
			var idx = name.LastIndexOf(".scale-", StringComparison.OrdinalIgnoreCase);
			if (idx < 0)
				continue;
			if (!int.TryParse(name.AsSpan(idx + ".scale-".Length), out var scale))
				continue;
			// Préfère 200, sinon le plus grand disponible.
			var score = scale == 200 ? 10_000 : scale;
			if (score <= bestScale)
				continue;
			bestScale = score;
			best = path;
		}

		return best;
	}
}
