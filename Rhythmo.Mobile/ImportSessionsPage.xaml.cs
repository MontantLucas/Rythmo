using Microsoft.Extensions.DependencyInjection;
using Rhythmo.Mobile.Infrastructure;
using Rhythmo.Mobile.Services;

namespace Rhythmo.Mobile;

public partial class ImportSessionsPage : ContentPage
{
	private sealed record SessionPickVm(Guid Id, string Title, string Meta)
	{
		public override string ToString() => Title;
	}

	private sealed record UserPickVm(Guid Id, string Name)
	{
		public override string ToString() => Name;
	}

	private readonly IRhythmoRepository _repo = ServiceHelper.Services.GetRequiredService<IRhythmoRepository>();

	private List<UserPickVm> _users = [];
	private List<SessionPickVm> _sessions = [];
	private Guid? _selectedSessionId;
	private Guid? _selectedOwnerId;

	public ImportSessionsPage()
	{
		InitializeComponent();
		UiGuard.Watch(this, LoadUsersAsync);
		BackBtn.Clicked += (_, _) => _ = UiShellNavigate.GoAsync("..");
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		_ = UiGuard.RunAsync(this, LoadUsersAsync, nameof(LoadUsersAsync));
	}

	private async Task LoadUsersAsync()
	{
		_users = (await _repo.ListImportableUsersAsync().ConfigureAwait(true))
			.Select(u => new UserPickVm(u.UserId, u.DisplayName))
			.ToList();
		UserPicker.ItemsSource = _users;
		UserPicker.SelectedIndex = -1;
		if (_users.Count == 0)
		{
			SessionsList.ItemsSource = null;
			await DisplayAlertAsync(
				"Importer",
				"Aucun autre utilisateur disponible pour l’import.",
				"OK").ConfigureAwait(true);
		}
	}

	private async void OnUserSelected(object? sender, EventArgs e)
	{
		_selectedSessionId = null;
		ImportBtn.IsEnabled = false;
		if (UserPicker.SelectedItem is not UserPickVm user)
		{
			SessionsList.ItemsSource = null;
			return;
		}

		_selectedOwnerId = user.Id;
		try
		{
			var tpls = await _repo.ListSessionTemplatesByOwnerAsync(user.Id).ConfigureAwait(true);
			_sessions = [];
			foreach (var t in tpls)
			{
				var exCount = await _repo.CountSessionExercisesAsync(t.Id).ConfigureAwait(true);
				_sessions.Add(new SessionPickVm(
					t.Id,
					t.Title,
					$"{exCount} exercice(s) · MAJ {t.UpdatedUtc.ToLocalTime():d}"));
			}

			SessionsList.ItemsSource = _sessions;
			SessionsList.SelectedItem = null;
		}
		catch (Exception ex)
		{
			await UiGuard.ReportAsync(this, ex, nameof(OnUserSelected)).ConfigureAwait(false);
		}
	}

	private void OnSessionSelected(object? sender, SelectionChangedEventArgs e)
	{
		if (SessionsList.SelectedItem is SessionPickVm pick)
		{
			_selectedSessionId = pick.Id;
			ImportBtn.IsEnabled = true;
		}
		else
		{
			_selectedSessionId = null;
			ImportBtn.IsEnabled = false;
		}
	}

	private async void OnImportClicked(object? sender, EventArgs e)
	{
		if (_selectedSessionId is not { } sid || _selectedOwnerId is not { } ownerId)
			return;

		var me = ServiceHelper.Services.GetRequiredService<ActiveProfileStore>().Get();
		if (ownerId == me)
		{
			await DisplayAlertAsync("Importer", "Choisis la séance d’un autre utilisateur.", "OK").ConfigureAwait(true);
			return;
		}

		try
		{
			ImportBtn.IsEnabled = false;
			await _repo.ImportSessionTemplateAsync(sid, me).ConfigureAwait(true);
			await DisplayAlertAsync("Importer", "Séance ajoutée à ton compte.", "OK").ConfigureAwait(true);
			await UiShellNavigate.GoAsync("..").ConfigureAwait(true);
		}
		catch (Exception ex)
		{
			await UiGuard.ReportAsync(this, ex, nameof(OnImportClicked)).ConfigureAwait(true);
		}
		finally
		{
			ImportBtn.IsEnabled = _selectedSessionId.HasValue;
		}
	}
}
