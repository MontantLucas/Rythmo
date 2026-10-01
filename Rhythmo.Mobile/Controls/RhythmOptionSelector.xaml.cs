using System.Collections;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls.Shapes;
using Rhythmo.Mobile.Infrastructure;
using Rhythmo.Mobile.Theme;

namespace Rhythmo.Mobile.Controls;

public partial class RhythmOptionSelector : ContentView
{
	public static readonly BindableProperty ItemsSourceProperty = BindableProperty.Create(
		nameof(ItemsSource),
		typeof(IList),
		typeof(RhythmOptionSelector),
		propertyChanged: OnItemsSourceChanged);

	public static readonly BindableProperty SelectedIndexProperty = BindableProperty.Create(
		nameof(SelectedIndex),
		typeof(int),
		typeof(RhythmOptionSelector),
		defaultValue: -1,
		defaultBindingMode: BindingMode.TwoWay,
		propertyChanged: OnSelectedIndexChanged);

	public static readonly BindableProperty IsCompactProperty = BindableProperty.Create(
		nameof(IsCompact),
		typeof(bool),
		typeof(RhythmOptionSelector),
		false,
		propertyChanged: OnIsCompactChanged);

	public static readonly BindableProperty TitleProperty = BindableProperty.Create(
		nameof(Title),
		typeof(string),
		typeof(RhythmOptionSelector),
		"Choisir");

	public static readonly BindableProperty PlaceholderProperty = BindableProperty.Create(
		nameof(Placeholder),
		typeof(string),
		typeof(RhythmOptionSelector),
		"Choisir…",
		propertyChanged: OnPlaceholderChanged);

	private bool _suppressSelectionEvent;
	private Grid? _pageOverlay;
	private Page? _hostPage;

	public event EventHandler? SelectedIndexChanged;

	public IList? ItemsSource
	{
		get => (IList?)GetValue(ItemsSourceProperty);
		set => SetValue(ItemsSourceProperty, value);
	}

	public int SelectedIndex
	{
		get => (int)GetValue(SelectedIndexProperty);
		set => SetValue(SelectedIndexProperty, value);
	}

	public bool IsCompact
	{
		get => (bool)GetValue(IsCompactProperty);
		set => SetValue(IsCompactProperty, value);
	}

	public string Title
	{
		get => (string)GetValue(TitleProperty);
		set => SetValue(TitleProperty, value);
	}

	public string Placeholder
	{
		get => (string)GetValue(PlaceholderProperty);
		set => SetValue(PlaceholderProperty, value);
	}

	public object? SelectedItem
	{
		get
		{
			var items = ItemsSource;
			var ix = SelectedIndex;
			if (items is null || ix < 0 || ix >= items.Count)
				return null;
			return items[ix];
		}
	}

	public RhythmOptionSelector()
	{
		InitializeComponent();
		Unloaded += (_, _) => CloseDropdown();
	}

	private static void OnItemsSourceChanged(BindableObject bindable, object oldValue, object newValue)
	{
		if (bindable is not RhythmOptionSelector selector)
			return;

		void Apply()
		{
			if (selector._pageOverlay is not null)
				selector.CloseDropdown();
			selector.RefreshValueLabel();
		}

		if (MainThread.IsMainThread)
			Apply();
		else
			MainThread.BeginInvokeOnMainThread(Apply);
	}

	private static void OnSelectedIndexChanged(BindableObject bindable, object oldValue, object newValue)
	{
		if (bindable is not RhythmOptionSelector selector)
			return;

		void Apply() => selector.RefreshValueLabel();

		if (MainThread.IsMainThread)
			Apply();
		else
			MainThread.BeginInvokeOnMainThread(Apply);
	}

	private static void OnIsCompactChanged(BindableObject bindable, object oldValue, object newValue)
	{
		if (bindable is not RhythmOptionSelector selector)
			return;

		void Apply() => selector.ApplyCompactStyle();

		if (MainThread.IsMainThread)
			Apply();
		else
			MainThread.BeginInvokeOnMainThread(Apply);
	}

	private static void OnPlaceholderChanged(BindableObject bindable, object oldValue, object newValue)
	{
		if (bindable is RhythmOptionSelector selector)
			selector.RefreshValueLabel();
	}

	private void ApplyCompactStyle()
	{
		if (IsCompact)
		{
			TriggerBorder.Padding = new Thickness(12, 8);
			TriggerBorder.MinimumHeightRequest = 40;
			ValueLabel.FontSize = 13;
			return;
		}

		TriggerBorder.Padding = new Thickness(16, 14);
		TriggerBorder.MinimumHeightRequest = 48;
		ValueLabel.FontSize = 15;
	}

	private void RefreshValueLabel()
	{
		var items = ItemsSource;
		var ix = SelectedIndex;
		if (items is null || ix < 0 || ix >= items.Count)
		{
			ValueLabel.Text = string.IsNullOrWhiteSpace(Placeholder) ? "Choisir…" : Placeholder;
			ValueLabel.TextColor = RhythmColors.TextSecondary;
			return;
		}

		ValueLabel.Text = items[ix]?.ToString() ?? "";
		ValueLabel.TextColor = RhythmColors.TextPrimary;
	}

	private void OnTriggerTapped(object? sender, TappedEventArgs e)
	{
		if (ItemsSource is null || ItemsSource.Count == 0)
			return;

		if (_pageOverlay is not null)
		{
			CloseDropdown();
			return;
		}

		OpenDropdownOverlay();
	}

	private void OpenDropdownOverlay()
	{
		var hostPage = FindHostPage(this);
		if (hostPage is null)
			return;

		var overlay = new Grid
		{
			InputTransparent = false,
			ZIndex = 220,
			BackgroundColor = Colors.Transparent
		};

		var scrim = new BoxView { Color = RhythmColors.Overlay };
		scrim.GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(CloseDropdown) });

		var pageHeight = hostPage.Height > 1 ? hostPage.Height : 640;
		var itemCount = ItemsSource?.Count ?? 0;
		var estimated = 86 + itemCount * 54;
		var sheetHeight = Math.Clamp(Math.Min(estimated, pageHeight * 0.58), 200, 480);
		var panel = new Border
		{
			Padding = new Thickness(14, 10, 14, 18),
			BackgroundColor = RhythmColors.Surface2,
			StrokeThickness = 0,
			HorizontalOptions = LayoutOptions.Fill,
			VerticalOptions = LayoutOptions.End,
			Margin = new Thickness(10, 0, 10, 18),
			HeightRequest = sheetHeight,
			StrokeShape = new RoundRectangle { CornerRadius = 22 }
		};

		var handle = new BoxView
		{
			Color = RhythmColors.TextSecondary.WithAlpha(0.4f),
			WidthRequest = 40,
			HeightRequest = 4,
			CornerRadius = 2,
			HorizontalOptions = LayoutOptions.Center,
			Margin = new Thickness(0, 2, 0, 10)
		};

		var header = new Label
		{
			Text = string.IsNullOrWhiteSpace(Title) ? "Choisir" : Title,
			FontFamily = "OpenSansSemibold",
			FontSize = 16,
			TextColor = RhythmColors.TextPrimary,
			Margin = new Thickness(4, 0, 4, 10)
		};

		var listHost = new VerticalStackLayout { Spacing = 4 };
		var items = ItemsSource;
		if (items is not null)
		{
			for (var i = 0; i < items.Count; i++)
			{
				var index = i;
				var selected = index == SelectedIndex;
				listHost.Children.Add(BuildOptionRow(items[i]?.ToString() ?? "", selected, () => SelectIndex(index)));
			}
		}

		var scroller = new ScrollView
		{
			VerticalScrollBarVisibility = ScrollBarVisibility.Default,
			Content = listHost
		};

		var layout = new Grid
		{
			RowDefinitions =
			[
				new RowDefinition(GridLength.Auto),
				new RowDefinition(GridLength.Auto),
				new RowDefinition(GridLength.Star)
			]
		};
		layout.Add(handle, 0, 0);
		layout.Add(header, 0, 1);
		layout.Add(scroller, 0, 2);
		panel.Content = layout;

		overlay.Children.Add(scrim);
		overlay.Children.Add(panel);

		if (!PageOverlay.Attach(hostPage, overlay))
			return;

		_pageOverlay = overlay;
		_hostPage = hostPage;
		ChevronLabel.Text = "▴";
	}

	private static Border BuildOptionRow(string label, bool selected, Action onTap)
	{
		var check = new Label
		{
			Text = selected ? "✓" : "",
			FontFamily = "OpenSansSemibold",
			FontSize = 16,
			TextColor = RhythmColors.Accent,
			WidthRequest = 22,
			VerticalOptions = LayoutOptions.Center
		};

		var text = new Label
		{
			Text = label,
			FontSize = 15,
			FontFamily = selected ? "OpenSansSemibold" : "OpenSansRegular",
			TextColor = RhythmColors.TextPrimary,
			LineBreakMode = LineBreakMode.TailTruncation,
			MaxLines = 1,
			VerticalOptions = LayoutOptions.Center
		};

		var grid = new Grid
		{
			ColumnDefinitions =
			[
				new ColumnDefinition(GridLength.Star),
				new ColumnDefinition(GridLength.Auto)
			],
			ColumnSpacing = 8
		};
		grid.Add(text, 0);
		grid.Add(check, 1);

		var border = new Border
		{
			Padding = new Thickness(14, 13),
			BackgroundColor = selected ? Color.FromArgb("#2622D3C5") : RhythmColors.Surface1,
			StrokeThickness = 0,
			StrokeShape = new RoundRectangle { CornerRadius = 14 },
			Content = grid
		};
		border.GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(onTap) });
		return border;
	}

	private void SelectIndex(int ix)
	{
		if (_suppressSelectionEvent)
			return;

		var items = ItemsSource;
		if (items is null || ix < 0 || ix >= items.Count)
			return;

		_suppressSelectionEvent = true;
		try
		{
			SelectedIndex = ix;
			CloseDropdown();
		}
		finally
		{
			_suppressSelectionEvent = false;
		}

		SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
	}

	public void CloseDropdown()
	{
		if (_hostPage is not null && _pageOverlay is not null)
			PageOverlay.Detach(_hostPage, _pageOverlay);

		_pageOverlay = null;
		_hostPage = null;
		if (ChevronLabel is not null)
			ChevronLabel.Text = "▾";
	}

	private static Page? FindHostPage(Element? start)
	{
		var current = start;
		while (current is not null)
		{
			if (current is Page page)
				return page;

			current = current.Parent;
		}

		return Shell.Current?.CurrentPage;
	}
}
