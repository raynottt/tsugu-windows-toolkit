namespace TidyWindow.Next.Desktop;

internal sealed record AdaptiveLayout(double SidebarWidth, double Padding, bool CompactManagers, bool CompactResults)
{
    /// <summary>The sidebar stays expanded like Windows Settings; the content column is reserved at every size.</summary>
    public const double RailWidth = 288;
    public const double OpenWidth = 288;
    /// <summary>The sidebar always reserves its own column and never overlays the page.</summary>
    public const double OverlayWidth = 1200;

    public static bool PushesContent(double width) => width < OverlayWidth;

    public static AdaptiveLayout ForWidth(double width, bool sidebarOpen = false)
    {
        var sidebar = sidebarOpen && PushesContent(width) ? OpenWidth : RailWidth;
        var padding = width < 700 ? 16 : width < 1100 ? 24 : 36;
        var content = Math.Max(0, width - sidebar - 2 * padding);
        return new(sidebar, padding, content < 600, content < 720);
    }
}
