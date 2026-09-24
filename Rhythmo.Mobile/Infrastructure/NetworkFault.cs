namespace Rhythmo.Mobile.Infrastructure;

/// <summary>Classifie les pannes réseau / timeout pour retry silencieux (pas d'UI).</summary>
public static class NetworkFault
{
	public static bool IsTransient(Exception ex)
	{
		for (var current = ex; current is not null; current = current.InnerException)
		{
			if (current is HttpRequestException or TimeoutException or TaskCanceledException
			    or OperationCanceledException)
				return true;

			var msg = current.Message;
			if (string.IsNullOrWhiteSpace(msg))
				continue;

			if (msg.Contains("timeout", StringComparison.OrdinalIgnoreCase)
			    || msg.Contains("timed out", StringComparison.OrdinalIgnoreCase)
			    || msg.Contains("network", StringComparison.OrdinalIgnoreCase)
			    || msg.Contains("socket", StringComparison.OrdinalIgnoreCase)
			    || msg.Contains("unreachable", StringComparison.OrdinalIgnoreCase)
			    || msg.Contains("connection", StringComparison.OrdinalIgnoreCase)
			    || msg.Contains("name resolution", StringComparison.OrdinalIgnoreCase)
			    || msg.Contains("temporarily unavailable", StringComparison.OrdinalIgnoreCase)
			    || msg.Contains("502", StringComparison.Ordinal)
			    || msg.Contains("503", StringComparison.Ordinal)
			    || msg.Contains("504", StringComparison.Ordinal))
				return true;
		}

		return false;
	}
}
