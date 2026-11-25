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
    public void SuggestedOperationsHaveTheirOwnQueueIdentity()
    {
        var spec = new SuggestedInstallSpec("fulgur", "Fulgur", SuggestedInstallSource.GitHubRelease,
            SuggestedInstallPayload.Installer, Repository: "fulgur-app/fulgur", AssetPattern: "^Fulgur-.*-setup\\.exe$");
        var operation = PackageOperation.InstallSuggested(spec);
        Assert.Equal("suggested:FULGUR", operation.ResourceKey);
        Assert.Equal(PackageOperationKind.Install, operation.Kind);
        Assert.Null(operation.InstallPackage);
    }
}
