using Microsoft.Maui.Controls.Shapes;
using Rhythmo.Mobile.Infrastructure;
using Rhythmo.Shared.Ranking;

namespace Rhythmo.Mobile.Theme;

internal static class RankUi
{
	public static Border RankCard(string title, int? rank, string subtitle, Action? onTap = null)
	{
		var color = RankPalette.For(rank);
		var card = new Border
		{
			Padding = new Thickness(16, 14),
			StrokeThickness = 0,
			BackgroundColor = RhythmColors.Surface1,
			StrokeShape = new RoundRectangle { CornerRadius = 16 },
			Content = new VerticalStackLayout
			{
				Spacing = 6,
				Children =
				{
					new Label
					{
						Text = title,
						FontFamily = "OpenSansSemibold",
						FontSize = 16,
						TextColor = RhythmColors.TextPrimary,
						LineBreakMode = LineBreakMode.TailTruncation
					},
					new Label
					{
						Text = RankLabels.Display(rank),
						FontFamily = "OpenSansSemibold",
						FontSize = 20,
						TextColor = color
					},
					new Label
					{
						Text = subtitle,
						FontSize = 13,
						TextColor = RhythmColors.TextSecondary
					}
				}
			}
		};

		if (onTap is not null)
		{
			var tap = new TapGestureRecognizer();
			tap.Tapped += (_, _) => onTap();
			card.GestureRecognizers.Add(tap);
		}

		return card;
	}

	public static Task GoGroup(string groupId) =>
		UiShellNavigate.GoAsync($"{nameof(MuscleGroupPage)}?GroupId={Uri.EscapeDataString(groupId)}");

	public static Task GoMuscle(string muscleId) =>
		UiShellNavigate.GoAsync($"{nameof(MusclePage)}?MuscleId={Uri.EscapeDataString(muscleId)}");

	public static Task GoExercise(Guid exerciseId) =>
		UiShellNavigate.GoAsync(
			$"{nameof(ExerciseRankPage)}?ExerciseId={Uri.EscapeDataString(exerciseId.ToString())}");

	public static Task GoQuest(Guid exerciseId) =>
		UiShellNavigate.GoAsync(
			$"{nameof(RankQuestPage)}?ExerciseId={Uri.EscapeDataString(exerciseId.ToString())}");
}
