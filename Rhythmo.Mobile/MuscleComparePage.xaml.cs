using Microsoft.Extensions.DependencyInjection;
using Rhythmo.Mobile.Charting;
using Rhythmo.Mobile.Data;
using Rhythmo.Mobile.Infrastructure;
using Rhythmo.Mobile.Services;
using Rhythmo.Mobile.Social;
using Rhythmo.Mobile.Theme;
using Rhythmo.Shared.Ranking;

namespace Rhythmo.Mobile;

public partial class MuscleComparePage : ContentPage
{
	private readonly List<ProfileRow> _others = [];
	private readonly MuscleRadarDrawable _radar = new();
	private string? _openGroup;
	private bool _loading;

	public MuscleComparePage()
	{
		InitializeComponent();
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		_ = ReloadAsync();
	}

	private void OnOtherChanged(object? sender, EventArgs e) => _ = ReloadAsync();

	private async Task ReloadAsync()
	{
		if (_loading)
			return;
		_loading = true;
		try
		{
			var repo = ServiceHelper.Services.GetRequiredService<IRhythmoRepository>();
			var meId = ServiceHelper.Services.GetRequiredService<ActiveProfileStore>().Get();
			if (_others.Count == 0)
			{
				var people = await repo.ListCommunityProfilesAsync().ConfigureAwait(true);
				_others.AddRange(people.Where(p => p.Id != meId));
				OtherPicker.ItemsSource = _others.Select(Display).ToList();
				if (_others.Count > 0 && OtherPicker.SelectedIndex < 0)
					OtherPicker.SelectedIndex = 0;
			}

			var me = await repo.GetProfileAsync(meId).ConfigureAwait(true);
			ProfileRow? other = OtherPicker.SelectedIndex >= 0 && OtherPicker.SelectedIndex < _others.Count
				? _others[OtherPicker.SelectedIndex]
				: null;
			var meSide = await Side(repo, meId).ConfigureAwait(true);
			var otherSide = other is null
				? MuscleCompareModel.Build([], [])
				: await Side(repo, other.Id).ConfigureAwait(true);
			Draw(me, meSide, other, otherSide);
		}
		catch (Exception ex)
		{
			await UiGuard.ReportAsync(this, ex, nameof(ReloadAsync)).ConfigureAwait(true);
		}
		finally
		{
			_loading = false;
		}
	}

	private static async Task<MuscleCompareSide> Side(IRhythmoRepository repo, Guid profileId)
	{
		var groups = await repo.ListGroupRankSnapshotsAsync(profileId, MuscleIds.StandardVersionId).ConfigureAwait(false);
		var muscles = await repo.ListMuscleRankSnapshotsAsync(profileId, MuscleIds.StandardVersionId).ConfigureAwait(false);
		return MuscleCompareModel.Build(
			groups.Select(g => new CompareSnapshot(g.GroupId, g.ValidatedRank)),
			muscles.Select(m => new CompareSnapshot(m.MuscleId, m.ValidatedRank)));
	}

	private void Draw(ProfileRow? me, MuscleCompareSide meSide, ProfileRow? other, MuscleCompareSide otherSide)
	{
		Host.Children.Clear();
		var cards = new Grid { ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star)], ColumnSpacing = 10 };
		cards.Add(ProfileCard(me, meSide, true), 0);
		cards.Add(ProfileCard(other, otherSide, false), 1);
		Host.Add(cards);

		_radar.Self = MuscleIds.Groups.Select(g => meSide.GroupRanks[g.Id]).ToList();
		_radar.Other = MuscleIds.Groups.Select(g => otherSide.GroupRanks[g.Id]).ToList();
		var chart = new GraphicsView { Drawable = _radar, HeightRequest = 280, BackgroundColor = RhythmColors.Bg };
		Host.Add(chart);
		Host.Add(new Label
		{
			Text = "Toi en turquoise · l’autre en violet",
			Style = (Style)Application.Current!.Resources["RhythmCaption"]
		});

		foreach (var group in MuscleIds.Groups)
		{
			var open = _openGroup == group.Id;
			var row = new VerticalStackLayout { Spacing = 8 };
			var line = new Grid
			{
				ColumnDefinitions =
				[
					new ColumnDefinition(new GridLength(28)),
					new ColumnDefinition(GridLength.Star),
					new ColumnDefinition(GridLength.Auto)
				],
				ColumnSpacing = 8
			};
			line.Add(new Image { Source = ExerciseIllustration.SourceForGroup(group.Id), WidthRequest = 28, HeightRequest = 28 }, 0);
			line.Add(new Label
			{
				Text = group.NameFr,
				FontFamily = "OpenSansSemibold",
				TextColor = RhythmColors.TextPrimary,
				VerticalOptions = LayoutOptions.Center
			}, 1);
			line.Add(new Label
			{
				Text = open ? "˅" : "›",
				TextColor = RhythmColors.TextSecondary,
				VerticalOptions = LayoutOptions.Center
			}, 2);
			row.Add(line);
			row.Add(RankLine(meSide.GroupRanks[group.Id], otherSide.GroupRanks[group.Id]));
			if (open)
			{
				foreach (var muscle in MuscleIds.Muscles.Where(m => m.GroupId == group.Id).OrderBy(m => m.Sort))
				{
					row.Add(new Label
					{
						Text = muscle.NameFr,
						FontSize = 13,
						TextColor = RhythmColors.TextSecondary,
						Margin = new Thickness(36, 4, 0, 0)
					});
					row.Add(RankLine(meSide.MuscleRanks[muscle.Id], otherSide.MuscleRanks[muscle.Id]));
				}
			}
			var card = new Border
			{
				Padding = 14,
				BackgroundColor = RhythmColors.Surface1,
				StrokeThickness = 0,
				StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 16 },
				Content = row
			};
			var id = group.Id;
			var tap = new TapGestureRecognizer();
			tap.Tapped += (_, _) =>
			{
				_openGroup = _openGroup == id ? null : id;
				Draw(me, meSide, other, otherSide);
			};
			card.GestureRecognizers.Add(tap);
			Host.Add(card);
		}
	}

	private static View ProfileCard(ProfileRow? profile, MuscleCompareSide side, bool self)
	{
		var name = self
			? string.IsNullOrWhiteSpace(profile?.DisplayName) ? "Moi" : profile!.DisplayName
			: string.IsNullOrWhiteSpace(profile?.DisplayName) ? "Utilisateur" : profile!.DisplayName;
		var sex = profile?.BiologicalSex ?? BiologicalSex.Male;
		var stack = new VerticalStackLayout
		{
			Spacing = 6,
			Children =
			{
				ProfileGenderAvatar.Create(name, sex, highlightMe: self, size: 44),
				new Label { Text = name, FontFamily = "OpenSansSemibold", TextColor = RhythmColors.TextPrimary },
				new Label
				{
					Text = side.EvaluatedLabel,
					FontSize = 12,
					TextColor = RhythmColors.TextSecondary
				}
			}
		};
		return new Border
		{
			Padding = 12,
			BackgroundColor = RhythmColors.Surface1,
			Stroke = self ? RhythmColors.Accent : RhythmColors.Violet,
			StrokeThickness = 1,
			StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 16 },
			Content = stack
		};
	}

	private static View RankLine(int? self, int? other)
	{
		var grid = new Grid
		{
			ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star)],
			ColumnSpacing = 8
		};
		grid.Add(Bar(self, RhythmColors.Accent), 0);
		grid.Add(Bar(other, RhythmColors.Violet), 1);
		return grid;
	}

	private static View Bar(int? rank, Color color)
	{
		var track = new Grid { HeightRequest = 8 };
		track.Add(new BoxView { Color = RhythmColors.Surface2, HeightRequest = 8 });
		if (rank is >= 1 and <= 10)
		{
			track.Add(new BoxView
			{
				Color = color,
				WidthRequest = 120d * rank.Value / 10d,
				HeightRequest = 8,
				HorizontalOptions = LayoutOptions.Start
			});
		}
		return new VerticalStackLayout
		{
			Spacing = 2,
			Children =
			{
				new Label
				{
					Text = rank is null ? "Non évalué" : RankLabels.Display(rank),
					FontSize = 11,
					TextColor = RhythmColors.TextSecondary
				},
				track
			}
		};
	}

	private static string Display(ProfileRow row) =>
		string.IsNullOrWhiteSpace(row.DisplayName) ? "Utilisateur" : row.DisplayName;
}
