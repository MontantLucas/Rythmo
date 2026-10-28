using Rhythmo.Mobile.Infrastructure;

namespace Rhythmo.Mobile;

public partial class MorePage : ContentPage
{
	public MorePage() => InitializeComponent();

	private async void OnProfileTapped(object? sender, TappedEventArgs e) =>
		await UiShellNavigate.GoAsync(nameof(ProfilesPage)).ConfigureAwait(false);

	private async void OnStatsTapped(object? sender, TappedEventArgs e) =>
		await UiShellNavigate.GoAsync(nameof(StatsPage)).ConfigureAwait(false);
}
