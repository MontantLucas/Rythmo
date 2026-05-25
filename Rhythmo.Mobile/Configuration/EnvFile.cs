using Microsoft.Maui.Storage;

namespace Rhythmo.Mobile.Configuration;

/// <summary>Charge un fichier .env (KEY=value) sans dépendance externe.</summary>
public static class EnvFile
{
	static readonly string[] AppPackageEnvNames = [".env", "supabase.env"];

	public static string? FindEnvPath()
	{
		var dir = new DirectoryInfo(AppContext.BaseDirectory);
		for (var i = 0; i < 8 && dir is not null; i++, dir = dir.Parent)
		{
			var candidate = Path.Combine(dir.FullName, ".env");
			if (File.Exists(candidate))
				return candidate;
		}

		var cwd = Directory.GetCurrentDirectory();
		var cwdEnv = Path.Combine(cwd, ".env");
		return File.Exists(cwdEnv) ? cwdEnv : null;
	}

	public static IReadOnlyDictionary<string, string> Load(string path) =>
		Parse(File.ReadAllLines(path));

	public static void TryLoadAll()
	{
		var path = FindEnvPath();
		if (path is not null)
			Load(path);
		else
			TryLoadFromAppPackage();
	}

	/// <summary>Windows / dev local : remonte depuis le dossier de l’exe.</summary>
	public static void TryLoadFromRepoRoot() => TryLoadAll();

	static void TryLoadFromAppPackage()
	{
		foreach (var name in AppPackageEnvNames)
		{
			if (TryLoadAppPackageFile(name))
				return;
		}
	}

	static bool TryLoadAppPackageFile(string fileName)
	{
		try
		{
			using var stream = FileSystem.OpenAppPackageFileAsync(fileName).GetAwaiter().GetResult();
			using var reader = new StreamReader(stream);
			Parse(reader.ReadToEnd().Split('\n', '\r'));
			return true;
		}
		catch (FileNotFoundException)
		{
			return false;
		}
		catch
		{
			// Ignorer : la clé peut venir des variables d'environnement système.
			return false;
		}
	}

	static IReadOnlyDictionary<string, string> Parse(IEnumerable<string> lines)
	{
		var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		foreach (var raw in lines)
		{
			var line = raw.Trim();
			if (line.Length == 0 || line.StartsWith('#'))
				continue;
			var eq = line.IndexOf('=');
			if (eq <= 0)
				continue;
			var key = line[..eq].Trim();
			var val = line[(eq + 1)..].Trim().Trim('"');
			map[key] = val;
			Environment.SetEnvironmentVariable(key, val);
		}

		return map;
	}
}
