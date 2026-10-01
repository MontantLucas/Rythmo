using Rhythmo.Mobile.Diagnostics;
using Rhythmo.Mobile.Services;

namespace Rhythmo.Mobile.Infrastructure;

/// <summary>Recharge une page visible quand l'enregistrement en arrière-plan se termine.</summary>
internal static class WorkoutFinalizeRefresh
{
	public static void Bind(Page page, Func<Task> reload)
	{
		var service = ServiceHelper.Services.GetRequiredService<WorkoutFinalizeService>();
		EventHandler<WorkoutFinalizeUpdate> handler = (_, update) =>
		{
			if (update.Phase != WorkoutFinalizePhase.Saved)
				return;

			MainThread.BeginInvokeOnMainThread(() => _ = SafeReload(reload));
		};

		page.Appearing += (_, _) =>
		{
			service.Updated -= handler;
			service.Updated += handler;
		};
		page.Disappearing += (_, _) => service.Updated -= handler;
	}

	private static async Task SafeReload(Func<Task> reload)
	{
		try
		{
			await reload().ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			CrashLogWriter.TryAppend(nameof(WorkoutFinalizeRefresh), ex);
		}
	}
}
