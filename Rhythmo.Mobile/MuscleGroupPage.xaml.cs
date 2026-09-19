using Microsoft.Extensions.DependencyInjection;
using Rhythmo.Mobile.Infrastructure;
using Rhythmo.Mobile.Services;
using Rhythmo.Mobile.Theme;
using Rhythmo.Shared.Ranking;

namespace Rhythmo.Mobile;

[QueryProperty(nameof(GroupIdEncoded), "GroupId")]
public partial class MuscleGroupPage : ContentPage
{
	private string _groupId = "";

	public MuscleGroupPage() => InitializeComponent();

	public string GroupIdEncoded
	{
		set => _groupId = Uri.UnescapeDataString(value ?? "");
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		await ReloadAsync().ConfigureAwait(true);
	}

	private async Task ReloadAsync()
	{
		Root.Children.Clear();
		var group = MuscleIds.Groups.FirstOrDefault(g => g.Id == _groupId);
		if (group.Id is null)
			return;

		Title = group.NameFr;
		var repo = ServiceHelper.Services.GetRequiredService<IRhythmoRepository>();
		var profileId = ServiceHelper.Services.GetRequiredService<ActiveProfileStore>().Get();
		var snaps = (await repo.ListMuscleRankSnapshotsAsync(profileId, MuscleIds.StandardVersionId)
			.ConfigureAwait(true)).ToDictionary(s => s.MuscleId);
		var groupSnap = (await repo.ListGroupRankSnapshotsAsync(profileId, MuscleIds.StandardVersionId)
			.ConfigureAwait(true)).FirstOrDefault(s => s.GroupId == _groupId);

		Root.Children.Add(new Label
		{
			Style = (Style)Application.Current!.Resources["RhythmH1"],
			Text = group.NameFr
		});
		Root.Children.Add(new Label
		{
			Style = (Style)Application.Current!.Resources["RhythmCaption"],
			Text = groupSnap is null
				? "Aucun muscle évalué"
				: $"{RankLabels.Display(groupSnap.ValidatedRank)} · {groupSnap.EvaluatedCount}/{groupSnap.TotalCount} muscles évalués"
		});

		foreach (var muscle in MuscleIds.Muscles.Where(m => m.GroupId == _groupId))
		{
			snaps.TryGetValue(muscle.Id, out var snap);
			var id = muscle.Id;
			Root.Children.Add(RankUi.RankCard(
				muscle.NameFr,
				snap?.ValidatedRank,
				snap is { EvaluatedCount: > 0 }
					? $"Théorique {RankLabels.Code(snap.TheoreticalRank)}"
					: "Non évalué",
				() => _ = RankUi.GoMuscle(id)));
		}
	}
}
