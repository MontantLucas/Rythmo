using System.Collections;
using Microsoft.Maui.ApplicationModel;

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

	private bool _suppressSelectionEvent;

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

	public RhythmOptionSelector()
	{
		InitializeComponent();
	}

	private static void OnItemsSourceChanged(BindableObject bindable, object oldValue, object newValue)
	{
		if (bindable is not RhythmOptionSelector selector)
			return;

		void Apply()
		{
			selector.OptionsView.ItemsSource = newValue as IList;
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

		DropdownPanel.IsVisible = !DropdownPanel.IsVisible;
		if (DropdownPanel.IsVisible)
			OptionsView.ItemsSource = ItemsSource;
	}

	private void OnOptionSelected(object? sender, SelectionChangedEventArgs e)
	{
		if (_suppressSelectionEvent || e.CurrentSelection.Count == 0)
			return;

		var selected = e.CurrentSelection[0];
		if (selected is null)
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
			DropdownPanel.IsVisible = false;
			OptionsView.SelectedItem = null;
		}
		finally
		{
			_suppressSelectionEvent = false;
		}

		SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
	}

	public void CloseDropdown() => DropdownPanel.IsVisible = false;
}
