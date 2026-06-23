using System.Net;
using Supabase.Gotrue;
using Rhythmo.Mobile.Configuration;
using SupabaseClient = Supabase.Client;

namespace Rhythmo.Mobile.Services;

public sealed class SupabaseAuthService(SupabaseClient client, ActiveProfileStore profiles, SupabaseSettings settings)
{
	private readonly SemaphoreSlim _sessionGate = new(1, 1);
	private bool _initialized;

	public bool IsSignedIn => client.Auth.CurrentSession is not null;

	public string? CurrentUserEmail =>
		client.Auth.CurrentUser?.Email ?? client.Auth.CurrentSession?.User?.Email;

	public Guid? CurrentUserId =>
		client.Auth.CurrentSession?.User?.Id is { } s && Guid.TryParse(s, out var g) ? g : null;

	public async Task InitializeAsync(CancellationToken ct = default)
	{
		await _sessionGate.WaitAsync(ct).ConfigureAwait(false);
		try
		{
			await EnsureInitializedCoreAsync().ConfigureAwait(false);
			BindProfile();
		}
		finally
		{
			_sessionGate.Release();
		}
	}

	public async Task EnsureSessionFreshAsync(CancellationToken ct = default)
	{
		await _sessionGate.WaitAsync(ct).ConfigureAwait(false);
		try
		{
			await EnsureInitializedCoreAsync().ConfigureAwait(false);
			if (client.Auth.CurrentSession is null)
			{
				profiles.Clear();
				return;
			}

			await client.Auth.RetrieveSessionAsync().ConfigureAwait(false);
			BindProfile();
		}
		finally
		{
			_sessionGate.Release();
		}
	}

	public async Task SendMagicLinkAsync(string email, CancellationToken ct = default)
	{
		var redirect = $"{settings.RedirectScheme}://login-callback/";
		await client.Auth.SignInWithOtp(new SignInWithPasswordlessEmailOptions(email.Trim())
		{
			EmailRedirectTo = redirect
		}).ConfigureAwait(false);
	}

	public async Task<(bool Ok, string? Error)> SignInWithPasswordAsync(string email, string password, CancellationToken ct = default)
	{
		_ = ct;
		try
		{
			var session = await client.Auth.SignInWithPassword(email.Trim(), password)
				.ConfigureAwait(false);
			if (session is null)
				return (false, "E-mail ou mot de passe incorrect.");
			BindProfile();
			return (true, null);
		}
		catch (Exception ex)
		{
			return (false, FormatAuthError(ex));
		}
	}

	public async Task<(bool Ok, string? Error)> TryCompleteMagicLinkFromUriAsync(Uri uri, CancellationToken ct = default)
	{
		_ = ct;
		try
		{
			var session = await client.Auth.GetSessionFromUrl(uri, storeSession: true).ConfigureAwait(false);
			if (session is null)
				return (false, "Lien invalide ou expiré.");
			BindProfile();
			return (true, null);
		}
		catch (Exception ex)
		{
			return (false, FormatAuthError(ex));
		}
	}

	public async Task<(bool Ok, string? Error)> TryCompleteMagicLinkFromUrlTextAsync(
		string rawUrl,
		string? email = null,
		CancellationToken ct = default)
	{
		var s = rawUrl.Trim();
		if (s.Length == 0)
			return (false, "URL vide.");

		if (!Uri.TryCreate(s, UriKind.Absolute, out var uri))
			return (false, "URL invalide.");

		if (IsSupabaseVerifyUrl(uri))
			return await CompleteSupabaseVerifyUrlAsync(uri, email, ct).ConfigureAwait(false);

		if (s.Contains("access_token=", StringComparison.OrdinalIgnoreCase))
		{
			var normalized = NormalizeCallbackUrl(s) ?? uri;
			return await TryCompleteMagicLinkFromUriAsync(normalized, ct).ConfigureAwait(false);
		}

		if (NormalizeCallbackUrl(s) is { } callback)
			return await TryCompleteMagicLinkFromUriAsync(callback, ct).ConfigureAwait(false);

		return (false,
			"URL non reconnue. Colle l’URL du mail (…/auth/v1/verify?token=…) ou rhythmo://login-callback/#access_token=…");
	}

	async Task<(bool Ok, string? Error)> CompleteSupabaseVerifyUrlAsync(Uri uri, string? email, CancellationToken ct)
	{
		var token = GetQueryValue(uri, "token");
		if (string.IsNullOrEmpty(token))
			return (false, "Paramètre token manquant dans l’URL.");

		try
		{
			var session = await client.Auth.VerifyTokenHash(token, Constants.EmailOtpType.MagicLink)
				.ConfigureAwait(false);
			if (session is not null)
			{
				BindProfile();
				return (true, null);
			}
		}
		catch (Exception ex)
		{
			var msg = FormatAuthError(ex);
			if (!string.IsNullOrWhiteSpace(email))
			{
				try
				{
					var session = await client.Auth.VerifyOTP(email.Trim(), token, Constants.EmailOtpType.MagicLink)
						.ConfigureAwait(false);
					if (session is not null)
					{
						BindProfile();
						return (true, null);
					}
				}
				catch (Exception ex2)
				{
					return (false, FormatAuthError(ex2));
				}
			}

			return (false, msg);
		}

		if (!string.IsNullOrWhiteSpace(email))
		{
			try
			{
				var session = await client.Auth.VerifyOTP(email.Trim(), token, Constants.EmailOtpType.MagicLink)
					.ConfigureAwait(false);
				if (session is not null)
				{
					BindProfile();
					return (true, null);
				}
			}
			catch (Exception ex)
			{
				return (false, FormatAuthError(ex));
			}
		}

		try
		{
			using var http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false });
			if (!string.IsNullOrWhiteSpace(settings.AnonKey))
				http.DefaultRequestHeaders.TryAddWithoutValidation("apikey", settings.AnonKey);

			using var response = await http.GetAsync(uri, ct).ConfigureAwait(false);
			if (response.Headers.Location is { } location)
			{
				var target = location.IsAbsoluteUri ? location : new Uri(uri, location);
				return await TryCompleteMagicLinkFromUriAsync(target, ct).ConfigureAwait(false);
			}

			if (response.StatusCode == HttpStatusCode.OK)
			{
				var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
				if (body.Contains("access_token", StringComparison.OrdinalIgnoreCase))
				{
					var fake = new Uri($"{settings.RedirectScheme}://login-callback/#{body.TrimStart('#')}");
					return await TryCompleteMagicLinkFromUriAsync(fake, ct).ConfigureAwait(false);
				}
			}
		}
		catch (Exception ex)
		{
			return (false, FormatAuthError(ex));
		}

		return (false,
			"Token refusé ou expiré. Renseigne ton e-mail au-dessus et réessaie, ou utilise « Connexion mot de passe ».");
	}

	static bool IsSupabaseVerifyUrl(Uri uri) =>
		uri.Host.Contains("supabase.co", StringComparison.OrdinalIgnoreCase)
		&& uri.AbsolutePath.Contains("/verify", StringComparison.OrdinalIgnoreCase);

	static string? GetQueryValue(Uri uri, string key)
	{
		var query = uri.Query.TrimStart('?');
		if (query.Length == 0)
			return null;

		foreach (var part in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
		{
			var eq = part.IndexOf('=');
			if (eq <= 0)
				continue;
			var name = Uri.UnescapeDataString(part[..eq]);
			if (!string.Equals(name, key, StringComparison.OrdinalIgnoreCase))
				continue;
			return Uri.UnescapeDataString(part[(eq + 1)..]);
		}

		return null;
	}

	/// <summary>Accepte rhythmo://…, localhost/#access_token=… ou seulement le fragment #access_token=…</summary>
	public static Uri? NormalizeCallbackUrl(string raw)
	{
		var s = raw.Trim();
		if (s.Length == 0)
			return null;

		const string scheme = "rhythmo";
		if (s.StartsWith('#'))
			s = $"{scheme}://login-callback/{s}";

		if (!s.Contains("://", StringComparison.Ordinal))
		{
			var path = s.StartsWith('/') ? s : "/" + s;
			s = $"{scheme}://login-callback{path}";
		}

		return Uri.TryCreate(s, UriKind.Absolute, out var uri) ? uri : null;
	}

	public static string FormatAuthError(Exception ex)
	{
		if (IsMagicLinkRateLimited(ex))
			return "Plus de lien magique disponible aujourd’hui. Connecte-toi avec ton mot de passe.";

		if (!string.IsNullOrWhiteSpace(ex.Message))
			return ex.Message;
		return ex.GetType().Name + " — voir logs ou réessaie.";
	}

	public static bool IsMagicLinkRateLimited(Exception ex)
	{
		for (var current = ex; current is not null; current = current.InnerException)
		{
			var msg = current.Message;
			if (msg.Contains("429", StringComparison.Ordinal)
			    || msg.Contains("rate limit", StringComparison.OrdinalIgnoreCase)
			    || msg.Contains("too many requests", StringComparison.OrdinalIgnoreCase)
			    || msg.Contains("email rate limit", StringComparison.OrdinalIgnoreCase))
				return true;
		}

		return false;
	}

	public static bool RequiresReauthentication(Exception ex)
	{
		for (var current = ex; current is not null; current = current.InnerException)
		{
			var msg = current.Message;
			if (string.IsNullOrWhiteSpace(msg))
				continue;

			if (msg.Contains("jwt", StringComparison.OrdinalIgnoreCase)
			    || msg.Contains("token", StringComparison.OrdinalIgnoreCase)
			    || msg.Contains("expired", StringComparison.OrdinalIgnoreCase)
			    || msg.Contains("refresh", StringComparison.OrdinalIgnoreCase)
			    || msg.Contains("session expir", StringComparison.OrdinalIgnoreCase)
			    || msg.Contains("invalid grant", StringComparison.OrdinalIgnoreCase)
			    || msg.Contains("401", StringComparison.Ordinal)
			    || msg.Contains("403", StringComparison.Ordinal))
				return true;
		}

		return false;
	}

	public async Task SignOutAsync()
	{
		await client.Auth.SignOut().ConfigureAwait(false);
		profiles.Clear();
	}

	async Task EnsureInitializedCoreAsync()
	{
		if (_initialized)
			return;

		await client.InitializeAsync().ConfigureAwait(false);
		_initialized = true;
	}

	private void BindProfile()
	{
		if (CurrentUserId is { } uid)
			profiles.Set(uid);
	}
}
