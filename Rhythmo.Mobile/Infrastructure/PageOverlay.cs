using Microsoft.Maui.Controls;

namespace Rhythmo.Mobile.Infrastructure;

/// <summary>Attache un overlay plein écran, y compris sur les grilles multi-lignes.</summary>
internal static class PageOverlay
{
	public static bool Attach(Page page, Grid overlay)
	{
		if (page is not ContentPage cp)
			return false;

		if (cp.Content is Grid host)
		{
			SpanHost(host, overlay);
			return true;
		}

		var wrapper = new Grid();
		if (cp.Content is not null)
			wrapper.Children.Add(cp.Content);
		wrapper.Children.Add(overlay);
		cp.Content = wrapper;
		return true;
	}

	public static void Detach(Page page, Grid overlay)
	{
		if (page is not ContentPage cp || cp.Content is not Grid host)
			return;

		host.Children.Remove(overlay);
	}

	private static void SpanHost(Grid host, Grid overlay)
	{
		Grid.SetRow(overlay, 0);
		Grid.SetRowSpan(overlay, Math.Max(1, host.RowDefinitions.Count));
		Grid.SetColumn(overlay, 0);
		Grid.SetColumnSpan(overlay, Math.Max(1, host.ColumnDefinitions.Count));
		host.Children.Add(overlay);
	}
}
