namespace Rhythmo.Mobile.Infrastructure;

public interface IDevErrorPresenter
{
    Task TryShowSafeAsync(Exception ex, string context);
}
