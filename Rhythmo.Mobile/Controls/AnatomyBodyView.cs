using Rhythmo.Mobile.Controls.Anatomy;
using Rhythmo.Mobile.Theme;
using Rhythmo.Shared.Ranking;

namespace Rhythmo.Mobile.Controls;

public sealed class AnatomyBodyView : ContentView
{
	private readonly Dictionary<string, BodyGroupVisual> _groups = [];
	private readonly AnatomyCanvas _front = new(AnatomyViewKind.Front);
	private readonly AnatomyCanvas _back = new(AnatomyViewKind.Back);
	private readonly Image _robot = new()
	{
		Aspect = Aspect.AspectFit,
		HorizontalOptions = LayoutOptions.Fill,
		VerticalOptions = LayoutOptions.Fill,
		InputTransparent = true
	};
	private readonly Grid _stage = new() { HeightRequest = 340 };
	private readonly HorizontalStackLayout _toggle = new()
	{
		Spacing = 8,
		HorizontalOptions = LayoutOptions.Center
	};
	private readonly Button _frontBtn = MakeToggle("Avant");
	private readonly Button _backBtn = MakeToggle("Arrière");
	private readonly Label _viewCaption = MakeCaption("Vue avant");
	private readonly Label _nameLabel = new()
	{
		FontFamily = "OpenSansSemibold",
		FontSize = 20,
		TextColor = RhythmColors.TextPrimary
	};
	private readonly Label _evalLabel = new()
	{
		FontSize = 13,
		TextColor = RhythmColors.TextSecondary
	};
	private readonly ContentView _rankHost = new();
	private readonly Button _openBtn = new()
	{
		Text = "Voir le détail →",
		Style = (Style)Application.Current!.Resources["RhythmBtnGhost"],
		HorizontalOptions = LayoutOptions.Start,
		IsVisible = false
	};
	private readonly VerticalStackLayout _panel;
	private bool _showBack;
	private string? _selectedId;
	private int? _overallRank;
	private string? _robotFile;

	public event EventHandler<string>? GroupOpened;

	public AnatomyBodyView()
	{
		_frontBtn.Clicked += (_, _) => SetSide(false);
		_backBtn.Clicked += (_, _) => SetSide(true);
		_front.GroupPicked += OnGroupPicked;
		_back.GroupPicked += OnGroupPicked;
		_front.GroupHovered += (_, id) => SyncHover(id);
		_back.GroupHovered += (_, id) => SyncHover(id);
		_openBtn.Clicked += (_, _) =>
		{
			if (_selectedId is { } id)
				GroupOpened?.Invoke(this, id);
		};

		_toggle.Children.Add(_frontBtn);
		_toggle.Children.Add(_backBtn);

		_stage.Children.Add(_robot);
		_stage.Children.Add(_front);
		_stage.Children.Add(_back);

		_panel = new VerticalStackLayout
		{
			Spacing = 8,
			Margin = new Thickness(4, 8, 4, 0),
			Children =
			{
				new BoxView
				{
					HeightRequest = 1,
					Color = Color.FromArgb("#243040"),
					Margin = new Thickness(0, 0, 0, 8)
				},
				_nameLabel,
				_rankHost,
				_evalLabel,
				_openBtn
			}
		};
		_panel.GestureRecognizers.Add(new TapGestureRecognizer
		{
			Command = new Command(() =>
			{
				if (_selectedId is { } id)
					GroupOpened?.Invoke(this, id);
			})
		});

		Content = new VerticalStackLayout
		{
			Spacing = 16,
			Children = { _toggle, _stage, _viewCaption, _panel }
		};
		MaximumWidthRequest = 720;
		HorizontalOptions = LayoutOptions.Center;
		ApplySide();
		ShowHint();
	}

	public void SetGroups(IEnumerable<BodyGroupVisual> groups)
	{
		_groups.Clear();
		foreach (var g in groups)
			_groups[g.GroupId] = g;

		var evaluated = _groups.Values
			.Where(g => g.ValidatedRank is not null)
			.Select(g => g.ValidatedRank!.Value)
			.ToList();
		_overallRank = evaluated.Count == 0
			? null
			: Math.Clamp(
				(int)Math.Floor(evaluated.Average()),
				ExerciseRankCalculator.MinRank,
				ExerciseRankCalculator.MaxRank);

		_front.SetGroups(_groups);
		_back.SetGroups(_groups);
		if (_selectedId is not null)
			ShowGroup(_selectedId);
		else
			ShowHint();
		PaintRobot();
	}

	private void OnGroupPicked(object? sender, string? groupId)
	{
		if (groupId is null)
			return;

		if (_selectedId == groupId)
		{
			GroupOpened?.Invoke(this, groupId);
			return;
		}

		_selectedId = groupId;
		_front.SelectedGroupId = groupId;
		_back.SelectedGroupId = groupId;
		ShowGroup(groupId);
	}

	private void SyncHover(string? id)
	{
		_front.HoveredGroupId = id;
		_back.HoveredGroupId = id;
	}

	private void ShowGroup(string groupId)
	{
		if (!_groups.TryGetValue(groupId, out var g))
		{
			ShowHint();
			return;
		}

		_nameLabel.Text = g.NameFr;
		_rankHost.Content = RankBadge.Stacked(g.ValidatedRank);
		_evalLabel.Text = $"{g.EvaluatedCount}/{g.TotalCount} muscles évalués";
		_openBtn.IsVisible = true;
	}

	private void ShowHint()
	{
		_nameLabel.Text = "Rythmo";
		_rankHost.Content = RankBadge.Stacked(_overallRank);
		_evalLabel.Text = _overallRank is null
			? "Touche une zone du robot pour voir le rang du groupe."
			: "Rang global · touche une zone pour un groupe.";
		_openBtn.IsVisible = false;
		PaintRobot();
	}

	private void PaintRobot()
	{
		var file = RankRobot.FileName(_overallRank, _showBack);
		if (_robotFile == file)
			return;
		_robotFile = file;
		_robot.Source = file;
	}

	private void SetSide(bool back)
	{
		_showBack = back;
		ApplySide();
	}

	private void ApplySide()
	{
		_front.IsVisible = !_showBack;
		_back.IsVisible = _showBack;
		_viewCaption.Text = _showBack ? "Vue arrière" : "Vue avant";
		StyleToggle(_frontBtn, !_showBack);
		StyleToggle(_backBtn, _showBack);
		PaintRobot();
	}

	private static Button MakeToggle(string text) => new()
	{
		Text = text,
		FontFamily = "OpenSansSemibold",
		FontSize = 14,
		CornerRadius = 14,
		Padding = new Thickness(18, 10),
		MinimumHeightRequest = 44,
		MinimumWidthRequest = 96
	};

	private static void StyleToggle(Button btn, bool on)
	{
		btn.BackgroundColor = on ? RhythmColors.Accent : RhythmColors.Surface2;
		btn.TextColor = on ? RhythmColors.Bg : RhythmColors.TextPrimary;
	}

	private static Label MakeCaption(string text) => new()
	{
		Text = text,
		Style = (Style)Application.Current!.Resources["RhythmCaption"],
		HorizontalTextAlignment = TextAlignment.Center
	};

	private sealed class AnatomyCanvas : GraphicsView
	{
		private readonly AnatomyBodyDrawable _map;

		public event EventHandler<string?>? GroupPicked;
		public event EventHandler<string?>? GroupHovered;

		public AnatomyCanvas(AnatomyViewKind kind)
		{
			_map = new AnatomyBodyDrawable { Kind = kind };
			Drawable = _map;
			BackgroundColor = Colors.Transparent;
			HorizontalOptions = LayoutOptions.Fill;
			VerticalOptions = LayoutOptions.Fill;
			MinimumHeightRequest = 280;
			StartInteraction += (_, e) =>
			{
				if (e.Touches is not { Length: > 0 })
					return;
				var p = e.Touches[0];
				GroupPicked?.Invoke(this, _map.HitTest(p.X, p.Y, (float)Width, (float)Height));
			};
			StartHoverInteraction += (_, e) => ApplyHover(e);
			MoveHoverInteraction += (_, e) => ApplyHover(e);
			EndHoverInteraction += (_, _) =>
			{
				_map.HoveredGroupId = null;
				Invalidate();
				GroupHovered?.Invoke(this, null);
			};
		}

		public void SetGroups(IReadOnlyDictionary<string, BodyGroupVisual> groups)
		{
			_map.Groups = groups;
			Invalidate();
		}

		public string? SelectedGroupId
		{
			get => _map.SelectedGroupId;
			set
			{
				_map.SelectedGroupId = value;
				Invalidate();
			}
		}

		public string? HoveredGroupId
		{
			get => _map.HoveredGroupId;
			set
			{
				if (_map.HoveredGroupId == value)
					return;
				_map.HoveredGroupId = value;
				Invalidate();
			}
		}

		private void ApplyHover(TouchEventArgs e)
		{
			if (e.Touches is not { Length: > 0 })
				return;
			var p = e.Touches[0];
			var id = _map.HitTest(p.X, p.Y, (float)Width, (float)Height);
			if (_map.HoveredGroupId == id)
				return;
			_map.HoveredGroupId = id;
			Invalidate();
			GroupHovered?.Invoke(this, id);
		}
	}
}
