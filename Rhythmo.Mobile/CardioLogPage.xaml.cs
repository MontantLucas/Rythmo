using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Rhythmo.Mobile.Data;
using Rhythmo.Mobile.Infrastructure;
using Rhythmo.Mobile.Services;
using Rhythmo.Mobile.Theme;
using Rhythmo.Shared.Cardio;

namespace Rhythmo.Mobile;

public partial class CardioLogPage : ContentPage
{
	public CardioLogPage() => InitializeComponent();

	protected override void OnAppearing()
	{
		base.OnAppearing();
		_ = ReloadAsync();
	}

	private async void OnSaveClicked(object? sender, EventArgs e)
	{
		ErrorLabel.IsVisible = false;
		if (!double.TryParse(DistanceEntry.Text?.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var km) ||
		    !double.TryParse(DurationEntry.Text?.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var minutes))
		{
			ShowError("Indique une distance et une durée.");
			return;
		}
		var meters = km * 1000d;
		var seconds = (int)Math.Round(minutes * 60d, MidpointRounding.AwayFromZero);
		if (!CardioMath.IsPositive(meters, seconds))
		{
			ShowError("La distance et la durée doivent être strictement positives.");
			return;
		}
		try
		{
			var repo = ServiceHelper.Services.GetRequiredService<IRhythmoRepository>();
			var profileId = ServiceHelper.Services.GetRequiredService<ActiveProfileStore>().Get();
			await repo.AddCardioActivityAsync(new CardioActivityRow
			{
				ProfileId = profileId,
				PerformedUtc = DateTime.UtcNow,
				DistanceM = meters,
				DurationS = seconds
			}).ConfigureAwait(true);
			DistanceEntry.Text = "";
			DurationEntry.Text = "";
			await ReloadAsync().ConfigureAwait(true);
		}
		catch (Exception ex)
		{
			await UiGuard.ReportAsync(this, ex, nameof(OnSaveClicked)).ConfigureAwait(true);
		}
	}

	private async Task ReloadAsync()
	{
		try
		{
			var repo = ServiceHelper.Services.GetRequiredService<IRhythmoRepository>();
			var profileId = ServiceHelper.Services.GetRequiredService<ActiveProfileStore>().Get();
			var rows = await repo.ListCardioActivitiesSinceAsync(profileId, DateTime.UtcNow.AddDays(-120)).ConfigureAwait(true);
			Host.Children.Clear();
			if (rows.Count == 0)
			{
				Host.Add(new Label { Text = "Aucune sortie enregistrée.", TextColor = RhythmColors.TextSecondary });
				return;
			}
			foreach (var row in rows)
			{
				var pace = CardioMath.PaceSecondsPerKm(row.DistanceM, row.DurationS);
				var paceText = pace is null ? "" : $" · {TimeSpan.FromSeconds(pace.Value):m\\:ss}/km";
				var grid = new Grid
				{
					ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)],
					Children =
					{
						new Label
						{
							Text = $"{row.DistanceM / 1000d:0.#} km · {row.DurationS / 60d:0} min{paceText}",
							TextColor = RhythmColors.TextPrimary,
							VerticalOptions = LayoutOptions.Center
						}
					}
				};
				var del = new Button
				{
					Text = "Supprimer",
					BackgroundColor = Colors.Transparent,
					TextColor = RhythmColors.TextSecondary
				};
				var id = row.Id;
				del.Clicked += async (_, _) =>
				{
					await repo.DeleteCardioActivityAsync(id, profileId).ConfigureAwait(true);
					await ReloadAsync().ConfigureAwait(true);
				};
				grid.Add(del, 1);
				Host.Add(grid);
			}
		}
		catch (Exception ex)
		{
			await UiGuard.ReportAsync(this, ex, nameof(ReloadAsync)).ConfigureAwait(true);
		}
	}

	private void ShowError(string message)
	{
		ErrorLabel.Text = message;
		ErrorLabel.IsVisible = true;
	}
}
