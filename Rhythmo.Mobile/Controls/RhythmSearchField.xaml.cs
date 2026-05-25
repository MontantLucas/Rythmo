namespace Rhythmo.Mobile.Controls;

public partial class RhythmSearchField : ContentView
{
	public static readonly BindableProperty TextProperty = BindableProperty.Create(
		nameof(Text),
		typeof(string),
		typeof(RhythmSearchField),
		default(string),
		BindingMode.TwoWay);

	public static readonly BindableProperty PlaceholderProperty = BindableProperty.Create(
		nameof(Placeholder),
		typeof(string),
		typeof(RhythmSearchField),
		"Rechercher…");

	public RhythmSearchField()
	{
		InitializeComponent();
		SearchEntry.TextChanged += (_, e) => TextChanged?.Invoke(this, e);
	}

	public string? Text
	{
		get => (string?)GetValue(TextProperty);
		set => SetValue(TextProperty, value);
	}

	public string Placeholder
	{
		get => (string)GetValue(PlaceholderProperty);
		set => SetValue(PlaceholderProperty, value);
	}

	public event EventHandler<TextChangedEventArgs>? TextChanged;
}
