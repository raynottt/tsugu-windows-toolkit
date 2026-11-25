using System.ComponentModel;
using System.Net;
using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace TidyWindow.Next.Core;

public enum SuggestedInstallSource { GitHubRelease, DirectDownload, OfficialDownloadPage }
public enum SuggestedInstallPayload { Installer, PortableExecutable, PortableScanner, ArchiveInstaller, PortableArchive, ManualArchive }

public interface ISuggestedInstallService
{
    Task<PackageActionResult> InstallAsync(SuggestedInstallSpec spec, IProgress<CommandOutput>? progress = null, CancellationToken token = default);
}

/// <summary>
/// A curated installation recipe. GitHub recipes resolve the latest release at install time; direct downloads are limited
/// to the official vendor hosts allow-listed below. No shell command is accepted from the catalog.
/// </summary>
public sealed record SuggestedInstallSpec(
    string Id,
    string Name,
    SuggestedInstallSource Source,
    SuggestedInstallPayload Payload,
    string? Repository = null,
    string AssetPattern = "",
    string? ChecksumAssetPattern = null,
    string? DirectUrl = null,
    string? ExpectedSigner = null,
    string? InnerExecutablePattern = null,
    bool UseReleaseBodyChecksum = false,
    string? ReadmeUrl = null,
    string? ExpectedSha256 = null,
    string? AssetHost = null,
    bool UsePageChecksum = false)
{
    public string ResourceKey => "suggested:" + Id.ToUpperInvariant();
}

/// <summary>Downloads and launches a curated app from its upstream source, within the normal TSUGU operation queue.</summary>
public sealed class SuggestedInstallService(ICommandRunner runner, IExecutableLocator locator) : ISuggestedInstallService
{
    private const long MaximumDownloadBytes = 1_500_000_000;
    private static readonly HttpClient Http = CreateHttpClient();
    private static readonly string Root = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TSUGU", "Suggested");

    private sealed record ResolvedDownload(Uri Uri, string FileName, string Version, string? ExpectedHash, string? ChecksumText);

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(20) };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("TSUGU", "1.0"));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return client;
    }

    public async Task<PackageActionResult> InstallAsync(
        SuggestedInstallSpec spec,
        IProgress<CommandOutput>? progress = null,
        CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(spec.Id) || !Regex.IsMatch(spec.Id, @"^[A-Za-z0-9][A-Za-z0-9._-]{0,63}$"))
            throw new ArgumentException("The suggested app ID is invalid.", nameof(spec));

        Directory.CreateDirectory(Root);
        var appFolder = Path.Combine(Root, spec.Id);
        Directory.CreateDirectory(appFolder);

        Report(progress, $"Resolving the latest official download for {spec.Name}…");
        var resolved = spec.Source switch
        {
            SuggestedInstallSource.GitHubRelease => await ResolveGitHubReleaseAsync(spec, token).ConfigureAwait(false),
            SuggestedInstallSource.DirectDownload => ResolveDirect(spec),
            SuggestedInstallSource.OfficialDownloadPage => await ResolveOfficialDownloadPageAsync(spec, token).ConfigureAwait(false),
            _ => throw new ArgumentOutOfRangeException(nameof(spec.Source))
        };

        var downloadPath = Path.Combine(appFolder, SafeFileName(resolved.FileName));
        var partialPath = downloadPath + ".download";
        try
        {
            await DownloadAsync(resolved.Uri, partialPath, progress, token).ConfigureAwait(false);
            File.Move(partialPath, downloadPath, overwrite: true);
        }
        finally
        {
            try { if (File.Exists(partialPath)) File.Delete(partialPath); }
            catch (IOException) { }
        }

        Report(progress, "Verifying the downloaded file…");
        await VerifyAsync(downloadPath, spec, resolved, progress, token).ConfigureAwait(false);

        return spec.Payload switch
        {
            SuggestedInstallPayload.Installer => await RunInstallerAsync(spec, downloadPath, resolved.Version, progress, token).ConfigureAwait(false),
            SuggestedInstallPayload.PortableScanner => LaunchPortable(downloadPath, resolved.Version, $"Downloaded and launched {spec.Name}. It is a portable on-demand tool; it does not install continuous protection."),
            SuggestedInstallPayload.PortableExecutable => await InstallPortableExecutableAsync(spec, downloadPath, appFolder, resolved.Version, progress, token).ConfigureAwait(false),
            SuggestedInstallPayload.ArchiveInstaller => await InstallFromArchiveAsync(spec, downloadPath, appFolder, resolved.Version, progress, token).ConfigureAwait(false),
            SuggestedInstallPayload.PortableArchive => InstallPortableArchive(spec, downloadPath, appFolder, resolved.Version),
            SuggestedInstallPayload.ManualArchive => PrepareManualArchive(spec, downloadPath, appFolder, resolved.Version),
            _ => throw new ArgumentOutOfRangeException(nameof(spec.Payload))
        };
    }

    private static ResolvedDownload ResolveDirect(SuggestedInstallSpec spec)
    {
        if (!Uri.TryCreate(spec.DirectUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            throw new InvalidOperationException("The official download URL is missing or is not HTTPS.");
        if (!AllowedVendorHosts.Contains(uri.Host))
            throw new InvalidOperationException($"TSUGU refused an unapproved download host: {uri.Host}");
        var name = Uri.UnescapeDataString(Path.GetFileName(uri.AbsolutePath));
        if (name.Length == 0) throw new InvalidOperationException("The vendor download URL did not provide a file name.");
        var expectedHash = NormalizeSha256(spec.ExpectedSha256);
        return new(uri, SafeFileName(name), "latest", expectedHash, null);
    }
    private async Task<ResolvedDownload> ResolveOfficialDownloadPageAsync(SuggestedInstallSpec spec, CancellationToken token)
    {
        if (!Uri.TryCreate(spec.DirectUrl, UriKind.Absolute, out var pageUri) ||
            pageUri.Scheme != Uri.UriSchemeHttps ||
            !AllowedVendorHosts.Contains(pageUri.Host))
            throw new InvalidOperationException("The official vendor download page is missing or is not on an approved HTTPS host.");

        using var response = await Http.GetAsync(pageUri, HttpCompletionOption.ResponseContentRead, token).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var finalHost = response.RequestMessage?.RequestUri?.Host ?? "";
        if (!AllowedVendorHosts.Contains(finalHost))
            throw new InvalidOperationException("The official download page redirected to an untrusted host.");
        var html = await response.Content.ReadAsStringAsync(token).ConfigureAwait(false);
        if (spec.AssetPattern.Length == 0)
            throw new InvalidOperationException("No Windows asset selector is configured for this vendor page.");

        // A vendor page may link to the binary directly or expose its file name in the download button's data attributes.
        // Match only the configured vendor host; this avoids turning a general web page into an arbitrary download launcher.
        var assetHost = spec.AssetHost ?? "storage.googleapis.com";
        if (!AllowedVendorDownloadHosts.Contains(assetHost))
            throw new InvalidOperationException("The selected download host is not approved by TSUGU.");
        var selector = new Regex(spec.AssetPattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(2));
        var links = Regex.Matches(html, @"(?:href|data-href|data-url|data-download-url)\s*=\s*[""']([^""']+)[""']", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(2))
            .Select(match =>
            {
                var value = WebUtility.HtmlDecode(match.Groups[1].Value);
                if (!Uri.TryCreate(pageUri, value, out var target)) return (Uri: (Uri?)null, FileName: "");
                var contextStart = Math.Max(0, match.Index - 500);
                var contextLength = Math.Min(html.Length - contextStart, match.Length + 1200);
                var context = html.Substring(contextStart, contextLength);
                var assetMatch = selector.Match(Uri.UnescapeDataString(target.AbsoluteUri));
                if (!assetMatch.Success) assetMatch = selector.Match(WebUtility.HtmlDecode(context));
                if (!assetMatch.Success) return (Uri: (Uri?)null, FileName: "");
                var linkedName = Uri.UnescapeDataString(Path.GetFileName(target.AbsolutePath));
                var fileName = Regex.IsMatch(linkedName, @"\.(?:exe|msi|zip)$", RegexOptions.IgnoreCase)
                    ? linkedName : assetMatch.Value;
                return (Uri: target, FileName: fileName);
            })
            .Where(candidate => candidate.Uri is not null &&
                candidate.Uri.Scheme == Uri.UriSchemeHttps &&
                (candidate.Uri.Host.Equals(assetHost, StringComparison.OrdinalIgnoreCase) ||
                 assetHost.Equals("youngsam.net", StringComparison.OrdinalIgnoreCase) &&
                 candidate.Uri.Host.Equals("www.youngsam.net", StringComparison.OrdinalIgnoreCase)))
            .Select(candidate => (Uri: candidate.Uri!, candidate.FileName))
            .DistinctBy(candidate => candidate.Uri.AbsoluteUri, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (links.Length != 1)
            throw new InvalidOperationException(links.Length == 0
                ? "The official download page did not expose one matching Windows asset on the approved host. Nothing was downloaded."
                : "The official download page exposed multiple matching Windows assets. TSUGU will not guess which one to run.");

        var asset = links[0].Uri;
        var fileName = SafeFileName(links[0].FileName);
        var expectedHash = NormalizeSha256(spec.ExpectedSha256);
        if (spec.UsePageChecksum)
        {
            expectedHash = FindChecksumNearFile(html, fileName);
            if (expectedHash is null)
                throw new InvalidDataException($"The official page did not expose one unambiguous SHA-256 checksum for {fileName}. Nothing was launched.");
        }
        var version = Regex.Match(html, @"(?i)Download Google Antigravity.{0,400}?v([0-9]+\.[0-9]+\.[0-9]+)", RegexOptions.Singleline, TimeSpan.FromSeconds(2));
        if (!version.Success) version = Regex.Match(fileName, @"(?i)(?:v)?([0-9]+\.[0-9]+\.[0-9]+)");
        return new(asset, fileName, version.Success ? version.Groups[1].Value : "latest", expectedHash, null);
    }


    private async Task<ResolvedDownload> ResolveGitHubReleaseAsync(SuggestedInstallSpec spec, CancellationToken token)
    {
        if (spec.Repository is null || !Regex.IsMatch(spec.Repository, @"^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$"))
            throw new InvalidOperationException("The GitHub repository in the catalog is invalid.");
        if (spec.AssetPattern.Length == 0 || spec.AssetPattern.Length > 240)
            throw new InvalidOperationException("The release asset selector is missing.");

        var api = new Uri($"https://api.github.com/repos/{spec.Repository}/releases/latest");
        using var response = await Http.GetAsync(api, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false));
        var root = json.RootElement;
        var tag = root.TryGetProperty("tag_name", out var tagValue) ? tagValue.GetString() ?? "latest" : "latest";
        var body = root.TryGetProperty("body", out var bodyValue) ? bodyValue.GetString() ?? "" : "";
        var assets = root.GetProperty("assets").EnumerateArray().ToArray();
        var pattern = new Regex(spec.AssetPattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(2));
        var matches = assets.Where(asset =>
        {
            var name = asset.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
            return pattern.IsMatch(name);
        }).ToArray();

        if (matches.Length != 1)
            throw new InvalidOperationException(matches.Length == 0
                ? $"The latest release {tag} of {spec.Repository} does not contain the expected Windows installer ({spec.AssetPattern})."
                : $"The latest release of {spec.Repository} contains more than one matching installer. TSUGU will not guess which one to run.");

        var selected = matches[0];
        var fileName = selected.GetProperty("name").GetString()!;
        var downloadUrl = selected.GetProperty("browser_download_url").GetString()!;
        if (!Uri.TryCreate(downloadUrl, UriKind.Absolute, out var assetUri) ||
            assetUri.Scheme != Uri.UriSchemeHttps ||
            !assetUri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase) ||
            !assetUri.AbsolutePath.StartsWith($"/{spec.Repository}/releases/download/", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("GitHub returned an unexpected release asset URL.");

        string? digest = null;
        if (selected.TryGetProperty("digest", out var digestValue))
        {
            var value = digestValue.GetString();
            if (value is not null && value.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
                digest = value["sha256:".Length..].Trim();
        }

        string? checksumText = null;
        if (!string.IsNullOrWhiteSpace(spec.ChecksumAssetPattern))
        {
            var checksumRegex = new Regex(spec.ChecksumAssetPattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(2));
            var checksumAssets = assets.Where(asset =>
                checksumRegex.IsMatch(asset.GetProperty("name").GetString() ?? "")).ToArray();
            if (checksumAssets.Length != 1)
                throw new InvalidOperationException($"The latest release of {spec.Repository} did not publish the expected checksum file.");
            var checksumUrl = checksumAssets[0].GetProperty("browser_download_url").GetString()!;
            checksumText = await ReadGitHubAssetTextAsync(checksumUrl, spec.Repository, token).ConfigureAwait(false);
            digest = FindChecksum(checksumText, fileName) ?? digest;
            if (digest is null)
                throw new InvalidOperationException($"The checksum file in {spec.Repository} does not contain a SHA-256 entry for {fileName}.");
        }
        else if (spec.UseReleaseBodyChecksum)
        {
            var match = Regex.Match(body, @"(?im)SHA-256\s*:\s*([0-9a-f]{64})");
            if (match.Success) digest = match.Groups[1].Value;
        }

        return new(assetUri, fileName, tag, digest, checksumText);
    }

    internal static string? FindChecksumNearFile(string page, string fileName)
    {
        if (string.IsNullOrWhiteSpace(page) || string.IsNullOrWhiteSpace(fileName)) return null;

        var text = WebUtility.HtmlDecode(Regex.Replace(page, @"<[^>]+>", " "));
        var hashes = Regex.Matches(text, @"(?<![0-9a-fA-F])[0-9a-fA-F]{64}(?![0-9a-fA-F])")
            .Select(match => (Value: match.Value.ToLowerInvariant(), Index: match.Index))
            .ToArray();
        if (hashes.Length == 0) return null;

        var needle = Path.GetFileName(fileName);
        var positions = Regex.Matches(text, Regex.Escape(needle), RegexOptions.IgnoreCase)
            .Select(match => match.Index)
            .ToArray();
        var associated = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var position in positions)
        {
            var closest = hashes
                .Select(hash => (hash.Value, Distance: Math.Abs(hash.Index - position)))
                .Where(x => x.Distance <= 1800)
                .OrderBy(x => x.Distance)
                .FirstOrDefault();
            if (closest.Value is not null) associated.Add(closest.Value);
        }
        return associated.Count == 1 ? associated.Single() : null;
    }

    private static string? NormalizeSha256(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var hash = value.Trim();
        if (!Regex.IsMatch(hash, @"^[0-9a-fA-F]{64}$"))
            throw new InvalidDataException("The configured SHA-256 value is not a 64-character hexadecimal digest.");
        return hash.ToLowerInvariant();
    }

    private static readonly HashSet<string> AllowedVendorHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "dl.emsisoft.com",
        "devbuilds.s.kaspersky-labs.com",
        "antigravity.google",
        "youngsam.net",
        "www.youngsam.net",
        "anticheat-client.faceit-cdn.net"
    };

    private static readonly HashSet<string> AllowedVendorDownloadHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "dl.emsisoft.com",
        "devbuilds.s.kaspersky-labs.com",
        "storage.googleapis.com",
        "youngsam.net",
        "www.youngsam.net",
        "anticheat-client.faceit-cdn.net"
    };

    private static readonly HashSet<string> AllowedGitHubRedirectHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "github.com",
        "release-assets.githubusercontent.com",
        "objects.githubusercontent.com"
    };

    private static async Task<string> ReadGitHubAssetTextAsync(string url, string repository, CancellationToken token)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            !uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase) ||
            !uri.AbsolutePath.StartsWith($"/{repository}/releases/download/", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("GitHub returned an unexpected checksum URL.");
        using var response = await Http.GetAsync(uri, token).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var finalHost = response.RequestMessage?.RequestUri?.Host ?? "";
        if (!AllowedGitHubRedirectHosts.Contains(finalHost))
            throw new InvalidOperationException("The checksum download redirected to an untrusted host.");
        return await response.Content.ReadAsStringAsync(token).ConfigureAwait(false);
    }

    internal static string? FindChecksum(string checksumText, string fileName)
    {
        foreach (var line in checksumText.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var match = Regex.Match(line, @"^([0-9a-fA-F]{64})\s+\*?(.+?)\s*$");
            if (match.Success && Path.GetFileName(match.Groups[2].Value).Equals(fileName, StringComparison.OrdinalIgnoreCase))
                return match.Groups[1].Value.ToLowerInvariant();
            // A standalone .sha256 sidecar often contains only the digest.
            var only = Regex.Match(line, @"^([0-9a-fA-F]{64})$");
            if (only.Success && checksumText.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Length == 1)
                return only.Groups[1].Value.ToLowerInvariant();
        }
        return null;
    }

    private static async Task DownloadAsync(Uri uri, string destination, IProgress<CommandOutput>? progress, CancellationToken token)
    {
        using var response = await Http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var host = response.RequestMessage?.RequestUri?.Host ?? "";
        if (!AllowedGitHubRedirectHosts.Contains(host) && !AllowedVendorDownloadHosts.Contains(host))
            throw new InvalidOperationException($"The download redirected to an untrusted host ({host}).");
        var length = response.Content.Headers.ContentLength;
        if (length is > MaximumDownloadBytes)
            throw new InvalidOperationException("The download is larger than TSUGU's 1.5 GB safety limit.");
        await using var input = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
        await using var output = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None, 128 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        var buffer = new byte[128 * 1024];
        long received = 0;
        int read;
        var lastPercent = -1;
        while ((read = await input.ReadAsync(buffer, token).ConfigureAwait(false)) > 0)
        {
            received += read;
            if (received > MaximumDownloadBytes)
                throw new InvalidOperationException("The download exceeded TSUGU's 1.5 GB safety limit.");
            await output.WriteAsync(buffer.AsMemory(0, read), token).ConfigureAwait(false);
            if (length is > 0)
            {
                var percent = (int)Math.Clamp(received * 100L / length.Value, 0, 100);
                if (percent >= lastPercent + 5)
                {
                    lastPercent = percent;
                    Report(progress, $"Downloading {percent}%", transfer: true);
                }
            }
        }
        await output.FlushAsync(token).ConfigureAwait(false);
        if (length is > 0 && received != length.Value)
            throw new IOException("The download ended before all expected bytes arrived.");
        Report(progress, "Download complete.");
    }

    private async Task VerifyAsync(string file, SuggestedInstallSpec spec, ResolvedDownload resolved, IProgress<CommandOutput>? progress, CancellationToken token)
    {
        if (resolved.ExpectedHash is { Length: > 0 } expected)
        {
            var actual = await HashAsync(file, token).ConfigureAwait(false);
            if (!actual.Equals(expected, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("SHA-256 verification failed. The downloaded file was not launched.");
            Report(progress, "SHA-256 verified against the publisher's release checksum.");
        }

        var signature = await ReadAuthenticodeAsync(file, token).ConfigureAwait(false);
        if (signature.Status.Equals("Valid", StringComparison.OrdinalIgnoreCase))
        {
            if (spec.ExpectedSigner is { Length: > 0 } signer &&
                !signature.Subject.Contains(signer, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"The file is signed by an unexpected publisher ({signature.Subject}). It was not launched.");
            Report(progress, $"Authenticode signature valid: {signature.Subject}");
            return;
        }

        if (!signature.Status.Equals("NotSigned", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Windows reports an invalid or untrusted Authenticode signature ({signature.Status}). The file was not launched.");

        if (spec.ExpectedSigner is { Length: > 0 } expectedSigner)
            throw new InvalidDataException($"The download is not signed by the expected publisher ({expectedSigner}). The file was not launched.");

        if (resolved.ExpectedHash is not { Length: 64 })
            throw new InvalidDataException("The downloaded file is unsigned and the release did not provide a SHA-256 digest. TSUGU will not run an unverified installer.");
        Report(progress, "The file is not code-signed, but its SHA-256 digest matches the GitHub release metadata.");
    }

    private async Task<(string Status, string Subject)> ReadAuthenticodeAsync(string file, CancellationToken token)
    {
        if (!OperatingSystem.IsWindows()) return ("Unknown", "");
        var path = file.Replace("'", "''");
        var script = "$s=Get-AuthenticodeSignature -LiteralPath '" + path + "'; $subject=''; if($s.SignerCertificate){$subject=$s.SignerCertificate.Subject}; Write-Output ($s.Status.ToString() + '|' + $subject)";
        var command = new CommandSpec(locator.PowerShell,
            ["-NoLogo", "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "RemoteSigned", "-EncodedCommand",
             Convert.ToBase64String(Encoding.Unicode.GetBytes(script))],
            TimeSpan.FromSeconds(20));
        var result = await runner.RunAsync(command, token).ConfigureAwait(false);
        if (result.ExitCode != 0) return ("Unknown", "");
        var line = result.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).LastOrDefault() ?? "";
        var split = line.Split('|', 2);
        return (split[0], split.Length > 1 ? split[1] : "");
    }

    private async Task<PackageActionResult> RunInstallerAsync(SuggestedInstallSpec spec, string file, string version, IProgress<CommandOutput>? progress, CancellationToken token)
    {
        Report(progress, $"Launching {spec.Name} installer. Follow the installer and approve any Windows UAC prompt.");
        var result = await RunShellInstallerAsync(file, spec.Name, progress, token).ConfigureAwait(false);
        if (result.ExitCode is 1641 or 3010)
            return new(ActionOutcome.RestartRequired, $"{spec.Name} installed; restart required", $"Version: {version}\nExit code: {result.ExitCode}\n{CleanOutput(result)}");
        if (result.ExitCode != 0)
            return new(ActionOutcome.Failed, $"Could not install {spec.Name}", $"Version: {version}\nExit code: {result.ExitCode}\n{CleanOutput(result)}");
        return new(ActionOutcome.Completed, $"{spec.Name} installer finished", $"Version: {version}\n{CleanOutput(result)}");
    }

    /// <summary>ShellExecute lets Windows honour an installer's own requireAdministrator manifest and show the normal UAC prompt.</summary>
    private static async Task<CommandResult> RunShellInstallerAsync(string file, string name, IProgress<CommandOutput>? progress, CancellationToken token)
    {
        var info = new ProcessStartInfo(file)
        {
            UseShellExecute = true,
            WorkingDirectory = Path.GetDirectoryName(file) ?? Environment.SystemDirectory
        };
        using var process = new Process { StartInfo = info };
        try
        {
            if (!process.Start()) return new(-1, "", $"{name} installer could not be started.");
            await process.WaitForExitAsync(token).ConfigureAwait(false);
            return new(process.ExitCode, "", "");
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            Report(progress, "The Windows administrator prompt was declined. Nothing was installed.", warning: true);
            return new(-1, "", "Administrator permission was declined. Nothing was installed.");
        }
        catch (OperationCanceledException)
        {
            Report(progress, $"{name} installer is still running or was interrupted. Check its window before trying again.", warning: true);
            throw;
        }
    }

    private static PackageActionResult LaunchPortable(string file, string version, string message)
    {
        try
        {
            Process.Start(new ProcessStartInfo(file) { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(file)! });
            return new(ActionOutcome.Completed, message, $"Version: {version}\nDownloaded to: {file}");
        }
        catch (Exception ex) when (ex is Win32Exception or IOException or InvalidOperationException)
        { return new(ActionOutcome.Failed, "The portable app could not be launched", ex.Message); }
    }

    private async Task<PackageActionResult> InstallPortableExecutableAsync(SuggestedInstallSpec spec, string downloaded, string appFolder, string version, IProgress<CommandOutput>? progress, CancellationToken token)
    {
        var stable = Path.Combine(appFolder, SafeFileName(Path.GetFileName(downloaded)));
        if (!string.Equals(downloaded, stable, StringComparison.OrdinalIgnoreCase))
            File.Copy(downloaded, stable, overwrite: true);
        await CreateShortcutAsync(spec.Name, stable, progress, token).ConfigureAwait(false);
        return LaunchPortable(stable, version, $"{spec.Name} placed in TSUGU's user app folder and launched.");
    }

    private async Task<PackageActionResult> InstallFromArchiveAsync(SuggestedInstallSpec spec, string archive, string appFolder, string version, IProgress<CommandOutput>? progress, CancellationToken token)
    {
        var target = Path.Combine(appFolder, "Driver-" + SafeFileName(version));
        Directory.CreateDirectory(target);
        ZipFile.ExtractToDirectory(archive, target, overwriteFiles: true);
        var pattern = spec.InnerExecutablePattern ?? "installer.exe";
        var match = Directory.EnumerateFiles(target, pattern, SearchOption.AllDirectories).SingleOrDefault();
        if (match is null) throw new FileNotFoundException($"The archive did not contain the expected installer ({pattern}).");
        Report(progress, "Archive extracted. Starting the vendor's installer; a driver installation may request administrator permission.");
        var result = await RunShellInstallerAsync(match, spec.Name, progress, token).ConfigureAwait(false);
        if (result.ExitCode is 1641 or 3010)
            return new(ActionOutcome.RestartRequired, $"{spec.Name} installed; restart required", $"Version: {version}\n{CleanOutput(result)}");
        return result.ExitCode == 0
            ? new(ActionOutcome.Completed, $"{spec.Name} installer finished", $"Version: {version}\n{CleanOutput(result)}")
            : new(ActionOutcome.Failed, $"Could not install {spec.Name}", $"Exit code: {result.ExitCode}\n{CleanOutput(result)}");
    }

    private static PackageActionResult InstallPortableArchive(SuggestedInstallSpec spec, string archive, string appFolder, string version)
    {
        try
        {
            ZipFile.ExtractToDirectory(archive, appFolder, overwriteFiles: true);
            var pattern = spec.InnerExecutablePattern ?? "*.exe";
            var target = Directory.EnumerateFiles(appFolder, pattern, SearchOption.AllDirectories).FirstOrDefault()
                ?? throw new FileNotFoundException($"The archive did not contain {pattern}.");
            if (Path.GetExtension(target).Equals(".bat", StringComparison.OrdinalIgnoreCase))
            {
                Process.Start(new ProcessStartInfo(Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe")
                {
                    Arguments = "/c call \"" + target + "\"",
                    UseShellExecute = true,
                    WorkingDirectory = Path.GetDirectoryName(target)!
                });
            }
            else Process.Start(new ProcessStartInfo(target) { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(target)! });
            return new(ActionOutcome.Completed, $"{spec.Name} extracted and launched", $"Version: {version}\nInstalled folder: {appFolder}");
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or Win32Exception or InvalidOperationException)
        { return new(ActionOutcome.Failed, $"Could not prepare {spec.Name}", ex.Message); }
    }

    private static PackageActionResult PrepareManualArchive(SuggestedInstallSpec spec, string archive, string appFolder, string version)
    {
        try
        {
            ZipFile.ExtractToDirectory(archive, appFolder, overwriteFiles: true);
            Process.Start(new ProcessStartInfo("explorer.exe", "\"" + appFolder + "\"") { UseShellExecute = true });
            return new(ActionOutcome.Completed, $"{spec.Name} package prepared — manual steps remain",
                $"Version: {version}\nExtracted to: {appFolder}\n\n{spec.Name} is a patch for an existing installation, not a standalone installer. TSUGU has not overwritten any system audio files. Follow the upstream guide and back up the original files before applying it.\n{spec.ReadmeUrl}");
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or Win32Exception or InvalidOperationException)
        { return new(ActionOutcome.Failed, $"Could not prepare {spec.Name}", ex.Message); }
    }

    private async Task CreateShortcutAsync(string name, string target, IProgress<CommandOutput>? progress, CancellationToken token)
    {
        var startMenu = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
        var folder = Path.Combine(startMenu, "TSUGU");
        Directory.CreateDirectory(folder);
        var shortcut = Path.Combine(folder, SafeFileName(name) + ".lnk");
        var script = "$w=New-Object -ComObject WScript.Shell; $s=$w.CreateShortcut('" +
            shortcut.Replace("'", "''") + "'); $s.TargetPath='" + target.Replace("'", "''") +
            "'; $s.WorkingDirectory='" + Path.GetDirectoryName(target)!.Replace("'", "''") + "'; $s.Save()";
        var result = await runner.RunAsync(new CommandSpec(locator.PowerShell,
            ["-NoLogo", "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "RemoteSigned", "-EncodedCommand",
             Convert.ToBase64String(Encoding.Unicode.GetBytes(script))], TimeSpan.FromSeconds(20)), token).ConfigureAwait(false);
        if (result.ExitCode != 0)
            Report(progress, "The app was copied, but TSUGU could not create its Start menu shortcut.");
    }

    private static async Task<string> HashAsync(string path, CancellationToken token)
    {
        await using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 128 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        var hash = await SHA256.HashDataAsync(file, token).ConfigureAwait(false);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string SafeFileName(string name)
    {
        var cleaned = Path.GetFileName(name);
        if (cleaned.Length == 0 || cleaned.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || cleaned is "." or "..")
            throw new InvalidDataException("The upstream release supplied an invalid file name.");
        return cleaned;
    }

    private static string CleanOutput(CommandResult result) =>
        string.Join("\n", new[] { result.Output, result.Error }.Where(x => !string.IsNullOrWhiteSpace(x))).Trim();

    private static void Report(IProgress<CommandOutput>? progress, string text, bool transfer = false, bool warning = false)
    {
        try { progress?.Report(new(text, warning ? CommandOutputKind.Warning : transfer ? CommandOutputKind.Transfer : CommandOutputKind.Information)); }
        catch { }
    }
}
