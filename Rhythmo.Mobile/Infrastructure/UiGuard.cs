using Microsoft.Extensions.DependencyInjection;
using Rhythmo.Mobile.Diagnostics;
using Rhythmo.Shared.Resilience;

namespace Rhythmo.Mobile.Infrastructure;

/// <summary>
/// Empêche un <c>async void</c> de tuer l'app et inscrit la page au rejeu après reconnexion.
/// </summary>
internal static class UiGuard
{
	public static void Watch(Page page, Func<Task> reloadCore)
	{
		page.Appearing += (_, _) =>
		{
			if (TryGet(out var recovery))
				recovery.Bind(page, reloadCore);
		};
		page.Disappearing += (_, _) =>
		{
			if (TryGet(out var recovery))
				recovery.Unbind(page);
		};
	}

	public static Task RunAsync(
		Page? page,
		Func<Task> action,
		string context,
		CancellationToken cancellationToken = default)
	{
		if (!TryGet(out var recovery))
			return SwallowAsync(action, context);

		return recovery.RunAsync(page, action, context, cancellationToken);
	}

	public static Task ReportAsync(
		Page? page,
		Exception exception,
		string context,
		CancellationToken cancellationToken = default)
	{
		if (!TryGet(out var recovery))
		{
			CrashLogWriter.TryAppend(context, exception);
			return Task.CompletedTask;
		}

		return recovery.ReportAsync(page, exception, context, cancellationToken);
	}

	private static async Task SwallowAsync(Func<Task> action, string context)
	{
		try
		{
			await action().ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			CrashLogWriter.TryAppend(context, ex);
		}
	}

	private static bool TryGet(out SessionRecovery recovery)
	{
		try
		{
			recovery = Rhythmo.Mobile.ServiceHelper.Services.GetRequiredService<SessionRecovery>();
			return true;
		}
		catch
		{
			recovery = null!;
			return false;
		}
	}
}
