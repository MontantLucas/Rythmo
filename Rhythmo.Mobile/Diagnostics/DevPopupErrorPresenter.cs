using Microsoft.Maui.ApplicationModel.DataTransfer;
using Rhythmo.Mobile.Infrastructure;

namespace Rhythmo.Mobile.Diagnostics;

/// <summary>Fallback sans CommunityToolkit : fenêtre modale plein écran avec rapport collable.</summary>
public sealed class DevPopupErrorPresenter : IDevErrorPresenter
{
	public async Task TryShowSafeAsync(Exception ex, string context)
	{
		CrashLogWriter.TryAppend($"ErreurUI.{context}", ex);
		try
		{
			await MainThread.InvokeOnMainThreadAsync(async () =>
			{
				var shell = Shell.Current;
				if (shell?.CurrentPage is null)
					return;

				var report = BuildReport(context, ex);
				var editor = new Editor
				{
					Text = report,
					IsReadOnly = true,
					AutoSize = EditorAutoSizeOption.TextChanges,
					FontSize = 13,
					HorizontalOptions = LayoutOptions.Fill,
					VerticalOptions = LayoutOptions.Fill
				};
				var scroll = new ScrollView { Content = editor };
				var grid = new Grid
				{
					RowDefinitions =
					[
						new RowDefinition(GridLength.Star),
						new RowDefinition(GridLength.Auto)
					],
					Padding = new Thickness(12)
				};
				Grid.SetRow(scroll, 0);
				grid.Add(scroll);
				var bar = new HorizontalStackLayout { Spacing = 10, HorizontalOptions = LayoutOptions.End };
#if DEBUG
				var copy = new Button { Text = "Copier tout" };
#else
				var copy = new Button { Text = "Copier tout", IsVisible = false };
#endif
				copy.Clicked += async (_, _) =>
				{
					await Clipboard.Default.SetTextAsync(report).ConfigureAwait(false);
					await shell.CurrentPage.DisplayAlertAsync("Presse-papiers", "Rapport copié.", "OK")
						.ConfigureAwait(false);
				};
				var close = new Button { Text = "Fermer" };
				close.Clicked += async (_, _) =>
				{
					await shell.Navigation.PopModalAsync().ConfigureAwait(false);
				};
				bar.Add(copy);
				bar.Add(close);
				Grid.SetRow(bar, 1);
				grid.Add(bar);

				var modal = new ContentPage
				{
					Title = "Erreur DEV",
					Content = grid
				};

				await shell.Navigation
					.PushModalAsync(new NavigationPage(modal))
					.ConfigureAwait(false);
			}).ConfigureAwait(true);
		}
		catch
		{
			// ignore cascading failure
		}
	}

	private static string BuildReport(string context, Exception ex)
	{
		var utc = DateTime.UtcNow.ToString("O");
		var platform = $"{DeviceInfo.Platform} {DeviceInfo.VersionString}";
		var appId = $"{AppInfo.Name} v{AppInfo.VersionString} build:{AppInfo.BuildString}";
		return
			$"""Contexte: {context}\nUTC: {utc}\nPlateforme: {platform}\nApp: {appId}\n\n{ex}""";
	}
}
