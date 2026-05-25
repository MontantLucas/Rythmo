using Microsoft.Extensions.DependencyInjection;
using Rhythmo.Mobile.Services;

namespace Rhythmo.Mobile.Infrastructure;

/// <summary>Navigation racine WinUI — Shell et XAML uniquement sur le thread UI.</summary>
internal static class UiNavigation
{
	public static Task ShowLoginAsync() =>
		MainThread.InvokeOnMainThreadAsync(() =>
		{
			SetRoot(new NavigationPage(new LoginPage()));
		});

	public static Task ShowAppShellAsync() =>
		MainThread.InvokeOnMainThreadAsync(() =>
		{
			var shell = ServiceHelper.Services.GetRequiredService<AppShell>();
			SetRoot(shell);
		});

	/// <summary>Catalogue intégré en arrière-plan (ne bloque pas l’UI).</summary>
	public static void RunBootstrapInBackground()
	{
		_ = Task.Run(async () =>
		{
			try
			{
				await ServiceHelper.Services.GetRequiredService<SupabaseBootstrap>().EnsureAsync()
					.ConfigureAwait(false);
			}
			catch
			{
				// Optionnel au démarrage.
			}
		});
	}

	static void SetRoot(Page page)
	{
		if (Application.Current?.Windows.FirstOrDefault() is { } window)
			window.Page = page;
	}
}
