using Rhythmo.Mobile.Infrastructure;

namespace Rhythmo.Mobile.Diagnostics;

/// <summary>Les erreurs techniques restent dans le journal. Rien n'est affiché.</summary>
public sealed class DevPopupErrorPresenter : IDevErrorPresenter
{
	public Task TryShowSafeAsync(Exception ex, string context)
	{
		CrashLogWriter.TryAppend($"ErreurUI.{context}", ex);
		return Task.CompletedTask;
	}
}
