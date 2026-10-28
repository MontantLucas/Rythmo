namespace Rhythmo.Shared.Resilience;

/// <summary>
/// Décide quand retenter. Hors ligne : pas de polling, on attend un réveil (radio ou déverrouillage).
/// En ligne : tout de suite, puis 250 ms, 500 ms, et 1 s au maximum.
/// </summary>
public static class ReconnectPolicy
{
	public static TimeSpan? NextDelay(int consecutiveFailures, bool hasInternet, bool wake)
	{
		if (!hasInternet)
			return null;

		if (wake)
			return TimeSpan.Zero;

		return consecutiveFailures switch
		{
			<= 0 => TimeSpan.Zero,
			1 => TimeSpan.FromMilliseconds(250),
			2 => TimeSpan.FromMilliseconds(500),
			_ => TimeSpan.FromSeconds(1)
		};
	}

	public static DateTimeOffset? NextAttemptAt(
		DateTimeOffset now,
		int consecutiveFailures,
		bool hasInternet,
		bool wake)
	{
		var delay = NextDelay(consecutiveFailures, hasInternet, wake);
		return delay is null ? null : now + delay.Value;
	}
}
