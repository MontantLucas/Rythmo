using Microsoft.Extensions.DependencyInjection;
using Rhythmo.Mobile.Diagnostics;
using Rhythmo.Mobile.Infrastructure;
using Rhythmo.Mobile.Services;

namespace Rhythmo.Mobile;

public partial class AppShell : Shell
{
	public AppShell(GlobalExceptionBootstrap boot, IDevErrorPresenter presenter)
	{
		InitializeComponent();

		Routing.RegisterRoute(nameof(SessionEditPage), typeof(SessionEditPage));
		Routing.RegisterRoute(nameof(WorkoutRunnerPage), typeof(WorkoutRunnerPage));
		Routing.RegisterRoute(nameof(HistoryDetailPage), typeof(HistoryDetailPage));
		Routing.RegisterRoute(nameof(ImportSessionsPage), typeof(ImportSessionsPage));
		Routing.RegisterRoute(nameof(PrFeedPage), typeof(PrFeedPage));

		Loaded += (_, _) =>
		{
			try
			{
				boot.Register();
			}
			catch (Exception ex)
			{
				_ = presenter.TryShowSafeAsync(ex, nameof(AppShell));
			}
		};

		Navigated += (_, _) => _ = WorkoutDraftRecovery.TryPromptIfNeededAsync();
		PropertyChanged += (_, e) =>
		{
			if (e.PropertyName == nameof(CurrentItem))
				_ = WorkoutDraftRecovery.TryPromptIfNeededAsync();
		};
	}
}
