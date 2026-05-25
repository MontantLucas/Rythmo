using Microsoft.UI.Xaml;
using Rhythmo.Mobile.Diagnostics;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Rhythmo.Mobile.WinUI;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : MauiWinUIApplication
{
	/// <summary>
	/// Initializes the singleton application object.  This is the first line of authored code
	/// executed, and as such is the logical equivalent of main() or WinMain().
	/// </summary>
	public App()
	{
		this.InitializeComponent();
		UnhandledException += OnWinUiUnhandledException;
	}

	private static void OnWinUiUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
	{
		var ex = e.Exception ?? new InvalidOperationException(string.IsNullOrWhiteSpace(e.Message)
			? "WinUI UnhandledException sans détail."
			: e.Message);
		CrashLogWriter.TryAppend($"{nameof(Application)}.{nameof(UnhandledException)}", ex,
			string.IsNullOrWhiteSpace(e.Message) ? null : e.Message);
	}

	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}

