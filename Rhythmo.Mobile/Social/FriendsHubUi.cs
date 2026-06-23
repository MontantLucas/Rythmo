using Microsoft.Maui.Controls.Shapes;
using Rhythmo.Mobile.Data;
using Rhythmo.Mobile.Theme;

namespace Rhythmo.Mobile.Social;

internal static class FriendsHubUi
{
	private const double SectionGap = 24;
	private const double CardGap = 16;
	private const double ItemGap = 12;

	public static View LeaderboardSection(
		IReadOnlyList<View> periodTabs,
		IReadOnlyList<LeaderboardEntryVm> sessions,
		IReadOnlyList<LeaderboardEntryVm> volume,
		Action<string>? onProfileTap = null)
	{
		var card = SurfaceCard(padding: 16);
		var stack = new VerticalStackLayout { Spacing = CardGap };

		stack.Children.Add(new Label
		{
			Text = "Classement",
			FontFamily = "OpenSansSemibold",
			FontSize = 16,
			TextColor = RhythmColors.TextPrimary
		});
		var tabsWrap = new HorizontalStackLayout { Spacing = 8 };
		foreach (var t in periodTabs)
			tabsWrap.Children.Add(t);
		stack.Children.Add(tabsWrap);

		var columns = new Grid
		{
			ColumnDefinitions = { new(GridLength.Star), new(GridLength.Star) },
			ColumnSpacing = 12
		};
		var sessionsCol = LeaderboardColumn("Nombre de séances", sessions, onProfileTap);
		columns.Children.Add(sessionsCol);
		var volumeCol = LeaderboardColumn("Volume total (T)", volume, onProfileTap);
		Grid.SetColumn(volumeCol, 1);
		columns.Children.Add(volumeCol);
		stack.Children.Add(columns);

		card.Content = stack;
		return card;
	}

	public static View SectionHeader(
		string title,
		string? linkText = null,
		Action? onLink = null,
		Action? onTitleTap = null)
	{
		var grid = new Grid
		{
			ColumnDefinitions = { new(GridLength.Star), new(GridLength.Auto) },
			Margin = new Thickness(0, SectionGap, 0, 8)
		};
		var row = new HorizontalStackLayout { Spacing = 6 };
		if (title.Contains("PR", StringComparison.Ordinal))
			row.Children.Add(GlyphLabel(SocialGlyph.Bolt, 18, RhythmColors.Violet));
		row.Children.Add(SectionTitleInline(title));
		if (onTitleTap is not null)
		{
			var tap = new TapGestureRecognizer();
			tap.Tapped += (_, _) => onTitleTap();
			row.GestureRecognizers.Add(tap);
		}
		grid.Children.Add(row);

		if (linkText is not null)
		{
			var link = new Label
			{
				Text = linkText,
				FontSize = 13,
				TextColor = RhythmColors.Violet,
				VerticalOptions = LayoutOptions.Center
			};
			if (onLink is not null)
			{
				var tap = new TapGestureRecognizer();
				tap.Tapped += (_, _) => onLink();
				link.GestureRecognizers.Add(tap);
			}
			Grid.SetColumn(link, 1);
			grid.Children.Add(link);
		}

		return grid;
	}

	public static View PrCard(PrFeedItemVm pr)
	{
		var border = SurfaceCard(new Thickness(14, 12), margin: new Thickness(0, 0, 0, ItemGap));
		var grid = new Grid
		{
			ColumnDefinitions =
			{
				new(GridLength.Auto),
				new(GridLength.Star),
				new(GridLength.Auto)
			},
			ColumnSpacing = 12
		};

		grid.Children.Add(ProfileGenderAvatar.Create(pr.DisplayName, pr.BiologicalSex, size: 40));

		var body = new VerticalStackLayout { Spacing = 3 };
		body.Children.Add(new Label
		{
			Text = pr.DisplayName,
			FontFamily = "OpenSansSemibold",
			FontSize = 15,
			TextColor = RhythmColors.TextPrimary
		});
		body.Children.Add(new Label
		{
			Text = pr.ExerciseName,
			FontSize = 13,
			TextColor = RhythmColors.TextSecondary
		});
		var prRow = new HorizontalStackLayout { Spacing = 8 };
		prRow.Children.Add(new Label
		{
			Text = PrKindLabel(pr.Kind),
			FontSize = 11,
			FontFamily = "OpenSansSemibold",
			TextColor = RhythmColors.Violet,
			VerticalOptions = LayoutOptions.Center
		});
		prRow.Children.Add(new Label
		{
			Text = pr.PerformanceLine,
			FontFamily = "OpenSansSemibold",
			FontSize = 15,
			TextColor = RhythmColors.TextPrimary,
			VerticalOptions = LayoutOptions.Center
		});
		body.Children.Add(prRow);
		Grid.SetColumn(body, 1);
		grid.Children.Add(body);

		var time = new Label
		{
			Text = pr.RelativeTime,
			FontSize = 12,
			TextColor = RhythmColors.TextSecondary,
			VerticalOptions = LayoutOptions.Start
		};
		Grid.SetColumn(time, 2);
		grid.Children.Add(time);

		border.Content = grid;
		return border;
	}

	public static View ActivityColumns(IReadOnlyList<View> todayRows, IReadOnlyList<View> liveRows)
	{
		var grid = new Grid
		{
			ColumnDefinitions = { new(GridLength.Star), new(GridLength.Star) },
			ColumnSpacing = 12,
			Margin = new Thickness(0, 0, 0, SectionGap)
		};

		grid.Children.Add(ActivityCard("Aujourd'hui", todayRows));
		var liveCard = ActivityCard("En séance", liveRows);
		Grid.SetColumn(liveCard, 1);
		grid.Children.Add(liveCard);

		return grid;
	}

	public static View StreakSummaryCard(StreakSummaryVm summary)
	{
		var card = SurfaceCard(new Thickness(18, 16), margin: new Thickness(0, 0, 0, SectionGap));
		var grid = new Grid
		{
			ColumnDefinitions = { new(GridLength.Star), new(GridLength.Star) },
			ColumnSpacing = 16
		};

		grid.Children.Add(StreakHalf(
			$"{summary.ActiveDays} jour{(summary.ActiveDays > 1 ? "s" : "")} actif{(summary.ActiveDays > 1 ? "s" : "")}",
			summary.ActiveDays,
			7,
			RhythmColors.Warning));
		var weeksHalf = StreakHalf(
			$"{summary.RegularWeeks} semaine{(summary.RegularWeeks > 1 ? "s" : "")} régulière{(summary.RegularWeeks > 1 ? "s" : "")}",
			summary.RegularWeeks,
			4,
			RhythmColors.Accent);
		Grid.SetColumn(weeksHalf, 1);
		grid.Children.Add(weeksHalf);

		card.Content = grid;
		return card;
	}

	public static View BadgesCarousel(IReadOnlyList<BadgeVm> badges)
	{
		var scroll = new ScrollView
		{
			Orientation = ScrollOrientation.Horizontal,
			HorizontalScrollBarVisibility = ScrollBarVisibility.Never,
			Margin = new Thickness(0, 0, 0, SectionGap)
		};
		var row = new HorizontalStackLayout { Spacing = 14, Padding = new Thickness(0, 4) };
		foreach (var b in badges)
			row.Children.Add(BadgeHexagon(b));
		scroll.Content = row;
		return scroll;
	}

	public static Label EmptyHint(string text) => new()
	{
		Text = text,
		FontSize = 13,
		TextColor = RhythmColors.TextSecondary,
		Margin = new Thickness(0, 4, 0, 4)
	};

	public static Button PeriodTab(string text, bool selected, Action onClick, bool compact = false) =>
		TabPill(text, selected, onClick, compact);

	// ——— composants internes ———

	private static View LeaderboardColumn(
		string title,
		IReadOnlyList<LeaderboardEntryVm> entries,
		Action<string>? onProfileTap)
	{
		var inner = SurfaceCard(new Thickness(12, 10), bg: RhythmColors.Surface2);
		var stack = new VerticalStackLayout { Spacing = 10 };
		stack.Children.Add(new Label
		{
			Text = title,
			FontFamily = "OpenSansSemibold",
			FontSize = 13,
			TextColor = RhythmColors.TextSecondary
		});

		if (entries.Count == 0)
			stack.Children.Add(EmptyHint("—"));
		else
			foreach (var e in entries)
				stack.Children.Add(CompactLeaderboardRow(e, onProfileTap));

		inner.Content = stack;
		return inner;
	}

	private static View CompactLeaderboardRow(LeaderboardEntryVm entry, Action<string>? onProfileTap)
	{
		var isTop = entry.Rank <= 3;
		var grid = new Grid
		{
			ColumnDefinitions =
			{
				new(GridLength.Auto),
				new(GridLength.Auto),
				new(GridLength.Star)
			},
			ColumnSpacing = 8
		};

		grid.Children.Add(RankBadge(entry.Rank, isTop));
		var profileLabel = entry.DisplayName + (entry.IsMe ? " · toi" : "");
		var avatar = ProfileGenderAvatar.Create(
			entry.DisplayName,
			entry.BiologicalSex,
			entry.IsMe,
			size: 34,
			onTap: onProfileTap is null ? null : () => onProfileTap(profileLabel));
		Grid.SetColumn(avatar, 1);
		grid.Children.Add(avatar);

		var metric = new Label
		{
			Text = entry.PrimaryMetric,
			FontSize = isTop ? 15 : 13,
			FontFamily = "OpenSansSemibold",
			TextColor = isTop ? RhythmColors.Accent : RhythmColors.TextPrimary,
			LineBreakMode = LineBreakMode.NoWrap,
			MaxLines = 1,
			HorizontalOptions = LayoutOptions.End,
			VerticalOptions = LayoutOptions.Center
		};
		Grid.SetColumn(metric, 2);
		grid.Children.Add(metric);

		if (isTop)
		{
			grid.BackgroundColor = RhythmColors.Accent.WithAlpha(0.06f);
			grid.Padding = new Thickness(4, 2);
		}

		return grid;
	}

	private static View RankBadge(int rank, bool isTop)
	{
		var color = rank switch
		{
			1 => RhythmColors.Gold,
			2 => RhythmColors.Silver,
			3 => RhythmColors.Bronze,
			_ => RhythmColors.Surface2
		};
		var border = new Border
		{
			WidthRequest = 26,
			HeightRequest = 26,
			BackgroundColor = isTop ? color.WithAlpha(0.25f) : RhythmColors.Surface1,
			Stroke = isTop ? color.WithAlpha(0.6f) : RhythmColors.TextSecondary.WithAlpha(0.2f),
			StrokeThickness = 1,
			Content = new Label
			{
				Text = rank.ToString(),
				FontSize = 12,
				FontFamily = "OpenSansSemibold",
				TextColor = isTop ? color : RhythmColors.TextSecondary,
				HorizontalTextAlignment = TextAlignment.Center,
				VerticalTextAlignment = TextAlignment.Center
			}
		};
		border.StrokeShape = new RoundRectangle { CornerRadius = 13 };
		return border;
	}

	private static View TrendGlyph(LeaderboardTrend trend)
	{
		var (glyph, color) = trend switch
		{
			LeaderboardTrend.Up => (SocialGlyph.TrendUp, RhythmColors.Success),
			LeaderboardTrend.Down => (SocialGlyph.TrendDown, RhythmColors.Error),
			_ => (SocialGlyph.TrendFlat, RhythmColors.TextSecondary)
		};
		return GlyphLabel(glyph, 16, color);
	}

	private static View ActivityCard(string title, IReadOnlyList<View> rows)
	{
		var card = SurfaceCard(new Thickness(14, 12));
		var stack = new VerticalStackLayout { Spacing = 10 };
		stack.Children.Add(new Label
		{
			Text = title,
			FontFamily = "OpenSansSemibold",
			FontSize = 16,
			TextColor = RhythmColors.TextPrimary
		});

		if (rows.Count == 0)
			stack.Children.Add(EmptyHint("Rien pour le moment."));
		else
			foreach (var r in rows)
				stack.Children.Add(r);

		card.Content = stack;
		return card;
	}

	public static View TodayRow(TodayActivityVm item)
	{
		var grid = new Grid
		{
			ColumnDefinitions = { new(GridLength.Auto), new(GridLength.Star) },
			ColumnSpacing = 10,
			Padding = new Thickness(0, 4)
		};
		grid.Children.Add(ProfileGenderAvatar.Create(item.DisplayName, item.BiologicalSex, size: 34));
		var stack = new VerticalStackLayout { Spacing = 2 };
		stack.Children.Add(new Label
		{
			Text = item.DisplayName,
			FontFamily = "OpenSansSemibold",
			FontSize = 14,
			TextColor = RhythmColors.TextPrimary
		});
		stack.Children.Add(new Label
		{
			Text = item.WorkoutTitle,
			FontSize = 12,
			TextColor = RhythmColors.TextSecondary
		});
		var meta = new HorizontalStackLayout { Spacing = 6 };
		meta.Children.Add(new Label
		{
			Text = $"{item.DurationMinutes} min",
			FontSize = 12,
			TextColor = RhythmColors.TextSecondary
		});
		meta.Children.Add(new Label
		{
			Text = item.VolumeText,
			FontSize = 12,
			FontFamily = "OpenSansSemibold",
			TextColor = RhythmColors.Accent
		});
		stack.Children.Add(meta);
		Grid.SetColumn(stack, 1);
		grid.Children.Add(stack);
		return grid;
	}

	public static View LiveRow(LiveTrainingVm l)
	{
		var grid = new Grid
		{
			ColumnDefinitions = { new(GridLength.Auto), new(GridLength.Star), new(GridLength.Auto) },
			ColumnSpacing = 8,
			Padding = new Thickness(0, 4)
		};
		grid.Children.Add(ProfileGenderAvatar.Create(l.DisplayName, l.BiologicalSex, size: 34));
		var stack = new VerticalStackLayout { Spacing = 2 };
		stack.Children.Add(new Label
		{
			Text = l.DisplayName,
			FontFamily = "OpenSansSemibold",
			FontSize = 14,
			TextColor = RhythmColors.TextPrimary
		});
		stack.Children.Add(new Label
		{
			Text = l.SessionTitle,
			FontSize = 12,
			TextColor = RhythmColors.TextSecondary
		});
		Grid.SetColumn(stack, 1);
		grid.Children.Add(stack);

		var right = new HorizontalStackLayout { Spacing = 6, VerticalOptions = LayoutOptions.Center };
		right.Children.Add(new Label
		{
			Text = $"{l.ElapsedMinutes}",
			FontFamily = "OpenSansSemibold",
			FontSize = 16,
			TextColor = RhythmColors.Accent
		});
		right.Children.Add(LivePulseDot());
		Grid.SetColumn(right, 2);
		grid.Children.Add(right);
		return grid;
	}

	private static View StreakHalf(string label, int value, int maxDots, Color dotColor)
	{
		var stack = new VerticalStackLayout { Spacing = 10 };
		stack.Children.Add(new Label
		{
			Text = label,
			FontFamily = "OpenSansSemibold",
			FontSize = 14,
			TextColor = RhythmColors.TextPrimary
		});
		var dots = new HorizontalStackLayout { Spacing = 6 };
		for (var i = 0; i < maxDots; i++)
		{
			var filled = i < Math.Min(value, maxDots);
			dots.Children.Add(new BoxView
			{
				WidthRequest = 8,
				HeightRequest = 8,
				CornerRadius = 4,
				Color = filled ? dotColor : RhythmColors.Surface2
			});
		}
		stack.Children.Add(dots);
		return stack;
	}

	private static View BadgeHexagon(BadgeVm b)
	{
		var stack = new VerticalStackLayout { Spacing = 8, WidthRequest = 88, HorizontalOptions = LayoutOptions.Start };
		var hex = new Border
		{
			WidthRequest = 64,
			HeightRequest = 72,
			HorizontalOptions = LayoutOptions.Center,
			BackgroundColor = b.IsLocked ? RhythmColors.Surface2 : RhythmColors.Surface1,
			Stroke = b.RarityColor.WithAlpha(b.IsLocked ? 0.2f : 0.55f),
			StrokeThickness = 1.5,
			Content = new Label
			{
				Text = b.IsLocked ? SocialGlyph.Lock : b.IconGlyph,
				FontFamily = SocialGlyph.Font,
				FontSize = b.IsLocked ? 22 : 26,
				TextColor = b.IsLocked ? RhythmColors.TextSecondary : b.RarityColor,
				HorizontalTextAlignment = TextAlignment.Center,
				VerticalTextAlignment = TextAlignment.Center
			}
		};
		hex.StrokeShape = new RoundRectangle { CornerRadius = 16 };
		stack.Children.Add(hex);
		stack.Children.Add(new Label
		{
			Text = b.Title,
			FontSize = 11,
			TextColor = RhythmColors.TextPrimary,
			HorizontalTextAlignment = TextAlignment.Center,
			LineBreakMode = LineBreakMode.WordWrap,
			MaxLines = 2
		});
		stack.Children.Add(new Label
		{
			Text = b.RarityLabel,
			FontSize = 10,
			TextColor = b.RarityColor,
			HorizontalTextAlignment = TextAlignment.Center
		});
		return stack;
	}

	private static View LivePulseDot()
	{
		var dot = new BoxView
		{
			WidthRequest = 10,
			HeightRequest = 10,
			CornerRadius = 5,
			Color = RhythmColors.Success,
			VerticalOptions = LayoutOptions.Center
		};
		return dot;
	}

	private static Border SurfaceCard(
		Thickness? padding = null,
		Thickness? margin = null,
		Color? bg = null) =>
		new()
		{
			Padding = padding ?? new Thickness(16),
			Margin = margin ?? new Thickness(0),
			BackgroundColor = bg ?? RhythmColors.Surface1,
			StrokeThickness = 0,
			StrokeShape = new RoundRectangle { CornerRadius = 20 }
		};

	private static Label SectionTitleInline(string text) => new()
	{
		Text = text,
		FontFamily = "OpenSansSemibold",
		FontSize = 24,
		TextColor = RhythmColors.TextPrimary
	};

	private static Button TabPill(string text, bool selected, Action onClick, bool compact = false)
	{
		var btn = new Button
		{
			Text = text,
			FontSize = compact ? 11 : 12,
			FontFamily = "OpenSansSemibold",
			Padding = compact ? new Thickness(10, 5) : new Thickness(12, 7),
			CornerRadius = compact ? 12 : 14,
			BackgroundColor = Colors.Transparent,
			TextColor = selected ? RhythmColors.Accent : RhythmColors.TextSecondary,
			BorderColor = selected ? RhythmColors.Accent : RhythmColors.Surface2,
			BorderWidth = 1
		};
		btn.Clicked += (_, _) => onClick();
		return btn;
	}

	private static Label GlyphLabel(string glyph, double size, Color color) => new()
	{
		Text = glyph,
		FontFamily = SocialGlyph.Font,
		FontSize = size,
		TextColor = color,
		VerticalOptions = LayoutOptions.Center
	};

	private static string PrKindLabel(PrKind kind) => kind switch
	{
		PrKind.Reps => "PR REPS",
		PrKind.Volume => "PR VOLUME",
		_ => "PR POIDS"
	};
}
