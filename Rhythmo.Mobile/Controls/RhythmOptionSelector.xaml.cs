using System.Collections;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls.Shapes;
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

	private bool _suppressSelectionEvent;
	private Grid? _pageOverlay;
	private Page? _hostPage;
	private CollectionView? _overlayOptionsView;

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

	public RhythmOptionSelector()
	{
		InitializeComponent();
		Unloaded += (_, _) => CloseDropdown();
	}

	private static void OnItemsSourceChanged(BindableObject bindable, object oldValue, object newValue)
	{
		if (bindable is not RhythmOptionSelector selector)
			return;

		void Apply() => selector.RefreshValueLabel();

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

	private void ApplyCompactStyle()
	{
		if (IsCompact)
		{
			TriggerBorder.Padding = new Thickness(12, 8);
			TriggerBorder.MinimumHeightRequest = 36;
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
			ValueLabel.Text = "";
			return;
		}

		ValueLabel.Text = items[ix]?.ToString() ?? "";
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

		var hostContent = hostPage is ContentPage cp ? cp.Content : null;
		if (hostContent is null)
			return;

		hostContent.Measure(hostPage.Width, hostPage.Height);
		TriggerBorder.Measure(hostPage.Width, hostPage.Height);
		var bounds = GetBoundsRelativeTo(TriggerBorder, hostContent);

		var left = bounds.X;
		var width = Math.Max(bounds.Width, 160);
		var pageHeight = hostContent.Height > 0 ? hostContent.Height : hostPage.Height;
		var belowTop = bounds.Y + bounds.Height + 8;
		var spaceBelow = pageHeight - belowTop - 16;
		var spaceAbove = bounds.Y - 16;
		const double preferredHeight = 280;
		var openBelow = spaceBelow >= 140 || spaceBelow >= spaceAbove;
		var maxHeight = Math.Min(preferredHeight, openBelow ? spaceBelow : spaceAbove);
		maxHeight = Math.Max(maxHeight, 120);
		var panelTop = openBelow
			? belowTop
			: Math.Max(8, bounds.Y - maxHeight - 8);
		var listHeight = maxHeight - 8;

		var overlay = new Grid
		{
			InputTransparent = false,
			ZIndex = 100,
			BackgroundColor = Colors.Transparent
		};

		var scrim = new BoxView { Color = Colors.Transparent };
		scrim.GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(CloseDropdown) });

		var panel = new Border
		{
			Padding = new Thickness(6, 4),
			BackgroundColor = RhythmColors.Surface2,
			StrokeThickness = 0,
			HeightRequest = maxHeight,
			WidthRequest = width,
			HorizontalOptions = LayoutOptions.Start,
			VerticalOptions = LayoutOptions.Start,
			Margin = new Thickness(left, panelTop, 0, 0),
			StrokeShape = new RoundRectangle { CornerRadius = 14 }
		};

		var optionsView = new CollectionView
		{
			ItemsSource = ItemsSource,
			SelectionMode = SelectionMode.Single,
			HeightRequest = listHeight,
			VerticalScrollBarVisibility = ScrollBarVisibility.Always
		};
		optionsView.ItemTemplate = CreateOverlayItemTemplate();
		optionsView.SelectionChanged += OnOverlayOptionSelected;

		panel.Content = optionsView;
		overlay.Children.Add(scrim);
		overlay.Children.Add(panel);

		if (!AttachOverlay(hostPage, overlay))
			return;

		_pageOverlay = overlay;
		_hostPage = hostPage;
		_overlayOptionsView = optionsView;
	}

	private DataTemplate CreateOverlayItemTemplate()
	{
		return new DataTemplate(() =>
		{
			var label = new Label
			{
				FontSize = 15,
				TextColor = RhythmColors.TextPrimary,
				LineBreakMode = LineBreakMode.TailTruncation
			};
			label.SetBinding(Label.TextProperty, ".");

			var border = new Border
			{
				Padding = new Thickness(14, 12),
				BackgroundColor = Colors.Transparent,
				StrokeThickness = 0,
				Content = label
			};

			var tap = new TapGestureRecognizer();
			tap.Tapped += (_, _) =>
			{
				if (border.BindingContext is not null)
					SelectOverlayItem(border.BindingContext);
			};
			border.GestureRecognizers.Add(tap);
			return border;
		});
	}

	private void SelectOverlayItem(object selected)
	{
		if (_suppressSelectionEvent)
			return;

		var items = ItemsSource;
		if (items is null)
			return;

		var ix = -1;
		for (var i = 0; i < items.Count; i++)
		{
			if (Equals(items[i], selected))
			{
				ix = i;
				break;
			}
		}

		if (ix < 0)
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

	private void OnOverlayOptionSelected(object? sender, SelectionChangedEventArgs e)
	{
		if (_suppressSelectionEvent || e.CurrentSelection.Count == 0)
			return;

		var selected = e.CurrentSelection[0];
		if (selected is null)
			return;

		SelectOverlayItem(selected);
	}

	public void CloseDropdown()
	{
		if (_overlayOptionsView is not null)
		{
			_overlayOptionsView.SelectionChanged -= OnOverlayOptionSelected;
			_overlayOptionsView = null;
		}

		if (_hostPage is not null && _pageOverlay is not null)
			DetachOverlay(_hostPage, _pageOverlay);

		_pageOverlay = null;
		_hostPage = null;
	}

	private static Rect GetBoundsRelativeTo(VisualElement element, VisualElement root)
	{
		var bounds = element.Bounds;
		ScrollView? scrollView = null;
		var current = element.Parent as VisualElement;
		while (current is not null && current != root)
		{
			if (current is ScrollView sv)
				scrollView = sv;

			bounds = new Rect(
				bounds.X + current.X,
				bounds.Y + current.Y,
				bounds.Width,
				bounds.Height);
			current = current.Parent as VisualElement;
		}

		if (scrollView is not null)
		{
			bounds = new Rect(
				bounds.X - scrollView.ScrollX,
				bounds.Y - scrollView.ScrollY,
				bounds.Width,
				bounds.Height);
		}

		return bounds;
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

	private static bool AttachOverlay(Page page, Grid overlay)
	{
		if (page is not ContentPage cp || cp.Content is not View content)
			return false;

		if (content is Grid host)
		{
			host.Children.Add(overlay);
			return true;
		}

		var wrapper = new Grid();
		wrapper.Children.Add(content);
		wrapper.Children.Add(overlay);
		cp.Content = wrapper;
		return true;
	}

	private static void DetachOverlay(Page page, Grid overlay)
	{
		if (page is not ContentPage cp || cp.Content is not Grid host)
			return;

		host.Children.Remove(overlay);
	}
}
