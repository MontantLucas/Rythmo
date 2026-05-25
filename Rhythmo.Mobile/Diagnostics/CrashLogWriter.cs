using System.Text;
using Microsoft.Maui.Storage;

namespace Rhythmo.Mobile.Diagnostics;

/// <summary>Écriture append-only UTF-8 des exceptions sous le dossier données de l’app (logs/rhythmo_exceptions.log).</summary>
public static class CrashLogWriter
{
	private static readonly Lock FileLock = new();

	/// <summary>Chemin exact du dernier fichier log tenté (utile au support).</summary>
	public static string? LastResolvedPath { get; private set; }

	public static void TryAppend(string context, Exception exception, string? extraLine = null)
	{
		try
		{
			string platform;
			string appInfo;
			try
			{
				platform = $"{DeviceInfo.Platform} {DeviceInfo.VersionString}";
				appInfo = $"{AppInfo.Name} v{AppInfo.VersionString} build:{AppInfo.BuildString}";
			}
			catch
			{
				platform = "n/d";
				appInfo = "Rhythmo (infos app indisponibles au moment du log)";
			}

			var sb = new StringBuilder(512);
			sb.Append(DateTime.UtcNow.ToString("O")).Append('\n')
				.Append("Contexte: ").Append(context).Append('\n')
				.Append("Plateforme: ").Append(platform).Append('\n')
				.Append("App: ").Append(appInfo).Append('\n');

			if (!string.IsNullOrWhiteSpace(extraLine))
				sb.Append("Détail: ").Append(extraLine).Append('\n');

			sb.Append('\n').Append(exception).Append("\n").AppendLine(new string('-', 72));

			var path = GetLogFilePath(out var ensuredDir);
			Directory.CreateDirectory(ensuredDir);
			lock (FileLock)
				File.AppendAllText(path, sb.ToString(), Encoding.UTF8);
		}
		catch
		{
			// Ne jamais relancer depuis un chemin critique.
		}
	}

	public static string GetLogDirectory()
	{
		_ = GetLogFilePath(out var dir);
		return dir;
	}

	private static string GetLogFilePath(out string logsDirectory)
	{
		logsDirectory = Path.Combine(GetAppBaseDirectory(), "logs");
		LastResolvedPath = Path.Combine(logsDirectory, "rhythmo_exceptions.log");
		return LastResolvedPath;
	}

	private static string GetAppBaseDirectory()
	{
		try
		{
			return FileSystem.AppDataDirectory;
		}
		catch
		{
			return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
				"com.rhythmo.mobile");
		}
	}
}
