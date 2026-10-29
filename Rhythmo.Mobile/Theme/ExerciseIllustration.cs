using Rhythmo.Mobile.Data;
using Rhythmo.Shared.History;

namespace Rhythmo.Mobile.Theme;

public static class ExerciseIllustration
{
	private static readonly Dictionary<Guid, (string Category, string Name)> ById =
		LocalExerciseSeed.CreateRows().ToDictionary(
			r => r.Id,
			r => (r.Category ?? "", r.NameFr));

	public static string For(Guid exerciseId)
	{
		if (!ById.TryGetValue(exerciseId, out var row))
			return ExerciseArt.Generic;
		return ExerciseArt.Resolve(row.Category, row.Name);
	}

	public static string ForGroup(string? groupId) =>
		string.IsNullOrWhiteSpace(groupId) ? ExerciseArt.Generic : ExerciseArt.GroupFile(groupId);

	public static ImageSource SourceFor(Guid exerciseId) => Load(For(exerciseId));

	public static ImageSource SourceForGroup(string? groupId) => Load(ForGroup(groupId));

	public static ImageSource Load(string logicalName)
	{
		// Windows non packagé : Image.Source cherche ms-appx, qui ne voit pas les fichiers à côté de l'exe.
		var file = UnpackagedScaleFile(logicalName);
		return ImageSource.FromFile(file ?? logicalName);
	}

	private static string? UnpackagedScaleFile(string logicalName)
	{
		var baseName = Path.GetFileNameWithoutExtension(logicalName);
		if (string.IsNullOrWhiteSpace(baseName))
			return null;

		var dir = AppContext.BaseDirectory;
		if (!Directory.Exists(dir))
			return null;

		string? best = null;
		var bestScore = -1;
		foreach (var path in Directory.EnumerateFiles(dir, baseName + ".scale-*.*"))
		{
			var ext = Path.GetExtension(path);
			if (!ext.Equals(".png", StringComparison.OrdinalIgnoreCase) &&
			    !ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase) &&
			    !ext.Equals(".jpeg", StringComparison.OrdinalIgnoreCase))
				continue;

			var name = Path.GetFileNameWithoutExtension(path);
			var idx = name.LastIndexOf(".scale-", StringComparison.OrdinalIgnoreCase);
			if (idx < 0 || !int.TryParse(name.AsSpan(idx + ".scale-".Length), out var scale))
				continue;

			var score = scale == 200 ? 10_000 : scale;
			if (score <= bestScore)
				continue;
			bestScore = score;
			best = path;
		}

		return best;
	}
}
