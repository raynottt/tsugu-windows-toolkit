using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TidyWindow.Next.Core;

namespace TidyWindow.Next.Desktop;

public sealed class SuggestedApp
{
    public SuggestedApp(string name, string category, string description, string sourceLabel, string actionLabel,
        string installUrl, string? sourceUrl = null, string? packageId = null, bool interactive = false,
        string? wingetSource = null, SuggestedInstallSpec? installSpec = null, string glyph = "\uE71D")
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
        WingetSource = wingetSource;
        InstallSpec = installSpec;
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
    public string? WingetSource { get; }
    public SuggestedInstallSpec? InstallSpec { get; }
    public string Glyph { get; }
    public bool IsWinget => !string.IsNullOrWhiteSpace(PackageId);
    public bool CanAutoInstall => IsWinget || InstallSpec is not null;
    public Visibility SourceVisibility => string.IsNullOrWhiteSpace(SourceUrl) ? Visibility.Collapsed : Visibility.Visible;
    public string ActionAutomationName => IsWinget ? $"Add {Name} to the WinGet install queue"
        : InstallSpec is not null ? $"Download and install {Name} through TSUGU"
        : $"Open official page for {Name}";
    public string SourceAutomationName => $"Open source for {Name}";
}

public sealed partial class SuggestedView : UserControl
{
    private static SuggestedApp GitHub(string name, string category, string description, string repo, string assetPattern,
        SuggestedInstallPayload payload, string? checksumPattern = null, bool allowUnsigned = false,
        string? innerExecutable = null, bool bodyChecksum = false, string? readme = null, string glyph = "\uE8A5")
    {
        var slug = Regex.Replace(name.ToLowerInvariant(), @"[^a-z0-9._-]+", "-").Trim('-');
        var spec = new SuggestedInstallSpec(
            slug,
            name,
            SuggestedInstallSource.GitHubRelease,
            payload,
            Repository: repo,
            AssetPattern: assetPattern,
            ChecksumAssetPattern: checksumPattern,
            AllowUnsigned: allowUnsigned,
            InnerExecutablePattern: innerExecutable,
            UseReleaseBodyChecksum: bodyChecksum,
            ReadmeUrl: readme);
        var action = payload switch
        {
            SuggestedInstallPayload.ManualArchive => "Prepare package",
            SuggestedInstallPayload.PortableScanner => "Download & launch",
            SuggestedInstallPayload.PortableExecutable or SuggestedInstallPayload.PortableArchive => "Install & launch",
            _ => "Install via TSUGU"
        };
        return new(name, category, description, "GITHUB RELEASES · " + repo.ToUpperInvariant(), action,
            $"https://github.com/{repo}/releases/latest", $"https://github.com/{repo}", installSpec: spec, glyph: glyph);
    }

    private static readonly IReadOnlyList<SuggestedApp> Catalog =
    [
        GitHub("Fulgur", "Utilities", "Fast, cross-platform text editor. TSUGU selects the latest Windows x64 setup and verifies its SHA-256 release manifest.",
            "fulgur-app/fulgur", @"^Fulgur-.*-windows-x86_64-setup\.exe$", SuggestedInstallPayload.Installer,
            checksumPattern: @"^SHA256SUMS\.txt$", glyph: "\uE8A5"),
        GitHub("SteamInRust", "Gaming", "Native Steam client rewrite. Downloads the user-level Windows setup and verifies SHA256SUMS.txt before launch.",
            "madonchik123/SteamInRust", @"^SteamInRust-Setup-[\w.-]+\.exe$", SuggestedInstallPayload.Installer,
            checksumPattern: @"^SHA256SUMS\.txt$", glyph: "\uE7FC"),
        new("Microsoft Store app", "Utilities", "Opens the Store listing you supplied directly in Microsoft Store; the Store handles the app installation.",
            "MICROSOFT STORE", "Open Store", "ms-windows-store://pdp/?ProductId=9N97ZCKPD60Q",
            "https://apps.microsoft.com/detail/9n97zckpd60q?hl=ru-RU&gl=NL", glyph: "\uE7B8"),
        GitHub("Google Antigravity (community build)", "AI", "Community release from the repository you supplied. TSUGU downloads its Windows EXE; the build is unsigned unless the release begins publishing a verifiable checksum.",
            "confeden/Antigravity", @"^AG_[\w.-]+\.exe$", SuggestedInstallPayload.Installer,
            allowUnsigned: true, glyph: "\uE753"),
        new("Google Antigravity (official)", "AI", "TSUGU reads Google's official download page at install time, selects the Windows x64 setup (not ARM64), verifies the Google Authenticode signature and launches it.",
            "GOOGLE OFFICIAL · LIVE WINDOWS X64 LINK", "Download & install", "https://antigravity.google/download/",
            "https://antigravity.google/download/",
            installSpec: new("google-antigravity", "Google Antigravity", SuggestedInstallSource.OfficialDownloadPage,
                SuggestedInstallPayload.Installer, AssetPattern: @"/windows-x64/Antigravity-x64\.exe$", DirectUrl: "https://antigravity.google/download/", ExpectedSigner: "Google LLC"),
            glyph: "\uE753"),
        new("OpenAI Codex", "AI", "Official Windows installation through WinGet's Microsoft Store source. The app opens with Codex in the ChatGPT desktop app after you sign in.",
            "WINGET · MICROSOFT STORE", "Add to queue", "ms-windows-store://pdp/?ProductId=9PLM9XGG6VKS",
            "https://openai.com/ru-RU/codex/", packageId: "9PLM9XGG6VKS", wingetSource: "msstore", glyph: "\uE756"),
        new("Discord", "Communication", "Install Discord through the existing WinGet queue. Installer options and progress remain in TSUGU.",
            "WINGET · DISCORD.DISCORD", "Add to queue", "https://winget.run/pkg/Discord/Discord",
            packageId: "Discord.Discord", glyph: "\uE8BD"),
        new("TeamSpeak Client", "Communication", "Interactive install: the TeamSpeak setup window may open for you to finish.",
            "WINGET · INTERACTIVE INSTALL", "Add to queue", "https://winget.run/pkg/TeamSpeakSystems/TeamSpeakClient",
            packageId: "TeamSpeakSystems.TeamSpeakClient", interactive: true, glyph: "\uE714"),
        GitHub("Heronite", "Gaming", "Game library and launcher. Uses the latest Windows x64 setup and the publisher's SHA256SUMS manifest.",
            "RafaelGoulartB/Heronite", @"^Heronite-Setup-x86_64\.exe$", SuggestedInstallPayload.Installer,
            checksumPattern: @"^SHA256SUMS$", glyph: "\uE8A5"),
        new("PinTop", "Utilities", "The developer's official download page. The current page does not expose a stable, documented direct installer URL for unattended retrieval.",
            "OFFICIAL DOWNLOAD PAGE", "Open download", "https://www.youngsam.net/en/programs/pintop", glyph: "\uE840"),
        GitHub("SoundSwitch", "Audio", "Switch playback and recording devices from the tray. Downloads the latest x64 installer; TSUGU checks Authenticode and warns if the release is unsigned.",
            "belphemur/soundswitch", @"^SoundSwitch_v[\w.-]+_Release_Installer\.exe$", SuggestedInstallPayload.Installer,
            allowUnsigned: true, glyph: "\uE767"),
        GitHub("Equalizer APO 64", "Audio", "This is a patched 64-bit engine, not a standalone installer. TSUGU downloads and extracts it, but will not overwrite audio files automatically; the existing Equalizer APO setup and device selector must be handled first.",
            "TheFireKahuna/equalizerAPO64", @"^EqualizerAPO_Setup-x64-avx2\.zip$", SuggestedInstallPayload.ManualArchive,
            allowUnsigned: true, readme: "https://github.com/TheFireKahuna/equalizerAPO64/releases/latest", glyph: "\uE767"),
        GitHub("Raw Accel", "Gaming", "Downloads the official driver archive, extracts it and runs installer.exe. This installs a mouse driver, may request UAC approval and can require a restart.",
            "RawAccelOfficial/rawaccel", @"^RawAccel_v[\w.-]+\.zip$", SuggestedInstallPayload.ArchiveInstaller,
            allowUnsigned: true, innerExecutable: "installer.exe", readme: "https://github.com/RawAccelOfficial/rawaccel/blob/master/doc/Guide.md", glyph: "\uE962"),
        new("Emsisoft Emergency Kit", "Security", "Portable on-demand malware scanner. TSUGU downloads the official Emsisoft executable, checks its Authenticode publisher, then launches it. It is not persistent antivirus protection.",
            "EMSISOFT OFFICIAL · PORTABLE SCANNER", "Download & launch",
            "https://dl.emsisoft.com/EmsisoftEmergencyKit.exe", "https://www.emsisoft.com/en/emergency-kit/",
            installSpec: new("emsisoft-eek", "Emsisoft Emergency Kit", SuggestedInstallSource.DirectDownload, SuggestedInstallPayload.PortableScanner,
                DirectUrl: "https://dl.emsisoft.com/EmsisoftEmergencyKit.exe", ExpectedSigner: "Emsisoft"), glyph: "\uE83D"),
        new("Kaspersky Virus Removal Tool", "Security", "Portable on-demand malware scanner. TSUGU retrieves the current KVRT binary from Kaspersky's download host, verifies its Authenticode publisher, then launches it.",
            "KASPERSKY OFFICIAL · PORTABLE SCANNER", "Download & launch",
            "https://devbuilds.s.kaspersky-labs.com/devbuilds/KVRT/latest/full/KVRT.exe", "https://www.kaspersky.com/downloads/free-virus-removal-tool",
            installSpec: new("kaspersky-kvrt", "Kaspersky Virus Removal Tool", SuggestedInstallSource.DirectDownload, SuggestedInstallPayload.PortableScanner,
                DirectUrl: "https://devbuilds.s.kaspersky-labs.com/devbuilds/KVRT/latest/full/KVRT.exe", ExpectedSigner: "Kaspersky"), glyph: "\uE83D"),
        new("ArtCraft", "Creative", "Open the official ArtCraft download page. The page currently does not expose a stable direct Windows asset to TSUGU, so the vendor page remains the source of truth.",
            "OFFICIAL DOWNLOAD PAGE", "Open download", "https://getartcraft.com/download", glyph: "\uE8D4"),
        new("qBittorrent Enhanced Edition", "Utilities", "Install the enhanced qBittorrent build through the existing WinGet queue.",
            "WINGET · C0RE100.QBITTORRENT-ENHANCED-EDITION", "Add to queue", "https://winget.run/pkg/c0re100/qBittorrent-Enhanced-Edition",
            packageId: "c0re100.qBittorrent-Enhanced-Edition", glyph: "\uE896"),
        GitHub("FlClashX", "Network", "Cross-platform client. Downloads the current Windows x64 installer and checks the per-asset SHA-256 sidecar before running it.",
            "pluralplay/FlClashX", @"^FlClashX-windows-amd64-setup\.exe$", SuggestedInstallPayload.Installer,
            checksumPattern: @"^FlClashX-windows-amd64-setup\.exe\.sha256$", glyph: "\uE774"),
        GitHub("Solaar for Windows", "Utilities", "Extracts the official Windows ZIP into TSUGU's user app folder and launches the supplied Solaar.bat entry point. The release includes a SHA-256 digest.",
            "jazzvier1/Solaar-Windows", @"^Solaar-Windows\.zip$", SuggestedInstallPayload.PortableArchive,
            bodyChecksum: true, innerExecutable: "Solaar.bat", glyph: "\uE772"),
        GitHub("SoundKitten", "Audio", "Downloads the latest Windows x64 setup. The upstream release attaches a signature sidecar but no checksum manifest; TSUGU warns before running an unsigned build.",
            "SRock44/soundkitten", @"^SoundKitten_[\w.-]+_x64-setup\.exe$", SuggestedInstallPayload.Installer,
            allowUnsigned: true, glyph: "\uE767"),
        GitHub("Senthym", "Utilities", "A single-file Windows player, not a setup wizard. TSUGU places the executable in its user app folder, creates a Start menu shortcut and launches it.",
            "fabox-os/Senthym", @"^Senthym-windows\.exe$", SuggestedInstallPayload.PortableExecutable,
            allowUnsigned: true, glyph: "\uE8A5"),
        GitHub("Zephium Browser", "Browser", "Downloads the latest Windows x64 setup and verifies the SHA256SUMS manifest before starting the installer.",
            "zephium-browser/Zephium", @"^Zephium-Windows-x64-setup\.exe$", SuggestedInstallPayload.Installer,
            checksumPattern: @"^SHA256SUMS$", glyph: "\uE774"),
        new("FACEIT Anti-Cheat", "Gaming", "Opens FACEIT's official anti-cheat page. It installs a system-level component, so review the requirements and UAC prompt before proceeding.",
            "OFFICIAL FACEIT PAGE", "Open anti-cheat page", "https://www.faceit.com/ru/anti-cheat", glyph: "\uE83D"),
        new("MSI Afterburner", "Gaming", "Interactive WinGet install. A separate setup window may appear for you to complete.",
            "WINGET · INTERACTIVE INSTALL", "Add to queue", "https://winget.run/pkg/Guru3D/Afterburner",
            packageId: "Guru3D.Afterburner", interactive: true, glyph: "\uE9D9"),
        new("Stats.cc Overlay", "Gaming", "Open the Stats.cc overlay website.", "WEB APP", "Open website", "https://stats.cc/overlay", glyph: "\uE9D9")
    ];

    public int TotalSuggestions => Catalog.Count;
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
        if (app.CanAutoInstall) InstallRequested?.Invoke(app);
        else OpenRequested?.Invoke(app.InstallUrl);
    }

    private void SourceClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: SuggestedApp { SourceUrl: { Length: > 0 } url } })
            OpenRequested?.Invoke(url);
    }
}
