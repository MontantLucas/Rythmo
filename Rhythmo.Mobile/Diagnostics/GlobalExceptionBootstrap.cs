using Microsoft.Extensions.Logging;
using Rhythmo.Mobile.Infrastructure;

namespace Rhythmo.Mobile.Diagnostics;

public sealed class GlobalExceptionBootstrap(IDevErrorPresenter presenter, ILogger<GlobalExceptionBootstrap> log)
{
    public void Register()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex)
            {
                CrashLogWriter.TryAppend(nameof(AppDomain.UnhandledException), ex);
                log.LogCritical(ex, "Unhandled domain exception");
                _ = presenter.TryShowSafeAsync(ex, nameof(AppDomain.UnhandledException));
            }
        };

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            CrashLogWriter.TryAppend(nameof(TaskScheduler.UnobservedTaskException), e.Exception);
            log.LogError(e.Exception, "Unobserved task exception");
            _ = presenter.TryShowSafeAsync(e.Exception, nameof(TaskScheduler.UnobservedTaskException));
            e.SetObserved();
        };
    }
}
