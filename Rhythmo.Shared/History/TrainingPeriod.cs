namespace Rhythmo.Shared.History;

public enum TrainingPeriodKind
{
	Rolling7Days,
	CalendarMonth,
	ThreeMonths,
	CalendarYear
}

public sealed record TrainingPeriodWindow(
	TrainingPeriodKind Kind,
	DateOnly Start,
	DateOnly End,
	string ChipLabel,
	string RangeLabel,
	bool CanGoNext);

public static class TrainingPeriodCalendar
{
	private static readonly string[] MonthChips =
		["", "Jan.", "Févr.", "Mars", "Avr.", "Mai", "Juin", "Juil.", "Août", "Sept.", "Oct.", "Nov.", "Déc."];

	private static readonly string[] MonthRange =
		["", "janv.", "févr.", "mars", "avr.", "mai", "juin", "juil.", "août", "sept.", "oct.", "nov.", "déc."];

	public static DateOnly DefaultAnchor(TrainingPeriodKind kind, DateOnly today) =>
		kind switch
		{
			TrainingPeriodKind.Rolling7Days => today,
			TrainingPeriodKind.CalendarMonth => new DateOnly(today.Year, today.Month, 1),
			TrainingPeriodKind.ThreeMonths => new DateOnly(today.Year, today.Month, 1),
			TrainingPeriodKind.CalendarYear => new DateOnly(today.Year, 1, 1),
			_ => throw new ArgumentOutOfRangeException(nameof(kind))
		};

	public static TrainingPeriodWindow Window(TrainingPeriodKind kind, DateOnly anchor, DateOnly today) =>
		kind switch
		{
			TrainingPeriodKind.Rolling7Days => SevenDays(anchor > today ? today : anchor, today),
			TrainingPeriodKind.CalendarMonth => Month(new DateOnly(anchor.Year, anchor.Month, 1), today),
			TrainingPeriodKind.ThreeMonths => ThreeMonths(AlignThreeMonthEnd(anchor, today), today),
			TrainingPeriodKind.CalendarYear => Year(anchor.Year > today.Year ? today.Year : anchor.Year, today),
			_ => throw new ArgumentOutOfRangeException(nameof(kind))
		};

	public static TrainingPeriodWindow Current(TrainingPeriodKind kind, DateOnly today) =>
		Window(kind, DefaultAnchor(kind, today), today);

	public static DateOnly PreviousAnchor(TrainingPeriodWindow window)
	{
		return window.Kind switch
		{
			TrainingPeriodKind.Rolling7Days => window.End.AddDays(-7),
			TrainingPeriodKind.CalendarMonth => new DateOnly(window.Start.Year, window.Start.Month, 1).AddMonths(-1),
			TrainingPeriodKind.ThreeMonths => EndMonthOf(window).AddMonths(-3),
			TrainingPeriodKind.CalendarYear => new DateOnly(window.Start.Year - 1, 1, 1),
			_ => throw new ArgumentOutOfRangeException(nameof(window))
		};
	}

	public static DateOnly? NextAnchor(TrainingPeriodWindow window, DateOnly today)
	{
		if (!window.CanGoNext)
			return null;
		return window.Kind switch
		{
			TrainingPeriodKind.Rolling7Days => window.End.AddDays(7) > today ? today : window.End.AddDays(7),
			TrainingPeriodKind.CalendarMonth => new DateOnly(window.Start.Year, window.Start.Month, 1).AddMonths(1),
			TrainingPeriodKind.ThreeMonths => EndMonthOf(window).AddMonths(3),
			TrainingPeriodKind.CalendarYear => new DateOnly(window.Start.Year + 1, 1, 1),
			_ => null
		};
	}

	public static bool Contains(TrainingPeriodWindow window, DateOnly day) =>
		day >= window.Start && day <= window.End;

	public static string Chip(TrainingPeriodKind kind, DateOnly today, DateOnly anchor)
	{
		var window = Window(kind, anchor, today);
		return window.ChipLabel;
	}

	private static TrainingPeriodWindow SevenDays(DateOnly end, DateOnly today)
	{
		if (end > today)
			end = today;
		var start = end.AddDays(-6);
		return new TrainingPeriodWindow(
			TrainingPeriodKind.Rolling7Days,
			start,
			end,
			"7 j",
			FormatRange(start, end),
			end < today);
	}

	private static TrainingPeriodWindow Month(DateOnly first, DateOnly today)
	{
		if (first > new DateOnly(today.Year, today.Month, 1))
			first = new DateOnly(today.Year, today.Month, 1);
		var last = first.AddMonths(1).AddDays(-1);
		var isCurrent = first.Year == today.Year && first.Month == today.Month;
		var end = isCurrent ? today : last;
		return new TrainingPeriodWindow(
			TrainingPeriodKind.CalendarMonth,
			first,
			end,
			MonthChips[first.Month],
			FormatRange(first, end),
			first.Year < today.Year || (first.Year == today.Year && first.Month < today.Month));
	}

	private static TrainingPeriodWindow ThreeMonths(DateOnly endMonth, DateOnly today)
	{
		var start = endMonth.AddMonths(-2);
		var isCurrent = endMonth.Year == today.Year && endMonth.Month == today.Month;
		var end = isCurrent ? today : endMonth.AddMonths(1).AddDays(-1);
		var currentEnd = new DateOnly(today.Year, today.Month, 1);
		return new TrainingPeriodWindow(
			TrainingPeriodKind.ThreeMonths,
			start,
			end,
			"3 mois",
			FormatRange(start, end),
			endMonth < currentEnd);
	}

	private static TrainingPeriodWindow Year(int year, DateOnly today)
	{
		if (year > today.Year)
			year = today.Year;
		var start = new DateOnly(year, 1, 1);
		var end = year == today.Year ? today : new DateOnly(year, 12, 31);
		return new TrainingPeriodWindow(
			TrainingPeriodKind.CalendarYear,
			start,
			end,
			year.ToString(),
			FormatRange(start, end),
			year < today.Year);
	}

	private static DateOnly AlignThreeMonthEnd(DateOnly anchor, DateOnly today)
	{
		var end = new DateOnly(anchor.Year, anchor.Month, 1);
		var current = new DateOnly(today.Year, today.Month, 1);
		if (end > current)
			end = current;
		var targetMod = current.Month % 3;
		while (end.Month % 3 != targetMod)
			end = end.AddMonths(-1);
		return end;
	}

	private static DateOnly EndMonthOf(TrainingPeriodWindow window) =>
		new(window.End.Year, window.End.Month, 1);

	public static string FormatRange(DateOnly start, DateOnly end)
	{
		if (start == end)
			return $"{start.Day} {MonthRange[start.Month]} {start.Year}";
		if (start.Year == end.Year && start.Month == end.Month)
			return $"{start.Day}–{end.Day} {MonthRange[end.Month]} {end.Year}";
		if (start.Year == end.Year)
			return $"{start.Day} {MonthRange[start.Month]}–{end.Day} {MonthRange[end.Month]} {end.Year}";
		return $"{start.Day} {MonthRange[start.Month]} {start.Year}–{end.Day} {MonthRange[end.Month]} {end.Year}";
	}
}
