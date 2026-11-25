using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace TidyWindow.Next.Desktop;

public sealed class SuggestedApp
{
    public SuggestedApp(string name, string category, string description, string sourceLabel, string actionLabel,
        string installUrl, string? sourceUrl = null, string? packageId = null, bool interactive = false, string glyph = "\\uE71D")
    {
        Name = name;
        Category = category;
        Description = description;
        SourceLabel = sourceLabel;
        ActionLabel = actionLabel;
        InstallUrl = installUrl;
        SourceUrl = sourceUrl;
        PackageId = packageId;
        Interactive = interactive;
        Glyph = glyph;
    }

    public string Name { get; }
    public string Category { get; }
    public string Description { get; }
    public string SourceLabel { get; }
    public string ActionLabel { get; }
    public string InstallUrl { get; }
    public string? SourceUrl { get; }
    public string? PackageId { get; }
    public bool Interactive { get; }
    public string Glyph { get; }
    public bool IsWinget => !string.IsNullOrWhiteSpace(PackageId);
    public Visibility SourceVisibility => string.IsNullOrWhiteSpace(SourceUrl) ? Visibility.Collapsed : Visibility.Visible;
    public string ActionAutomationName => IsWinget ? $"Add {Name} to the WinGet install queue" : $"Open download page for {Name}";
    public string SourceAutomationName => $"Open source for {Name}";
}

public sealed partial class SuggestedView : UserControl
{
    private static readonly IReadOnlyList<SuggestedApp> Catalog =
    [
        new("Fulgur", "Utilities", "Open-source desktop app. Choose the Windows x64 installer from its latest GitHub release.", "GITHUB RELEASES · WINDOWS X64 ASSET", "View release", "https://github.com/fulgur-app/fulgur/releases/latest", "https://github.com/fulgur-app/fulgur", glyph: "\uE8A5"),
        new("SteamInRust", "Gaming", "Community Steam-related utility. Review the release notes and choose the setup or portable build.", "GITHUB RELEASES · WINDOWS SETUP", "View release", "https://github.com/madonchik123/SteamInRust/releases/latest", "https://github.com/madonchik123/SteamInRust", glyph: "\uE7FC"),
        new("Microsoft Store app", "Utilities", "Open the Microsoft Store listing you supplied and install it through the Store.", "MICROSOFT STORE", "Open Store listing", "https://apps.microsoft.com/detail/9n97zckpd60q?hl=ru-RU&gl=NL", glyph: "\uE7B8"),
        new("Google Antigravity", "AI", "Open Google's official download page. The GitHub builds are available from the source link.", "OFFICIAL DOWNLOAD · GITHUB ALTERNATIVE", "Official download", "https://antigravity.google/download/", "https://github.com/confeden/Antigravity/releases", glyph: "\uE753"),
        new("OpenAI Codex", "AI", "Open the official Codex product page to choose the supported desktop or editor workflow.", "OFFICIAL OPENAI PAGE", "Open Codex", "https://openai.com/ru-RU/codex/", glyph: "\uE756"),
        new("Discord", "Communication", "Install Discord through WinGet and TSUGU's existing installation queue.", "WINGET · DISCORD.DISCORD", "Add to queue", "https://winget.run/pkg/Discord/Discord", packageId: "Discord.Discord", glyph: "\uE8BD"),
        new("TeamSpeak Client", "Communication", "Interactive install: the TeamSpeak setup window may open for you to finish.", "WINGET · INTERACTIVE INSTALL", "Add to queue", "https://winget.run/pkg/TeamSpeakSystems/TeamSpeakClient", packageId: "TeamSpeakSystems.TeamSpeakClient", interactive: true, glyph: "\uE714"),
        new("Heronite", "Utilities", "Community project with Windows x64 setup and portable assets in GitHub Releases.", "GITHUB RELEASES · WINDOWS X64", "View release", "https://github.com/RafaelGoulartB/Heronite/releases/latest", "https://github.com/RafaelGoulartB/Heronite", glyph: "\uE8A5"),
        new("PinTop", "Utilities", "Open the developer's official PinTop download page.", "OFFICIAL DOWNLOAD", "Open download", "https://www.youngsam.net/en/programs/pintop", glyph: "\uE840"),
        new("SoundSwitch", "Audio", "Switch Windows playback and recording devices with a small tray utility.", "GITHUB RELEASES · WINDOWS INSTALLER", "View release", "https://github.com/belphemur/soundswitch/releases/latest", "https://github.com/belphemur/soundswitch", glyph: "\uE767"),
        new("Equalizer APO 64", "Audio", "System-wide audio processing. Review the setup options and device requirements before installing.", "GITHUB RELEASES · SETUP ARCHIVE", "View release", "https://github.com/TheFireKahuna/equalizerAPO64/releases/latest", "https://github.com/TheFireKahuna/equalizerAPO64", glyph: "\uE767"),
        new("Raw Accel", "Gaming", "Mouse acceleration utility with a driver component. Review the release and installation notes first.", "GITHUB RELEASES · DRIVER UTILITY", "View release", "https://github.com/RawAccelOfficial/rawaccel/releases/latest", "https://github.com/RawAccelOfficial/rawaccel", glyph: "\uE962"),
        new("Emsisoft Emergency Kit", "Security", "On-demand malware scanning toolkit from Emsisoft's official download page.", "OFFICIAL EMSISOFT DOWNLOAD", "Open download", "https://www.emsisoft.com/en/emergency-kit/", glyph: "\uE83D"),
        new("ArtCraft", "Creative", "Open the official ArtCraft download page.", "OFFICIAL DOWNLOAD", "Open download", "https://getartcraft.com/download", glyph: "\uE8D4"),
        new("qBittorrent Enhanced Edition", "Utilities", "Install the enhanced qBittorrent edition through WinGet.", "WINGET · C0RE100.QBITTORRENT-ENHANCED-EDITION", "Add to queue", "https://winget.run/pkg/c0re100/qBittorrent-Enhanced-Edition", packageId: "c0re100.qBittorrent-Enhanced-Edition", glyph: "\uE896"),
        new("FlClashX", "Network", "Cross-platform network client with Windows x64 setup and portable release assets.", "GITHUB RELEASES · WINDOWS X64", "View release", "https://github.com/pluralplay/FlClashX/releases/latest", "https://github.com/pluralplay/FlClashX", glyph: "\uE774"),
        new("Solaar for Windows", "Utilities", "Windows build of the Solaar device manager. Check the release notes for supported devices.", "GITHUB RELEASES · WINDOWS ZIP", "View release", "https://github.com/jazzvier1/Solaar-Windows/releases/latest", "https://github.com/jazzvier1/Solaar-Windows", glyph: "\uE772"),
        new("SoundKitten", "Audio", "Audio utility with Windows x64 installer and signature sidecar in its latest release.", "GITHUB RELEASES · WINDOWS X64", "View release", "https://github.com/SRock44/soundkitten/releases/latest", "https://github.com/SRock44/soundkitten", glyph: "\uE767"),
        new("Senthym", "Utilities", "Open-source desktop project with a Windows executable in its latest release.", "GITHUB RELEASES · WINDOWS BUILD", "View release", "https://github.com/fabox-os/Senthym/releases/latest", "https://github.com/fabox-os/Senthym", glyph: "\uE8A5"),
        new("Zephium Browser", "Browser", "Open-source browser project with a Windows x64 setup asset and release checksums.", "GITHUB RELEASES · WINDOWS X64", "View release", "https://github.com/zephium-browser/Zephium/releases/latest", "https://github.com/zephium-browser/Zephium", glyph: "\uE774"),
        new("FACEIT Anti-Cheat", "Gaming", "Open FACEIT's official anti-cheat page. It installs a system-level component; read the requirements before setup.", "OFFICIAL FACEIT PAGE", "Open anti-cheat page", "https://www.faceit.com/ru/anti-cheat", glyph: "\uE83D"),
        new("MSI Afterburner", "Gaming", "Interactive installation through WinGet. Review the publisher and setup prompts before proceeding.", "WINGET · INTERACTIVE INSTALL", "Add to queue", "https://winget.run/pkg/Guru3D/Afterburner", packageId: "Guru3D.Afterburner", interactive: true, glyph: "\uE9D9"),
        new("Stats.cc Overlay", "Gaming", "Open the Stats.cc overlay website.", "WEB APP", "Open website", "https://stats.cc/overlay", glyph: "\uE9D9")
    ];

    public ObservableCollection<SuggestedApp> Rows { get; } = [];
    public event Action<SuggestedApp>? InstallRequested;
    public event Action<string>? OpenRequested;

    public SuggestedView()
    {
        InitializeComponent();
        DataContext = this;
        CategoryPicker.ItemsSource = new[] { "All categories", "AI", "Audio", "Browser", "Communication", "Creative", "Gaming", "Network", "Security", "Utilities" };
        CategoryPicker.SelectedIndex = 0;
        ApplyFilter();
    }

    private void FilterChanged(object sender, RoutedEventArgs e) => ApplyFilter();

    private void ApplyFilter()
    {
        var query = SearchBox?.Text.Trim() ?? "";
        var category = CategoryPicker?.SelectedItem as string;
        var matches = Catalog.Where(app =>
            (category is null or "All categories" || app.Category.Equals(category, StringComparison.OrdinalIgnoreCase)) &&
            (query.Length == 0 || app.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
             app.Description.Contains(query, StringComparison.OrdinalIgnoreCase) ||
             app.Category.Contains(query, StringComparison.OrdinalIgnoreCase)));
        Rows.Clear();
        foreach (var app in matches) Rows.Add(app);
        CountText.Text = $"{Rows.Count} suggestions";
        EmptyState.Visibility = Rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void PrimaryClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: SuggestedApp app }) return;
        if (app.IsWinget) InstallRequested?.Invoke(app);
        else OpenRequested?.Invoke(app.InstallUrl);
    }

    private void SourceClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: SuggestedApp { SourceUrl: { Length: > 0 } url } })
            OpenRequested?.Invoke(url);
    }
}
