using Microsoft.Extensions.DependencyInjection;
using Rhythmo.Mobile.Diagnostics;
using Rhythmo.Mobile.Infrastructure;
using Rhythmo.Mobile.Services;

namespace Rhythmo.Mobile;

public partial class AppShell : Shell
{
	public AppShell(GlobalExceptionBootstrap boot, IDevErrorPresenter presenter, WorkoutFinalizeService finalize)
	{
		InitializeComponent();
		WorkoutSaveBanner.Bind(this, finalize);
		finalize.ResumePending();

		Routing.RegisterRoute(nameof(SessionEditPage), typeof(SessionEditPage));
		Routing.RegisterRoute(nameof(WorkoutRunnerPage), typeof(WorkoutRunnerPage));
		Routing.RegisterRoute(nameof(HistoryDetailPage), typeof(HistoryDetailPage));
		Routing.RegisterRoute(nameof(ImportSessionsPage), typeof(ImportSessionsPage));
		Routing.RegisterRoute(nameof(PrFeedPage), typeof(PrFeedPage));
		Routing.RegisterRoute(nameof(MuscleGroupPage), typeof(MuscleGroupPage));
		Routing.RegisterRoute(nameof(MusclePage), typeof(MusclePage));
		Routing.RegisterRoute(nameof(ExerciseRankPage), typeof(ExerciseRankPage));
		Routing.RegisterRoute(nameof(RankQuestsPage), typeof(RankQuestsPage));
		Routing.RegisterRoute(nameof(RankQuestPage), typeof(RankQuestPage));
		Routing.RegisterRoute(nameof(StatsPage), typeof(StatsPage));
		Routing.RegisterRoute(nameof(ProfilesPage), typeof(ProfilesPage));

		Loaded += (_, _) =>
		{
			WorkoutSaveBanner.NotifyShellReady();
			try
			{
				boot.Register();
			}
			catch (Exception ex)
			{
				_ = presenter.TryShowSafeAsync(ex, nameof(AppShell));
			}
		};

		Navigated += (_, _) =>
		{
			_ = WorkoutDraftRecovery.TryPromptIfNeededAsync();
			_ = QuestResumeDialog.TryPromptIfNeededAsync();
		};
		PropertyChanged += (_, e) =>
		{
			if (e.PropertyName != nameof(CurrentItem))
				return;
			_ = WorkoutDraftRecovery.TryPromptIfNeededAsync();
			_ = QuestResumeDialog.TryPromptIfNeededAsync();
		};
	}
}
