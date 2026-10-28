using Microsoft.Maui.Networking;
using Rhythmo.Mobile.Diagnostics;
using Rhythmo.Mobile.Services;
using Rhythmo.Shared.Resilience;

namespace Rhythmo.Mobile.Infrastructure;

/// <summary>
/// Réveille la session au déverrouillage et dès que la radio revient, puis rejoue la page visible.
/// </summary>
public sealed class SessionRecovery
{
	private readonly ConnectionRecovery _core;
	private readonly SupabaseAuthService _auth;
	private readonly AsyncLocal<string?> _context = new();
	private int _attached;
	private int _reauthPrompt;

	public SessionRecovery(
		SupabaseAuthService auth,
		SupabaseBootstrap bootstrap,
		WorkoutFinalizeService finalize)
	{
		_auth = auth;
		_core = new ConnectionRecovery(
			async ct =>
			{
				await auth.EnsureSessionFreshAsync(ct).ConfigureAwait(false);
				if (auth.IsSignedIn)
					await bootstrap.EnsureAsync(ct).ConfigureAwait(false);
			},
			_ =>
			{
				finalize.ResumePending();
				return Task.CompletedTask;
			},
			onFault: (ex, kind) => CrashLogWriter.TryAppend($"{_context.Value ?? "SessionRecovery"}.{kind}", ex),
			shouldRestore: () => auth.IsSignedIn);
	}

	public void Attach(Window window)
	{
		if (Interlocked.Exchange(ref _attached, 1) != 0)
			return;

		window.Resumed += (_, _) => _core.NotifyResumed();
		Connectivity.Current.ConnectivityChanged += (_, e) =>
			_core.SetRadio(HasUsableNetwork(e.NetworkAccess));
		_core.SetRadio(HasUsableNetwork(Connectivity.Current.NetworkAccess));
	}

	/// <summary>
	/// Sur Android, l'accès est souvent Unknown ou ConstrainedInternet alors que les requêtes passent.
	/// Seule l'absence de réseau doit bloquer la reconnexion.
	/// </summary>
	private static bool HasUsableNetwork(NetworkAccess access) =>
		access is not NetworkAccess.None and not NetworkAccess.Local;

	public void Bind(object page, Func<Task> reload)
	{
		_core.Bind(page, async _ =>
		{
			if (MainThread.IsMainThread)
				await reload().ConfigureAwait(false);
			else
				await MainThread.InvokeOnMainThreadAsync(reload).ConfigureAwait(false);
		});
	}

	public void Unbind(object page) => _core.Unbind(page);

	public void FollowUp() => _core.RequestFollowUp();

	public async Task<FaultKind?> RunAsync(
		Page? page,
		Func<Task> action,
		string context,
		CancellationToken cancellationToken = default)
	{
		_context.Value = context;
		var kind = await _core.GuardAsync(async _ =>
		{
			// Les pages touchent l'UI après await : le travail doit démarrer sur le thread UI,
			// sinon ConfigureAwait(true) ne revient jamais sur ce thread et l'écran reste vide.
			if (MainThread.IsMainThread)
				await action().ConfigureAwait(false);
			else
				await MainThread.InvokeOnMainThreadAsync(action).ConfigureAwait(false);
		}, cancellationToken).ConfigureAwait(false);

		if (kind == FaultKind.Reauth && page is not null)
			await PromptReauthAsync(page).ConfigureAwait(false);

		return kind;
	}

	public async Task ReportAsync(
		Page? page,
		Exception exception,
		string context,
		CancellationToken cancellationToken = default)
	{
		var kind = FaultClassifier.Classify(exception, cancellationToken.IsCancellationRequested);
		CrashLogWriter.TryAppend($"{context}.{kind}", exception);

		if (kind == FaultKind.Transient)
		{
			FollowUp();
			return;
		}

		if (kind == FaultKind.Reauth && page is not null)
			await PromptReauthAsync(page).ConfigureAwait(false);
	}

	private async Task PromptReauthAsync(Page page)
	{
		if (Interlocked.CompareExchange(ref _reauthPrompt, 1, 0) != 0)
			return;

		try
		{
			await MainThread.InvokeOnMainThreadAsync(async () =>
			{
				var result = await RhythmReauthDialog.ShowAsync(
					page,
					_auth.CurrentUserEmail,
					"Ta session a expiré. Reconnecte-toi pour continuer.").ConfigureAwait(true);
				if (!result.Confirmed)
					return;

				var (ok, err) = await _auth.SignInWithPasswordAsync(result.Email, result.Password)
					.ConfigureAwait(true);
				if (!ok)
				{
					await RhythmAlertDialog.ShowAsync(
						page,
						"Connexion refusée",
						err ?? "Impossible de se reconnecter.",
						isError: true).ConfigureAwait(true);
					return;
				}

				_core.NotifyResumed();
			}).ConfigureAwait(false);
		}
		finally
		{
			Interlocked.Exchange(ref _reauthPrompt, 0);
		}
	}
}
