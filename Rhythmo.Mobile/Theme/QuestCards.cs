using Microsoft.Maui.Controls.Shapes;
using Rhythmo.Shared.Ranking;

namespace Rhythmo.Mobile.Theme;

internal enum QuestCardState
{
	InProgress,
	New,
	Available,
	Succeeded,
	Failed
}

internal sealed record QuestCardModel(
	Guid ExerciseId,
	string ExerciseName,
	int TargetRank,
	int? ValidatedRank,
	string? Objective,
	QuestCardState State);

internal static class QuestCards
{
	public static Border Build(QuestCardModel model, Action? onStart)
	{
		var rankColor = RankPalette.For(model.TargetRank);
		var isNew = model.State == QuestCardState.New;
		var canStart = model.State is QuestCardState.New or QuestCardState.Available or QuestCardState.InProgress;

		var body = new VerticalStackLayout { Spacing = 12 };
		if (model.State != QuestCardState.Available)
			body.Children.Add(Badge(model.State, rankColor));
		body.Children.Add(RankBadge.Stacked(model.TargetRank));
		body.Children.Add(new Label
		{
			Text = model.ExerciseName.ToUpperInvariant(),
			FontFamily = "OpenSansSemibold",
			FontSize = 16,
			TextColor = RhythmColors.TextPrimary,
			LineBreakMode = LineBreakMode.WordWrap
		});

		if (!string.IsNullOrWhiteSpace(model.Objective) && canStart)
		{
			body.Children.Add(Divider());
			body.Children.Add(new Label
			{
				Text = model.State == QuestCardState.InProgress ? "Temps restant" : "Objectif",
				Style = CaptionStyle(),
			});
			body.Children.Add(new Label
			{
				Text = model.Objective,
				FontFamily = "OpenSansSemibold",
				FontSize = 22,
				TextColor = RhythmColors.TextPrimary,
				LineBreakMode = LineBreakMode.WordWrap
			});
			body.Children.Add(RankBadge.Progress(model.ValidatedRank, model.TargetRank));
		}
		else if (model.State == QuestCardState.Succeeded)
		{
			body.Children.Add(new Label
			{
				Text = $"{RankLabels.Code(model.ValidatedRank)} → {RankLabels.Code(model.TargetRank)}",
				FontSize = 15,
				TextColor = RhythmColors.TextSecondary
			});
			body.Children.Add(new Label
			{
				Text = "+1 rang validé",
				FontFamily = "OpenSansSemibold",
				FontSize = 14,
				TextColor = RhythmColors.Success
			});
		}
		else if (model.State == QuestCardState.Failed)
		{
			body.Children.Add(new Label
			{
				Text = "Tentative échouée",
				FontSize = 14,
				TextColor = RhythmColors.TextSecondary
			});
			body.Children.Add(new Label
			{
				Text = "Nouvelle tentative demain",
				FontSize = 13,
				TextColor = RhythmColors.TextSecondary
			});
		}

		if (canStart && onStart is not null)
		{
			var start = new Button
			{
				Text = model.State == QuestCardState.InProgress ? "Reprendre" : "Commencer",
				MinimumHeightRequest = 46,
				Margin = new Thickness(0, 4, 0, 0)
			};
			start.Clicked += (_, _) => onStart();
			body.Children.Add(start);
		}

		var card = new Border
		{
			Padding = new Thickness(20, 18),
			StrokeThickness = isNew ? 1.5 : 0,
			Stroke = isNew ? rankColor : Colors.Transparent,
			BackgroundColor = RhythmColors.Surface1,
			StrokeShape = new RoundRectangle { CornerRadius = 20 },
			Content = body
		};

		if (canStart && onStart is not null)
			card.GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(onStart) });

		return card;
	}

	private static View Badge(QuestCardState state, Color rankColor) => state switch
	{
		QuestCardState.InProgress => Chip("EN COURS", RhythmColors.Accent),
		QuestCardState.New => Chip("NOUVEAU DÉFI", rankColor),
		QuestCardState.Succeeded => Chip("✓  QUÊTE RÉUSSIE", RhythmColors.Success),
		QuestCardState.Failed => Chip("TENTATIVE", RhythmColors.TextSecondary),
		_ => Chip("QUÊTE", RhythmColors.TextSecondary)
	};

	private static Border Chip(string text, Color color) => new()
	{
		Padding = new Thickness(10, 4),
		StrokeThickness = 0,
		BackgroundColor = color.WithAlpha(0.16f),
		HorizontalOptions = LayoutOptions.Start,
		StrokeShape = new RoundRectangle { CornerRadius = 10 },
		Content = new Label
		{
			Text = text,
			FontFamily = "OpenSansSemibold",
			FontSize = 11,
			TextColor = color
		}
	};

	private static BoxView Divider() => new()
	{
		HeightRequest = 1,
		Color = Color.FromArgb("#2A3140"),
		Margin = new Thickness(0, 4)
	};

	private static Style CaptionStyle() =>
		(Style)Application.Current!.Resources["RhythmCaption"];
}
