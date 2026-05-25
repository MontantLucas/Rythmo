namespace Rhythmo.Mobile.Configuration;

/// <summary>
/// Configuration Supabase (clé publishable <c>sb_publishable_…</c> ou legacy anon — jamais secret/service_role ni mdp DB).
/// Renseigne <see cref="AnonKey"/> via variables d'environnement ou user-secrets en dev.
/// </summary>
public sealed class SupabaseSettings
{
	public const string DefaultUrl = "https://lvmqvylradoimaeckwpj.supabase.co";

	public string Url { get; init; } = DefaultUrl;

	public string AnonKey { get; init; } = "";

	public string RedirectScheme { get; init; } = "rhythmo";

	public static SupabaseSettings Load()
	{
		EnvFile.TryLoadFromRepoRoot();

		var url = Environment.GetEnvironmentVariable("SUPABASE_URL");
		var key = Environment.GetEnvironmentVariable("SUPABASE_ANON_KEY");
		return new SupabaseSettings
		{
			Url = string.IsNullOrWhiteSpace(url) ? DefaultUrl : url.Trim(),
			AnonKey = key?.Trim() ?? ""
		};
	}

	public bool IsConfigured => !string.IsNullOrWhiteSpace(AnonKey);
}
