using System.Text.Json;
using Microsoft.Maui.Storage;
using Rhythmo.Shared.Contracts;

namespace Rhythmo.Mobile.Services;

public sealed class WorkoutDraftStore
{
	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		PropertyNameCaseInsensitive = true,
		WriteIndented = false
	};

	private readonly Lock _fileLock = new();

	public bool HasDraft(Guid profileId) => File.Exists(GetPath(profileId));

	public WorkoutDraftEnvelope? TryLoad(Guid profileId)
	{
		var path = GetPath(profileId);
		if (!File.Exists(path))
			return null;

		try
		{
			var json = File.ReadAllText(path);
			return JsonSerializer.Deserialize<WorkoutDraftEnvelope>(json, JsonOptions);
		}
		catch
		{
			return null;
		}
	}

	public void Save(WorkoutDraftEnvelope draft)
	{
		var path = GetPath(draft.ProfileId);
		var dir = Path.GetDirectoryName(path)!;
		Directory.CreateDirectory(dir);

		var json = JsonSerializer.Serialize(draft, JsonOptions);
		var tempPath = path + ".tmp";

		lock (_fileLock)
		{
			File.WriteAllText(tempPath, json);
			if (File.Exists(path))
				File.Delete(path);
			File.Move(tempPath, path);
		}
	}

	public void Clear(Guid profileId)
	{
		var path = GetPath(profileId);
		lock (_fileLock)
		{
			if (File.Exists(path))
				File.Delete(path);
		}
	}

	static string GetPath(Guid profileId) =>
		Path.Combine(GetDraftsDirectory(), $"{profileId:N}_active_workout.json");

	static string GetDraftsDirectory()
	{
		try
		{
			return Path.Combine(FileSystem.AppDataDirectory, "drafts");
		}
		catch
		{
			return Path.Combine(
				Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
				"com.rhythmo.mobile",
				"drafts");
		}
	}
}
