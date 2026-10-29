using Microsoft.Extensions.DependencyInjection;
using Rhythmo.Mobile.Infrastructure;
using Rhythmo.Mobile.Services;
using Rhythmo.Mobile.Theme;
using Rhythmo.Shared.Challenges;

namespace Rhythmo.Mobile;

public partial class WeeklyChallengesPage : ContentPage
{
	public WeeklyChallengesPage()
	{
		InitializeComponent();
		Refresh.Command = new Command(() => _ = LoadAsync());
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
			var service = ServiceHelper.Services.GetRequiredService<WeeklyChallengeService>();
			var board = await service.LoadAsync(repo, profileId).ConfigureAwait(true);
			ScoreLabel.Text = $"{board.Score} pts";
			Host.Children.Clear();
			foreach (var card in board.Cards)
				Host.Add(Build(card, service, repo, profileId));
		}
		catch (Exception ex)
		{
			await UiGuard.ReportAsync(this, ex, nameof(LoadAsync)).ConfigureAwait(true);
		}
	}

	private View Build(ChallengeCardModel card, WeeklyChallengeService service, IRhythmoRepository repo, Guid profileId)
	{
		var stack = new VerticalStackLayout { Spacing = 6 };
		stack.Add(new Label
		{
			Text = card.Title,
			FontFamily = "OpenSansSemibold",
			FontSize = 17,
			TextColor = RhythmColors.TextPrimary
		});
		stack.Add(new Label { Text = card.Detail, TextColor = RhythmColors.TextSecondary, FontSize = 13 });
		if (card.DistanceLabel is not null)
			stack.Add(new Label { Text = card.DistanceLabel, TextColor = RhythmColors.Accent, FontFamily = "OpenSansSemibold" });
		stack.Add(new Label
		{
			Text = StatusText(card),
			FontSize = 13,
			TextColor = card.Status == ChallengeStatus.Succeeded ? RhythmColors.Accent : RhythmColors.TextSecondary
		});
		if (card.CanStart)
			stack.Add(Action("Lancer", async () =>
			{
				await service.StartAsync(repo, profileId, card.AssignmentId).ConfigureAwait(true);
				await LoadAsync().ConfigureAwait(true);
			}));
		if (card.CanAbandon)
			stack.Add(Action("Abandonner", async () =>
			{
				await service.AbandonAsync(repo, profileId, card.AssignmentId).ConfigureAwait(true);
				await LoadAsync().ConfigureAwait(true);
			}));
		if (card.Family is ChallengeFamily.RunningDistance or ChallengeFamily.CardioPace)
			stack.Add(Action("Enregistrer une sortie", async () =>
				await UiShellNavigate.GoAsync(nameof(CardioLogPage)).ConfigureAwait(true)));
		return new Border
		{
			Padding = 14,
			BackgroundColor = RhythmColors.Surface1,
			StrokeThickness = 0,
			StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 16 },
			Content = stack
		};
	}

	private static string StatusText(ChallengeCardModel card) => card.Status switch
	{
		ChallengeStatus.Active => "En cours",
		ChallengeStatus.Succeeded => $"{card.Points} points",
		ChallengeStatus.Expired => "Expiré",
		_ => card.CanStart ? "Prêt" : "Pas assez d’historique"
	};

	private static Button Action(string text, Func<Task> run)
	{
		var btn = new Button
		{
			Text = text,
			HorizontalOptions = LayoutOptions.Start,
			BackgroundColor = RhythmColors.Surface2,
			TextColor = RhythmColors.Accent,
			CornerRadius = 14,
			Padding = new Thickness(12, 6)
		};
		btn.Clicked += async (_, _) =>
		{
			btn.IsEnabled = false;
			try
			{
				await run().ConfigureAwait(true);
			}
			finally
			{
				btn.IsEnabled = true;
			}
		};
		return btn;
	}
}
