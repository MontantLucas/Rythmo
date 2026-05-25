using System.Collections.Concurrent;

namespace Rhythmo.Mobile.Services;

/// <summary>Cache mémoire léger pour réduire les allers-retours Supabase sur données peu volatiles.</summary>
public sealed class RhythmoMemoryCache
{
	private sealed record Entry(object Value, DateTime ExpiresUtc);

	private readonly ConcurrentDictionary<string, Entry> _entries = new();

	public async Task<T> GetOrLoadAsync<T>(
		string key,
		TimeSpan ttl,
		Func<CancellationToken, Task<T>> loader,
		CancellationToken ct = default)
	{
		if (_entries.TryGetValue(key, out var hit) && hit.ExpiresUtc > DateTime.UtcNow)
			return (T)hit.Value;

		var value = await loader(ct).ConfigureAwait(false);
		_entries[key] = new Entry(value!, DateTime.UtcNow.Add(ttl));
		return value;
	}

	public void Remove(string key) => _entries.TryRemove(key, out _);

	public void InvalidateExercises() => Remove("exercises");

	public void InvalidateProfile(Guid userId) => Remove(ProfileKey(userId));

	public static string ProfileKey(Guid userId) => $"profile:{userId}";
}
