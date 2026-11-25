using TidyWindow.Next.Core;
using Xunit;

namespace TidyWindow.Next.Tests;

public sealed class SuggestedInstallTests
{
    [Fact]
    public void ReleaseChecksumIsMatchedToTheExactAssetName()
    {
        const string hash = "0801dace7e2be2cd7e3cc05dddcf49584bc4d0563921f832509a16778f11a078";
        var sums = $"{hash}  Solaar-Windows.zip\n{new string('a', 64)}  another.zip";
        Assert.Equal(hash, SuggestedInstallService.FindChecksum(sums, "Solaar-Windows.zip"));
        Assert.Null(SuggestedInstallService.FindChecksum(sums, "Different.zip"));
    }

    [Fact]
    public void SingleHashSidecarCanBeParsed()
    {
        const string hash = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
        Assert.Equal(hash, SuggestedInstallService.FindChecksum(hash, "FlClashX-windows-amd64-setup.exe"));
    }

    [Fact]
    public void OfficialDownloadPageChecksumIsAssociatedWithTheExactFile()
    {
        const string exeHash = "8f41e32df54fd3e14cf3caFC4CBC318070847FE6C4989EFF04D30E3BE7FD4CA0";
        const string zipHash = "07da694915baae640635aa508ab6c940fcec9a59ce192e758b8f56ffef759c5e";
        const string page = """
            <h2>PinTop-11.0.0-x64.exe</h2>
            <pre>
            # PinTop-11.0.0-x64.exe
            # 8F41E32DF54FD3E14CF3CAFC4CBC318070847FE6C4989EFF04D30E3BE7FD4CA0
            #
            # PinTop-11.0.0-x64.zip
            # 07DA694915BAAE640635AA508AB6C940FCEC9A59CE192E758B8F56FFEF759C5E
            </pre>
            """;

        Assert.Equal(exeHash.ToLowerInvariant(), SuggestedInstallService.FindChecksumNearFile(page, "PinTop-11.0.0-x64.exe"));
        Assert.Equal(zipHash, SuggestedInstallService.FindChecksumNearFile(page, "PinTop-11.0.0-x64.zip"));
        Assert.Null(SuggestedInstallService.FindChecksumNearFile(page, "Unknown.exe"));
    }

    [Fact]
    public void ConfiguredSha256MustBeAHexDigest()
    {
        var spec = new SuggestedInstallSpec("faceit", "FACEIT", SuggestedInstallSource.DirectDownload,
            SuggestedInstallPayload.Installer, DirectUrl: "https://anticheat-client.faceit-cdn.net/FACEITInstaller_64.exe",
            ExpectedSha256: new string('x', 64));
        Assert.Equal(new string('x', 64), spec.ExpectedSha256);
    }

    [Fact]
    public void MicrosoftStorePackagesKeepTheirWinGetSource()
    {
        var store = new Package(ManagerKind.Winget, "9PLM9XGG6VKS", "Codex", "Latest", "msstore");
        var regular = new Package(ManagerKind.Winget, "Discord.Discord", "Discord", "Latest", "WinGet");
        Assert.Contains("--source msstore", PackageService.DisplayInstallCommand(store));
        Assert.Contains("--source winget", PackageService.DisplayInstallCommand(regular));
    }

    [Fact]
    public void SuggestedOperationsHaveTheirOwnQueueIdentity()
    {
        var spec = new SuggestedInstallSpec("fulgur", "Fulgur", SuggestedInstallSource.GitHubRelease,
            SuggestedInstallPayload.Installer, Repository: "fulgur-app/fulgur", AssetPattern: "^Fulgur-.*-setup\\.exe$");
        var operation = PackageOperation.InstallSuggested(spec);
        Assert.Equal("suggested:FULGUR", operation.ResourceKey);
        Assert.Equal(PackageOperationKind.Install, operation.Kind);
        Assert.Null(operation.InstallPackage);
    }

    [Fact]
    public async Task SuggestedInstallIsDispatchedThroughTheQueueAndReleasesTheOperationGate()
    {
        var runner = new StubRunner();
        var locator = new StubLocator();
        var packages = new PackageService(runner, locator);
        var installer = new RecordingSuggestedInstaller();
        var executor = new PackageOperationExecutor(packages, null!, locator, suggestedInstalls: installer);
        var spec = new SuggestedInstallSpec("fulgur", "Fulgur", SuggestedInstallSource.GitHubRelease,
            SuggestedInstallPayload.Installer, Repository: "fulgur-app/fulgur", AssetPattern: "^Fulgur-.*-setup\\.exe$");

        var result = await executor.ExecuteAsync(PackageOperation.InstallSuggested(spec), CancellationToken.None);

        Assert.Equal(ActionOutcome.Completed, result.Outcome);
        Assert.Same(spec, installer.Received);
        Assert.True(packages.OperationGate.Wait(0));
        packages.OperationGate.Release();
    }

    private sealed class RecordingSuggestedInstaller : ISuggestedInstallService
    {
        public SuggestedInstallSpec? Received { get; private set; }

        public Task<PackageActionResult> InstallAsync(SuggestedInstallSpec spec,
            IProgress<CommandOutput>? progress = null, CancellationToken token = default)
        {
            Received = spec;
            return Task.FromResult(new PackageActionResult(ActionOutcome.Completed, "Installed", "Test installer."));
        }
    }

    private sealed class StubRunner : ICommandRunner
    {
        public Task<CommandResult> RunAsync(CommandSpec command, CancellationToken cancellationToken = default) =>
            Task.FromResult(new CommandResult(0, string.Empty, string.Empty));
    }

    private sealed class StubLocator : IExecutableLocator
    {
        public string? Find(ManagerKind manager) => null;
        public string PowerShell => "powershell.exe";
    }
}
