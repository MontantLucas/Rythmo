using Rhythmo.Mobile.Diagnostics;
using Rhythmo.Mobile.Infrastructure;
using Rhythmo.Mobile.Services;

namespace Rhythmo.Mobile;

public partial class App : Application
{
	public App()
	{
		Configuration.EnvFile.TryLoadFromRepoRoot();
		InitializeComponent();
		UserAppTheme = AppTheme.Dark;

		try
		{
			ServiceHelper.Services.GetService<GlobalExceptionBootstrap>()?.Register();
		}
		catch
		{
			// ServiceHelper pas encore prêt.
		}
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		// Évite l’écran « Chargement… » bloqué : login visible tout de suite.
		var window = new Window(new NavigationPage(new LoginPage()));
		window.Resumed += (_, _) => _ = RefreshSessionAfterResumeAsync();
		_ = BootAsync();
		return window;
	}

	static async Task BootAsync()
	{
		try
		{
			var auth = ServiceHelper.Services.GetRequiredService<SupabaseAuthService>();
			using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
			await auth.InitializeAsync().WaitAsync(timeout.Token).ConfigureAwait(false);

			if (!await TryRestoreSignedInUserAsync(timeout.Token).ConfigureAwait(false))
				return;

			await UiNavigation.ShowAppShellAsync().ConfigureAwait(false);
			UiNavigation.RunBootstrapInBackground();
		}
		catch (Exception ex)
		{
			CrashLogWriter.TryAppend(nameof(BootAsync), ex);
			try
			{
				await ServiceHelper.Services.GetRequiredService<SupabaseAuthService>().SignOutAsync()
					.ConfigureAwait(false);
			}
			catch
			{
				// ignore
			}

			await UiNavigation.ShowLoginAsync().ConfigureAwait(false);
		}
	}

	static async Task<bool> TryRestoreSignedInUserAsync(CancellationToken ct)
	{
		var auth = ServiceHelper.Services.GetRequiredService<SupabaseAuthService>();
		if (!auth.IsSignedIn || auth.CurrentUserId is not { } uid)
			return false;

		var repo = ServiceHelper.Services.GetRequiredService<IRhythmoRepository>();
		try
		{
			var profile = await repo.GetProfileAsync(uid, ct).ConfigureAwait(false);
			if (profile is not null)
				return true;
		}
		catch
		{
			// Session ou profil invalide.
		}

		await auth.SignOutAsync().ConfigureAwait(false);
		return false;
	}

	static async Task RefreshSessionAfterResumeAsync()
	{
		try
		{
			var auth = ServiceHelper.Services.GetRequiredService<SupabaseAuthService>();
			using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(12));
			await auth.EnsureSessionFreshAsync(timeout.Token).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			CrashLogWriter.TryAppend(nameof(RefreshSessionAfterResumeAsync), ex);
		}
	}

	protected override async void OnAppLinkRequestReceived(Uri uri) =>
		await CompleteMagicLinkAsync(uri).ConfigureAwait(false);

	internal void DispatchAppLink(Uri uri) => _ = CompleteMagicLinkAsync(uri);

	async Task CompleteMagicLinkAsync(Uri uri)
	{
		try
		{
			var auth = ServiceHelper.Services.GetRequiredService<SupabaseAuthService>();
			var (ok, err) = await auth.TryCompleteMagicLinkFromUriAsync(uri).ConfigureAwait(false);
			if (!ok)
			{
				await MainThread.InvokeOnMainThreadAsync(() =>
				{
					if (Application.Current?.Windows.FirstOrDefault()?.Page is NavigationPage
					    { CurrentPage: LoginPage login })
						login.SetStatus(err ?? "Impossible d’ouvrir le lien magique.");
				});
				return;
			}

			await LoginPage.NavigateToAppShellAsync().ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			CrashLogWriter.TryAppend(nameof(CompleteMagicLinkAsync), ex);
		}
	}
}
