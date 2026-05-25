using System.Text.Json;
using Rhythmo.Shared.Contracts;

namespace Rhythmo.Mobile.Services;

/// <summary>Serialisation locale du dernier même format que l’ancienne charge API.</summary>
public static class CompletedWorkoutSnapshot
{
	internal static readonly JsonSerializerOptions JsonWrite = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
	};

	private static readonly JsonSerializerOptions JsonRead = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		PropertyNameCaseInsensitive = true
	};

	public static string SerializeRequest(WorkoutCompletedRequest body) =>
		JsonSerializer.Serialize(body, JsonWrite);

	public static WorkoutCompletedRequest? DeserializeRequestSnapshot(string? json)
	{
		if (string.IsNullOrWhiteSpace(json))
			return null;

		return JsonSerializer.Deserialize<WorkoutCompletedRequest>(json, JsonRead);
	}
}
