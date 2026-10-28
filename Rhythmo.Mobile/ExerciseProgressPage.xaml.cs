using Microsoft.Extensions.DependencyInjection;
using Rhythmo.Mobile.Charting;
using Rhythmo.Mobile.Infrastructure;
using Rhythmo.Mobile.Services;

namespace Rhythmo.Mobile;

[QueryProperty(nameof(ExerciseIdText), "ExerciseId")]
public partial class ExerciseProgressPage : ContentPage
{
	private readonly WeightProgressDrawable _drawable = new();
	private Guid _exerciseId;

	public ExerciseProgressPage()
	{
		InitializeComponent();
		Chart.Drawable = _drawable;
	}

	public string ExerciseIdText
	{
		set => _exerciseId = Guid.TryParse(Uri.UnescapeDataString(value), out var id) ? id : Guid.Empty;
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		_ = LoadAsync();
	}

	private async Task LoadAsync()
	{
		try
		{
			var repo = ServiceHelper.Services.GetRequiredService<IRhythmoRepository>();
			var profileId = ServiceHelper.Services.GetRequiredService<ActiveProfileStore>().Get();
			var catalog = await repo.ListExercisesAsync().ConfigureAwait(true);
			TitleLabel.Text = catalog.FirstOrDefault(e => e.Id == _exerciseId)?.NameFr ?? "Exercice";
			var pts = await repo.ListPerformanceDailyAsync(profileId, _exerciseId).ConfigureAwait(true);
			_drawable.SetSeries(pts.Select(p => (p.PerformanceDate, p.MaxWeightKg)).ToList());
			Chart.Invalidate();
		}
		catch (Exception ex)
		{
			await UiGuard.ReportAsync(this, ex, nameof(LoadAsync)).ConfigureAwait(true);
		}
	}
}
