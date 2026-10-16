using System;
using System.IO;
using TestFramework.UI.Browser.Runtime;

namespace TestFramework.UI.Browser.Tests.Runtime;

/// <summary>
/// A browser is reported as available only when Playwright has actually finished installing it here -
/// for every engine, not only the one this machine happens to use.
/// </summary>
public sealed class InstalledBrowsersTests : IDisposable
{
    private readonly string cache = Path.Combine(Path.GetTempPath(), "ui-browser-cache-" + Guid.NewGuid().ToString("N"));

    public InstalledBrowsersTests() => Directory.CreateDirectory(this.cache);

    public void Dispose() => Directory.Delete(this.cache, recursive: true);

    [Theory]
    [InlineData("chromium")]
    [InlineData("firefox")]
    [InlineData("webkit")]
    public void AnEngineWithNothingDownloaded_IsNotReported(string engine)
        => Assert.Null(InstalledBrowsers.FindPlaywrightBuild(engine, [this.cache]));

    [Theory]
    [InlineData("chromium")]
    [InlineData("firefox")]
    [InlineData("webkit")]
    public void AnEngineWhoseDownloadFinished_IsReportedAsPlaywrightsOwnBuild(string engine)
    {
        this.Install(engine, "1234", complete: true);

        UiAvailableBrowser? found = InstalledBrowsers.FindPlaywrightBuild(engine, [this.cache]);

        Assert.NotNull(found);
        Assert.Equal(engine, found.Browser);
        Assert.Null(found.Channel);
    }

    [Fact]
    public void ADownloadThatNeverFinished_IsNotReported()
    {
        this.Install("firefox", "1234", complete: false);

        Assert.Null(InstalledBrowsers.FindPlaywrightBuild("firefox", [this.cache]));
    }

    [Fact]
    public void AnotherEnginesDownload_DoesNotCountForThisOne()
    {
        this.Install("chromium", "1234", complete: true);

        Assert.Null(InstalledBrowsers.FindPlaywrightBuild("webkit", [this.cache]));
    }

    [Fact]
    public void ACacheFolderThatDoesNotExist_IsNotReported()
        => Assert.Null(InstalledBrowsers.FindPlaywrightBuild("chromium", [Path.Combine(this.cache, "missing")]));

    private void Install(string engine, string build, bool complete)
    {
        string folder = Directory.CreateDirectory(Path.Combine(this.cache, $"{engine}-{build}")).FullName;

        if (complete)
        {
            File.WriteAllText(Path.Combine(folder, "INSTALLATION_COMPLETE"), string.Empty);
        }
    }
}
