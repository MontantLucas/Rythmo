using Rhythmo.Mobile.Theme;

namespace Rhythmo.Mobile.Controls;

public sealed class RhythmSectionTabs : ContentView
{
	public event EventHandler<int>? SelectedIndexChanged;

	private readonly Grid _row = new();
	private readonly List<string> _items = [];
	private int _index;

	public RhythmSectionTabs()
	{
		Content = new VerticalStackLayout
		{
			Spacing = 0,
			Children =
			{
				_row,
				new BoxView
				{
					HeightRequest = 1,
					Color = Color.FromArgb("#243040"),
					Margin = new Thickness(0, 0, 0, 2)
				}
			}
		};
		HeightRequest = 48;
	}

	public IReadOnlyList<string> Items => _items;

	public int SelectedIndex
	{
		get => _index;
		set => Select(value, raise: false);
	}

	public void SetItems(IReadOnlyList<string> items, int selectedIndex = 0)
	{
		_items.Clear();
		_items.AddRange(items);
		Select(selectedIndex, raise: false);
		Rebuild();
	}

	private void Rebuild()
	{
		_row.Children.Clear();
		_row.ColumnDefinitions.Clear();
		for (var i = 0; i < _items.Count; i++)
		{
			_row.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
			var index = i;
			var active = i == _index;
			var label = new Label
			{
				Text = _items[i],
				FontFamily = "OpenSansSemibold",
				FontSize = 15,
				HorizontalTextAlignment = TextAlignment.Center,
				VerticalTextAlignment = TextAlignment.Center,
				TextColor = active ? RhythmColors.TextPrimary : RhythmColors.TextSecondary
			};
			var underline = new BoxView
			{
				HeightRequest = 2,
				Color = active ? RhythmColors.Accent : Colors.Transparent,
				VerticalOptions = LayoutOptions.End,
				Margin = new Thickness(8, 0)
			};
			var cell = new Grid { HeightRequest = 44 };
			cell.Children.Add(label);
			cell.Children.Add(underline);
			cell.GestureRecognizers.Add(new TapGestureRecognizer
			{
				Command = new Command(() => Select(index, raise: true))
			});
			_row.Add(cell, i);
		}
	}

	private void Select(int index, bool raise)
	{
		if (_items.Count == 0)
			return;
		index = Math.Clamp(index, 0, _items.Count - 1);
		var changed = index != _index;
		_index = index;
		Rebuild();
		if (raise && changed)
			SelectedIndexChanged?.Invoke(this, _index);
	}
}
