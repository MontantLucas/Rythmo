using System.Net.Http;
using System.Net.Sockets;

namespace Rhythmo.Shared.Resilience;

/// <summary>
/// Une seule classification pour toute l'app : coupure, session morte, annulation volontaire, ou autre.
/// Une annulation dont le jeton de l'appelant est annulé n'est pas une coupure.
/// </summary>
public static class FaultClassifier
{
	public static FaultKind Classify(Exception exception, bool callerCanceled = false)
	{
		if (callerCanceled && ContainsCancellation(exception))
			return FaultKind.Canceled;

		if (ContainsTransient(exception))
			return FaultKind.Transient;

		if (ContainsReauth(exception))
			return FaultKind.Reauth;

		return FaultKind.Unexpected;
	}

	private static bool ContainsCancellation(Exception exception)
	{
		for (var current = exception; current is not null; current = current.InnerException)
		{
			if (current is OperationCanceledException)
				return true;
		}

		return false;
	}

	private static bool ContainsTransient(Exception exception)
	{
		for (var current = exception; current is not null; current = current.InnerException)
		{
			if (current is HttpRequestException or TimeoutException or SocketException)
				return true;

			// Coupure système (verrouillage, timeout HttpClient) : le jeton appelant n'est pas annulé.
			if (current is OperationCanceledException)
				return true;

			if (MessageLooksTransient(current.Message))
				return true;
		}

		return false;
	}

	private static bool MessageLooksTransient(string? message)
	{
		if (string.IsNullOrWhiteSpace(message))
			return false;

		return message.Contains("timeout", StringComparison.OrdinalIgnoreCase)
		       || message.Contains("timed out", StringComparison.OrdinalIgnoreCase)
		       || message.Contains("network", StringComparison.OrdinalIgnoreCase)
		       || message.Contains("socket", StringComparison.OrdinalIgnoreCase)
		       || message.Contains("unreachable", StringComparison.OrdinalIgnoreCase)
		       || message.Contains("connection", StringComparison.OrdinalIgnoreCase)
		       || message.Contains("name resolution", StringComparison.OrdinalIgnoreCase)
		       || message.Contains("temporarily unavailable", StringComparison.OrdinalIgnoreCase)
		       || message.Contains("502", StringComparison.Ordinal)
		       || message.Contains("503", StringComparison.Ordinal)
		       || message.Contains("504", StringComparison.Ordinal);
	}

	private static bool ContainsReauth(Exception exception)
	{
		for (var current = exception; current is not null; current = current.InnerException)
		{
			if (MessageLooksReauth(current.Message))
				return true;
		}

		return false;
	}

	private static bool MessageLooksReauth(string? message)
	{
		if (string.IsNullOrWhiteSpace(message))
			return false;

		if (message.Contains("invalid grant", StringComparison.OrdinalIgnoreCase)
		    || message.Contains("jwt expired", StringComparison.OrdinalIgnoreCase)
		    || message.Contains("session expir", StringComparison.OrdinalIgnoreCase)
		    || message.Contains("not authenticated", StringComparison.OrdinalIgnoreCase)
		    || message.Contains("refresh_token_not_found", StringComparison.OrdinalIgnoreCase)
		    || message.Contains("Invalid Refresh Token", StringComparison.OrdinalIgnoreCase)
		    || (message.Contains("JWT", StringComparison.Ordinal)
		        && message.Contains("expired", StringComparison.OrdinalIgnoreCase)))
			return true;

		if (!message.Contains("401", StringComparison.Ordinal) && !message.Contains("403", StringComparison.Ordinal))
			return false;

		return message.Contains("unauthorized", StringComparison.OrdinalIgnoreCase)
		       || message.Contains("forbidden", StringComparison.OrdinalIgnoreCase)
		       || message.Contains("jwt", StringComparison.OrdinalIgnoreCase)
		       || message.Contains("bearer", StringComparison.OrdinalIgnoreCase)
		       || message.Contains("auth", StringComparison.OrdinalIgnoreCase);
	}
}
