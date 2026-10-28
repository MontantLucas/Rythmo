using Rhythmo.Shared.History;

namespace Rhythmo.Mobile.Services;

public sealed class TrainingPeriodStore
{
	private readonly Dictionary<TrainingPeriodKind, DateOnly> _anchors = [];

	public static readonly TrainingPeriodKind[] Offered =
	[
		TrainingPeriodKind.Rolling7Days,
		TrainingPeriodKind.CalendarMonth,
		TrainingPeriodKind.CalendarYear
	];

	public TrainingPeriodKind Kind { get; private set; } = TrainingPeriodKind.Rolling7Days;

	public event EventHandler? Changed;

	public TrainingPeriodWindow Window(DateOnly today)
	{
		var anchor = _anchors.TryGetValue(Kind, out var stored)
			? stored
			: TrainingPeriodCalendar.DefaultAnchor(Kind, today);
		return TrainingPeriodCalendar.Window(Kind, anchor, today);
	}

	public string Chip(TrainingPeriodKind kind, DateOnly today)
	{
		var anchor = _anchors.TryGetValue(kind, out var stored)
			? stored
			: TrainingPeriodCalendar.DefaultAnchor(kind, today);
		return TrainingPeriodCalendar.Window(kind, anchor, today).ChipLabel;
	}

	public void Select(TrainingPeriodKind kind)
	{
		Kind = kind;
		Changed?.Invoke(this, EventArgs.Empty);
	}

	public void Previous(DateOnly today)
	{
		var window = Window(today);
		_anchors[Kind] = TrainingPeriodCalendar.PreviousAnchor(window);
		Changed?.Invoke(this, EventArgs.Empty);
	}

	public void Next(DateOnly today)
	{
		var window = Window(today);
		var next = TrainingPeriodCalendar.NextAnchor(window, today);
		if (next is null)
			return;
		_anchors[Kind] = next.Value;
		Changed?.Invoke(this, EventArgs.Empty);
	}
}
