using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;

namespace Rhythmo.Mobile.Infrastructure;

internal static class UiShellNavigate
{
	public static Task GoAsync(string route) =>
		MainThread.InvokeOnMainThreadAsync(async () => await Shell.Current.GoToAsync(route));
}
