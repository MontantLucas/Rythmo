using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls.Shapes;
using Rhythmo.Mobile.Data;
using Rhythmo.Mobile.Infrastructure;
using Rhythmo.Mobile.Services;
using Rhythmo.Mobile.Theme;
using Rhythmo.Shared.Contracts;

namespace Rhythmo.Mobile;

[QueryProperty(nameof(WorkoutIdEncoded), "WorkoutId")]
public partial class HistoryDetailPage : ContentPage
{
	private readonly IDevErrorPresenter _dev =
		ServiceHelper.Services.GetRequiredService<IDevErrorPresenter>();

	private Guid _workoutId;
	private bool _reloadStarted;
	private string? _pendingTitle;
	private string? _pendingNotes;
	private WorkoutCompletedRequest? _pendingSnap;
	private Dictionary<Guid, string>? _pendingNames;
	private Dictionary<Guid, string?>? _pendingCategories;

	public HistoryDetailPage()
	{
		InitializeComponent();
		Loaded += (_, _) => ApplyPendingDetailUi();
		HandlerChanged += (_, _) => ApplyPendingDetailUi();
	}

	public string WorkoutIdEncoded
	{
		set =>
			_workoutId = string.IsNullOrWhiteSpace(value)
				? Guid.Empty
				: Guid.Parse(Uri.UnescapeDataString(value));
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();

		if (_workoutId == Guid.Empty)
		{
			_ = ShowMissingIdAndGoBackAsync();
			return;
		}

		if (_reloadStarted)
		{
			ApplyPendingDetailUi();
			return;
		}

		_reloadStarted = true;
		if (IsLoaded)
			_ = ReloadAsync();
		else
			Loaded += OnDeferredReload;
	}

	private void OnDeferredReload(object? sender, EventArgs e)
	{
		Loaded -= OnDeferredReload;
		_ = ReloadAsync();
	}

	private async Task ShowMissingIdAndGoBackAsync()
	{
		await MainThread.InvokeOnMainThreadAsync(async () =>
		{
			await DisplayAlertAsync("Historique", "Identifiant manquant.", "OK").ConfigureAwait(true);
			await UiShellNavigate.GoAsync("..").ConfigureAwait(true);
		}).ConfigureAwait(true);
	}

	private async void OnBackClicked(object? sender, EventArgs e) =>
		await UiShellNavigate.GoAsync("..").ConfigureAwait(true);

	private void ApplyPendingDetailUi()
	{
		if (_pendingTitle is null)
			return;

		var header = HeaderLabel ?? FindByName(nameof(HeaderLabel)) as Label;
		var meta = MetaLabel ?? FindByName(nameof(MetaLabel)) as Label;
		var host = ExerciseDetailHost ?? FindByName(nameof(ExerciseDetailHost)) as VerticalStackLayout;
		if (header is null || meta is null || host is null)
			return;

		header.Text = _pendingTitle;
		meta.Text = _pendingNotes ?? "";
		RenderExerciseDetails(host, _pendingSnap, _pendingNames, _pendingCategories);

		_pendingTitle = null;
		_pendingNotes = null;
		_pendingSnap = null;
		_pendingNames = null;
		_pendingCategories = null;
	}

	private async Task ApplyHistoryDetailUiAsync(
		string title,
		string notes,
		WorkoutCompletedRequest? snap,
		Dictionary<Guid, string> names,
		Dictionary<Guid, string?> categories)
	{
		_pendingTitle = title;
		_pendingNotes = notes;
		_pendingSnap = snap;
		_pendingNames = names;
		_pendingCategories = categories;

		await MainThread.InvokeOnMainThreadAsync(ApplyPendingDetailUi).ConfigureAwait(true);

		for (var attempt = 0; attempt < 60 && _pendingTitle is not null; attempt++)
		{
			await Task.Delay(50).ConfigureAwait(true);
			await MainThread.InvokeOnMainThreadAsync(ApplyPendingDetailUi).ConfigureAwait(true);
		}
	}

	private static void RenderExerciseDetails(
		VerticalStackLayout host,
		WorkoutCompletedRequest? snap,
		IReadOnlyDictionary<Guid, string>? names,
		IReadOnlyDictionary<Guid, string?>? categories)
	{
		host.Children.Clear();

		if (snap?.Exercises is not { Count: > 0 })
		{
			host.Children.Add(new Label
			{
				Text = "Aucun détail de séries enregistré pour cette entrée.",
				FontSize = 14,
				TextColor = RhythmColors.TextSecondary
			});
			return;
		}

		foreach (var ex in snap.Exercises)
		{
			string? name = null;
			if (names is not null)
				names.TryGetValue(ex.ExerciseId, out name);
			var title = string.IsNullOrWhiteSpace(name)
				? $"Exercice {ex.ExerciseId.ToString()[..8]}"
				: name!;

			var card = new Border
			{
				Padding = new Thickness(18),
				BackgroundColor = RhythmColors.Surface1,
				StrokeThickness = 0
			};
			card.StrokeShape = new RoundRectangle { CornerRadius = 16 };

			var stack = new VerticalStackLayout { Spacing = 10 };
			stack.Children.Add(new Label
			{
				Text = title,
				FontFamily = "OpenSansSemibold",
				FontSize = 17,
				TextColor = RhythmColors.TextPrimary,
				LineBreakMode = LineBreakMode.WordWrap
			});

			if (categories is not null &&
			    categories.TryGetValue(ex.ExerciseId, out var cat) &&
			    !string.IsNullOrWhiteSpace(cat))
			{
				stack.Children.Add(new Label
				{
					Text = cat,
					FontSize = 12,
					TextColor = RhythmColors.Accent
				});
			}

			if (ex.Sets is not { Count: > 0 })
			{
				stack.Children.Add(new Label
				{
					Text = "Aucune série enregistrée",
					FontSize = 13,
					TextColor = RhythmColors.TextSecondary
				});
			}
			else
			{
				for (var i = 0; i < ex.Sets.Count; i++)
				{
					var s = ex.Sets[i];
					var num = s.SetNumber > 0 ? s.SetNumber : i + 1;
					var kg = s.WeightKg.ToString("0.#", CultureInfo.InvariantCulture);

					var row = new Grid
					{
						ColumnDefinitions =
						{
							new ColumnDefinition(GridLength.Star),
							new ColumnDefinition(GridLength.Auto)
						},
						ColumnSpacing = 12
					};

					row.Children.Add(new Label
					{
						Text = $"Série {num}",
						FontSize = 14,
						TextColor = RhythmColors.TextSecondary,
						VerticalOptions = LayoutOptions.Center
					});

					var values = new Label
					{
						Text = $"{s.Reps} reps · {kg} kg",
						FontFamily = "OpenSansSemibold",
						FontSize = 15,
						TextColor = RhythmColors.TextPrimary,
						HorizontalOptions = LayoutOptions.End,
						VerticalOptions = LayoutOptions.Center
					};
					Grid.SetColumn(values, 1);
					row.Children.Add(values);

					stack.Children.Add(row);
				}
			}

			card.Content = stack;
			host.Children.Add(card);
		}
	}

	private async Task ReloadAsync()
	{
		try
		{
			var repo = ServiceHelper.Services.GetRequiredService<IRhythmoRepository>();
			var active = ServiceHelper.Services.GetRequiredService<ActiveProfileStore>().Get();

			var row = await repo.GetCompletedWorkoutAsync(_workoutId, active).ConfigureAwait(false);

			if (row is null)
			{
				await MainThread.InvokeOnMainThreadAsync(async () =>
				{
					await DisplayAlertAsync(
							"Historique indisponible",
							"Cette entrée ne correspond plus à un résumé enregistré.",
							"OK")
						.ConfigureAwait(true);
					await UiShellNavigate.GoAsync("..").ConfigureAwait(true);
				}).ConfigureAwait(true);
				return;
			}

			var catalog = await repo.ListExercisesAsync().ConfigureAwait(false);
			var namesById = catalog.ToDictionary(static e => e.Id, static e => e.NameFr ?? "");
			var categoriesById = catalog.ToDictionary(static e => e.Id, static e => e.Category);

			var title = row.SessionTitle ?? "";
			var snap = CompletedWorkoutSnapshot.DeserializeRequestSnapshot(row.PayloadJson);
			var notes = WorkoutHistoryFormatter.BuildListSubtitle(row);

			await ApplyHistoryDetailUiAsync(title, notes, snap, namesById, categoriesById).ConfigureAwait(true);
		}
		catch (Exception ex)
		{
			await _dev.TryShowSafeAsync(ex, nameof(ReloadAsync)).ConfigureAwait(true);
		}
	}

	private async void OnDeleteClicked(object? sender, EventArgs e)
	{
		var ok = await DisplayAlertAsync(
			"Supprimer ?",
			"Cette séance sera retirée de ton historique cloud.",
			"Supprimer",
			"Annuler").ConfigureAwait(true);

		if (!ok)
			return;

		try
		{
			var repo = ServiceHelper.Services.GetRequiredService<IRhythmoRepository>();
			var active = ServiceHelper.Services.GetRequiredService<ActiveProfileStore>().Get();

			var tracked = await repo.GetCompletedWorkoutAsync(_workoutId, active).ConfigureAwait(true);
			if (tracked is null)
			{
				await UiShellNavigate.GoAsync("..").ConfigureAwait(true);
				return;
			}

			await repo.DeleteCompletedWorkoutAsync(_workoutId, active).ConfigureAwait(true);
			await UiShellNavigate.GoAsync("..").ConfigureAwait(true);
		}
		catch (Exception ex)
		{
			await _dev.TryShowSafeAsync(ex, nameof(OnDeleteClicked)).ConfigureAwait(true);
		}
	}
}
