using Microsoft.Extensions.DependencyInjection;
using Rhythmo.Mobile.Data;
using Rhythmo.Mobile.Infrastructure;
using Rhythmo.Mobile.Services;

namespace Rhythmo.Mobile;

public partial class ProfilesPage : ContentPage
{
	private readonly IDevErrorPresenter _dev =
		ServiceHelper.Services.GetRequiredService<IDevErrorPresenter>();

	private readonly IRhythmoRepository _repo = ServiceHelper.Services.GetRequiredService<IRhythmoRepository>();
	private readonly SupabaseAuthService _auth = ServiceHelper.Services.GetRequiredService<SupabaseAuthService>();

	public ProfilesPage()
	{
		InitializeComponent();
		SexPicker.ItemsSource = new[] { "Homme", "Femme" };
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		await LoadEditorAsync(ServiceHelper.Services.GetRequiredService<ActiveProfileStore>().Get())
			.ConfigureAwait(true);
	}

	private async void OnSaveProfileClicked(object? sender, EventArgs e)
	{
		var profileId = _auth.CurrentUserId
		                ?? ServiceHelper.Services.GetRequiredService<ActiveProfileStore>().Get();
		await SaveEditorAsync(profileId).ConfigureAwait(true);
	}

	private async void OnSignOutClicked(object? sender, EventArgs e)
	{
		await _auth.SignOutAsync().ConfigureAwait(true);
		await UiNavigation.ShowLoginAsync().ConfigureAwait(true);
	}

	private async Task LoadEditorAsync(Guid profileId)
	{
		try
		{
			var effectiveId = _auth.CurrentUserId ?? profileId;
			var row = await _repo.GetProfileAsync(effectiveId).ConfigureAwait(true);
			if (row is null)
			{
				row = new ProfileRow
				{
					Id = effectiveId,
					DisplayName = "Athlète",
					BiologicalSex = BiologicalSex.Male,
					WeightKg = 75
				};
				await _repo.SaveProfileAsync(row).ConfigureAwait(true);
			}
			DisplayNameEntry.Text = row.DisplayName;
			SexPicker.SelectedIndex = row.BiologicalSex == BiologicalSex.Male ? 0 : 1;
			WeightEntry.Text = row.WeightKg.ToString(System.Globalization.CultureInfo.InvariantCulture);
			HeightEntry.Text =
				row.HeightCm?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "";
			AgeEntry.Text = row.AgeYears?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "";
			ProfileNameLabel.Text = string.IsNullOrWhiteSpace(row.DisplayName) ? "Athlète" : row.DisplayName;
			ProfileMetaLabel.Text = BuildMeta(row);
		}
		catch (Exception ex)
		{
			await _dev.TryShowSafeAsync(ex, nameof(LoadEditorAsync)).ConfigureAwait(false);
		}
	}

	private static string BuildMeta(ProfileRow row)
	{
		var parts = new List<string>();
		if (row.WeightKg > 0)
			parts.Add($"{row.WeightKg:0.#} kg");
		if (row.HeightCm is > 0)
			parts.Add($"{row.HeightCm:0} cm");
		if (row.AgeYears is > 0)
			parts.Add($"{row.AgeYears} ans");
		return parts.Count == 0 ? "Biométrie pour l’estimation kcal." : string.Join("  ·  ", parts);
	}

	private async Task SaveEditorAsync(Guid profileId)
	{
		try
		{
			var row = await _repo.GetProfileAsync(profileId).ConfigureAwait(true)
			          ?? new ProfileRow { Id = profileId, DisplayName = "Athlète" };
			row.DisplayName = string.IsNullOrWhiteSpace(DisplayNameEntry.Text)
				? row.DisplayName
				: DisplayNameEntry.Text.Trim();
			row.BiologicalSex =
				SexPicker.SelectedIndex <= 0 ? BiologicalSex.Male : BiologicalSex.Female;
			if (double.TryParse(WeightEntry.Text?.Replace(",", "."),
				    System.Globalization.NumberStyles.Float,
				    System.Globalization.CultureInfo.InvariantCulture, out var w))
				row.WeightKg = w;
			if (string.IsNullOrWhiteSpace(HeightEntry.Text))
				row.HeightCm = null;
			else if (double.TryParse(HeightEntry.Text?.Replace(",", "."),
				         System.Globalization.NumberStyles.Float,
				         System.Globalization.CultureInfo.InvariantCulture, out var h))
				row.HeightCm = h;
			if (string.IsNullOrWhiteSpace(AgeEntry.Text))
				row.AgeYears = null;
			else if (int.TryParse(AgeEntry.Text?.Trim(), out var age) && age is > 0 and < 120)
				row.AgeYears = age;
			row.Id = _auth.CurrentUserId ?? profileId;
			await _repo.SaveProfileAsync(row).ConfigureAwait(true);
			ServiceHelper.Services.GetRequiredService<ActiveProfileStore>().Set(row.Id);
			try
			{
				await ServiceHelper.Services.GetRequiredService<MuscleRankingService>()
					.RefreshAsync(row.Id).ConfigureAwait(true);
			}
			catch (Exception rankEx) when (rankEx is not OperationCanceledException)
			{
				await _dev.TryShowSafeAsync(rankEx, nameof(SaveEditorAsync) + ".Rank").ConfigureAwait(false);
			}
			await LoadEditorAsync(row.Id).ConfigureAwait(true);
			await RhythmSuccessDialog.ShowAsync(this, "Profil enregistré avec succès").ConfigureAwait(true);
		}
		catch (Exception ex)
		{
			await _dev.TryShowSafeAsync(ex, nameof(SaveEditorAsync)).ConfigureAwait(false);
		}
	}
}
