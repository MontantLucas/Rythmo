using Rhythmo.Mobile.Controls.Anatomy;
using Rhythmo.Mobile.Theme;
using Rhythmo.Shared.Ranking;

namespace Rhythmo.Mobile.Controls;

public sealed class AnatomyBodyView : ContentView
{
	private const double DualBreakpoint = 680;

	private readonly Dictionary<string, BodyGroupVisual> _groups = [];
	private readonly AnatomyCanvas _front = new(AnatomyViewKind.Front);
	private readonly AnatomyCanvas _back = new(AnatomyViewKind.Back);
	private readonly Grid _bodies = new() { ColumnSpacing = 8 };
	private readonly HorizontalStackLayout _toggle = new()
	{
		Spacing = 8,
		HorizontalOptions = LayoutOptions.Center
	};
	private readonly Button _frontBtn = MakeToggle("Avant");
	private readonly Button _backBtn = MakeToggle("Arrière");
	private readonly Label _frontCaption = MakeCaption("Vue avant");
	private readonly Label _backCaption = MakeCaption("Vue arrière");
	private readonly VerticalStackLayout _frontCol = new() { Spacing = 6 };
	private readonly VerticalStackLayout _backCol = new() { Spacing = 6 };
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
	private bool _dual;
	private string? _selectedId;

	public event EventHandler<string>? GroupOpened;

	public AnatomyBodyView()
	{
		_frontBtn.Clicked += (_, _) =>
		{
			_showBack = false;
			ApplyLayout();
		};
		_backBtn.Clicked += (_, _) =>
		{
			_showBack = true;
			ApplyLayout();
		};
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

		_frontCol.Children.Add(_front);
		_frontCol.Children.Add(_frontCaption);
		_backCol.Children.Add(_back);
		_backCol.Children.Add(_backCaption);

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

		var root = new VerticalStackLayout { Spacing = 16 };
		root.Children.Add(_toggle);
		root.Children.Add(_bodies);
		root.Children.Add(_panel);
		Content = root;
		MaximumWidthRequest = 720;
		HorizontalOptions = LayoutOptions.Center;
		SizeChanged += (_, _) => ApplyLayout();
		ApplyLayout();
		ShowHint();
	}

	public void SetGroups(IEnumerable<BodyGroupVisual> groups)
	{
		_groups.Clear();
		foreach (var g in groups)
			_groups[g.GroupId] = g;

		_front.SetGroups(_groups);
		_back.SetGroups(_groups);
		if (_selectedId is not null)
			ShowGroup(_selectedId);
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
		_nameLabel.Text = "Sélectionne un groupe";
		_rankHost.Content = null;
		_evalLabel.Text = "Touche une zone musculaire pour voir son rang.";
		_openBtn.IsVisible = false;
	}

	private void ApplyLayout()
	{
		_dual = Width >= DualBreakpoint;
		_toggle.IsVisible = !_dual;
		_frontCaption.IsVisible = _dual;
		_backCaption.IsVisible = _dual;
		PaintToggle();

		_bodies.Children.Clear();
		_bodies.ColumnDefinitions.Clear();
		_bodies.RowDefinitions.Clear();

		var bodyH = _dual ? 460d : 420d;
		_front.HeightRequest = bodyH;
		_back.HeightRequest = bodyH;

		if (_dual)
		{
			_bodies.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
			_bodies.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
			_front.IsVisible = true;
			_back.IsVisible = true;
			_bodies.Add(_frontCol, 0);
			_bodies.Add(_backCol, 1);
			return;
		}

		_bodies.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
		_front.IsVisible = !_showBack;
		_back.IsVisible = _showBack;
		_bodies.Add(_showBack ? _backCol : _frontCol, 0);
	}

	private void PaintToggle()
	{
		StyleToggle(_frontBtn, !_showBack);
		StyleToggle(_backBtn, _showBack);
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
			HorizontalOptions = LayoutOptions.Fill;
			VerticalOptions = LayoutOptions.Fill;
			MinimumHeightRequest = 320;
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
