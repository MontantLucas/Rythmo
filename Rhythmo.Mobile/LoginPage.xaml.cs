using Rhythmo.Mobile.Configuration;
using Rhythmo.Mobile.Diagnostics;
using Rhythmo.Mobile.Infrastructure;
using Rhythmo.Mobile.Services;

namespace Rhythmo.Mobile;

public partial class LoginPage : ContentPage
{
	private readonly SupabaseAuthService _auth = ServiceHelper.Services.GetRequiredService<SupabaseAuthService>();
	private readonly SupabaseSettings _settings = ServiceHelper.Services.GetRequiredService<SupabaseSettings>();

	public LoginPage() => InitializeComponent();

	internal void SetStatus(string message) => StatusLabel.Text = message;

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		await _auth.InitializeAsync().ConfigureAwait(true);
		if (!_settings.IsConfigured)
			StatusLabel.Text = "Clé Supabase absente : rebuild avec Rhythmo/.env.";
	}

	private void OnTogglePasswordClicked(object? sender, EventArgs e)
	{
		PasswordEntry.IsPassword = !PasswordEntry.IsPassword;
		TogglePasswordBtn.Text = PasswordEntry.IsPassword
			? LoginPageGlyphs.VisibilityOff
			: LoginPageGlyphs.Visibility;
	}

	private async void OnSendLinkClicked(object? sender, EventArgs e)
	{
		if (!await EnsureConfiguredAsync().ConfigureAwait(true))
			return;

		var email = EmailEntry.Text?.Trim();
		if (string.IsNullOrWhiteSpace(email))
		{
			await RhythmAlertDialog.ShowAsync(this, "E-mail", "Saisis ton adresse e-mail.").ConfigureAwait(true);
			return;
		}

		try
		{
			SendLinkBtn.IsEnabled = false;
			StatusLabel.Text = "";
			await _auth.SendMagicLinkAsync(email).ConfigureAwait(true);
			StatusLabel.Text = "Lien envoyé — consulte ta boîte mail.";
			await RhythmAlertDialog.ShowAsync(
				this,
				"Lien magique",
				"Un e-mail vient de t’être envoyé. Ouvre-le sur ton téléphone pour te connecter.").ConfigureAwait(true);
		}
		catch (Exception ex)
		{
			if (SupabaseAuthService.IsMagicLinkRateLimited(ex))
			{
				StatusLabel.Text = "";
				await RhythmAlertDialog.ShowAsync(
					this,
					"Limite atteinte",
					"Plus de lien magique disponible aujourd’hui. Connecte-toi avec ton mot de passe.",
					isError: true).ConfigureAwait(true);
			}
			else
			{
				await ShowErrorAsync(ex).ConfigureAwait(true);
			}
		}
		finally
		{
			SendLinkBtn.IsEnabled = true;
		}
	}

	private async void OnPasswordSignInClicked(object? sender, EventArgs e)
	{
		if (!await EnsureConfiguredAsync().ConfigureAwait(true))
			return;

		var email = EmailEntry.Text?.Trim();
		var password = PasswordEntry.Text;
		if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(password))
		{
			await RhythmAlertDialog.ShowAsync(
				this,
				"Connexion",
				"E-mail et mot de passe requis.").ConfigureAwait(true);
			return;
		}

		PasswordSignInBtn.IsEnabled = false;
		try
		{
			StatusLabel.Text = "";
			var (ok, err) = await _auth.SignInWithPasswordAsync(email, password).ConfigureAwait(true);
			if (!ok)
			{
				await ShowErrorMessageAsync(err ?? "Connexion refusée.").ConfigureAwait(true);
				return;
			}

			await NavigateToAppShellAsync().ConfigureAwait(true);
		}
		catch (Exception ex)
		{
			await ShowErrorAsync(ex).ConfigureAwait(true);
		}
		finally
		{
			PasswordSignInBtn.IsEnabled = true;
		}
	}

	async Task ShowErrorAsync(Exception ex)
	{
		CrashLogWriter.TryAppend("LoginPage", ex);
		var msg = SupabaseAuthService.FormatAuthError(ex);
		if (CrashLogWriter.LastResolvedPath is { } path)
			msg += $"\n\nLog : {path}";
		await ShowErrorMessageAsync(msg).ConfigureAwait(true);
	}

	async Task ShowErrorMessageAsync(string msg)
	{
		SetStatus(msg);
		await RhythmAlertDialog.ShowAsync(this, "Erreur", msg, isError: true).ConfigureAwait(true);
	}

	async Task<bool> EnsureConfiguredAsync()
	{
		if (_settings.IsConfigured)
			return true;

		await RhythmAlertDialog.ShowAsync(
			this,
			"Configuration",
			"Clé Supabase manquante. Rebuild l’APK avec Rhythmo/.env ou définis SUPABASE_ANON_KEY.",
			isError: true).ConfigureAwait(true);
		return false;
	}

	public static async Task NavigateToAppShellAsync()
	{
		await UiNavigation.ShowAppShellAsync().ConfigureAwait(true);
		UiNavigation.RunBootstrapInBackground();
		await WorkoutDraftRecovery.TryPromptIfNeededAsync().ConfigureAwait(true);
	}
}
