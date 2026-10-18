using System.Collections.Generic;
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
        => Assert.Null(InstalledBrowsers.FindPlaywrightBuild(engine, Pins, [this.cache]));

    [Theory]
    [InlineData("chromium")]
    [InlineData("firefox")]
    [InlineData("webkit")]
    public void AnEngineWhoseDownloadFinished_IsReportedAsPlaywrightsOwnBuild(string engine)
    {
        this.Install(engine, "1234", complete: true);

        UiAvailableBrowser? found = InstalledBrowsers.FindPlaywrightBuild(engine, Pins, [this.cache]);

        Assert.NotNull(found);
        Assert.Equal(engine, found.Browser);
        Assert.Null(found.Channel);
    }

    [Fact]
    public void ADownloadThatNeverFinished_IsNotReported()
    {
        this.Install("firefox", "1234", complete: false);

        Assert.Null(InstalledBrowsers.FindPlaywrightBuild("firefox", Pins, [this.cache]));
    }

    [Fact]
    public void AnotherEnginesDownload_DoesNotCountForThisOne()
    {
        this.Install("chromium", "1234", complete: true);

        Assert.Null(InstalledBrowsers.FindPlaywrightBuild("webkit", Pins, [this.cache]));
    }

    [Fact]
    public void ACacheFolderThatDoesNotExist_IsNotReported()
        => Assert.Null(InstalledBrowsers.FindPlaywrightBuild("chromium", Pins, [Path.Combine(this.cache, "missing")]));

    [Fact]
    public void ABuildOfAnotherPlaywrightVersion_IsNotReported_BecauseTheLaunchWouldNotUseIt()
    {
        this.Install("chromium", "1223", complete: true);

        Assert.Null(InstalledBrowsers.FindPlaywrightBuild("chromium", Pins, [this.cache]));
    }

    [Fact]
    public void ChromiumsHeadlessShellAlone_IsReported_BecauseAHeadlessLaunchNeedsNothingElse()
    {
        this.Install("chromium_headless_shell", "1234", complete: true);

        Assert.NotNull(InstalledBrowsers.FindPlaywrightBuild("chromium", Pins, [this.cache]));
    }

    [Fact]
    public void WithoutThePins_NothingIsReported_RatherThanAGuess()
        => Assert.Null(InstalledBrowsers.FindPlaywrightBuild("chromium", null, [this.cache]));

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> Pins = new Dictionary<string, IReadOnlyList<string>>
    {
        ["chromium"] = ["1234"],
        ["chromium-headless-shell"] = ["1234"],
        ["firefox"] = ["1234"],
        ["webkit"] = ["1234"],
    };

    private void Install(string engine, string build, bool complete)
    {
        string folder = Directory.CreateDirectory(Path.Combine(this.cache, $"{engine}-{build}")).FullName;

        if (complete)
        {
            File.WriteAllText(Path.Combine(folder, "INSTALLATION_COMPLETE"), string.Empty);
        }
    }
}
