namespace TidyWindow.Next.Desktop;

internal sealed record AdaptiveLayout(double SidebarWidth, double Padding, bool CompactManagers, bool CompactResults)
{
    /// <summary>On wide windows the pane stays expanded like Windows Settings; narrow windows use a compact rail that can expand without covering the page.</summary>
    public const double RailWidth = 56;
    public const double OpenWidth = 288;
    /// <summary>On wide windows the full pane stays open; on narrow windows an expanded pane pushes content aside.</summary>
    public const double OverlayWidth = 900;

    public static bool PushesContent(double width) => width < OverlayWidth;

    public static AdaptiveLayout ForWidth(double width, bool sidebarOpen = false)
    {
        var sidebar = width >= OverlayWidth || sidebarOpen && PushesContent(width) ? OpenWidth : RailWidth;
        var padding = width < 700 ? 16 : width < 1100 ? 24 : 36;
        var content = Math.Max(0, width - sidebar - 2 * padding);
        return new(sidebar, padding, content < 600, content < 720);
    }
}
