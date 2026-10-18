using System;
using System.Collections.Generic;
using System.IO;
using TestFramework.UI.Browser.Runtime;

namespace TestFramework.UI.Browser.Tests.Runtime;

/// <summary>
/// Whether the exact browser build a launch looks for is on the machine, told from Playwright's own pins and folders.
/// </summary>
public sealed class PlaywrightBuildsTests : IDisposable
{
    private readonly string cache = Path.Combine(Path.GetTempPath(), "ui-builds-" + Guid.NewGuid().ToString("N"));

    public PlaywrightBuildsTests() => Directory.CreateDirectory(this.cache);

    public void Dispose() => Directory.Delete(this.cache, recursive: true);

    [Theory]
    [InlineData("chromium", true, "chromium_headless_shell-1234")]
    [InlineData("chromium", false, "chromium-1234")]
    [InlineData("firefox", true, "firefox-1538")]
    public void AMissingBuild_IsNamedTheWayPlaywrightNamesItsFolder(string engine, bool headless, string folder)
    {
        UiBuildCheck check = PlaywrightBuilds.Check(engine, headless, Pins(), [this.cache]);

        Assert.True(check.IsKnownMissing);
        Assert.Equal(folder, check.Needed);
    }

    [Fact]
    public void AFinishedBuildOfTheRightNumber_IsInstalled()
    {
        this.Install("chromium_headless_shell-1234", complete: true);

        Assert.True(PlaywrightBuilds.Check("chromium", headless: true, Pins(), [this.cache]).IsInstalled);
    }

    [Fact]
    public void AnUnfinishedDownload_IsMissing()
    {
        this.Install("chromium_headless_shell-1234", complete: false);

        Assert.True(PlaywrightBuilds.Check("chromium", headless: true, Pins(), [this.cache]).IsKnownMissing);
    }

    [Fact]
    public void AnOlderBuild_IsMissing_AndSaidToBeThere()
    {
        // A pipeline cache restored from before a Playwright upgrade: finished, and the wrong number.
        this.Install("chromium_headless_shell-1223", complete: true);

        UiBuildCheck check = PlaywrightBuilds.Check("chromium", headless: true, Pins(), [this.cache]);

        Assert.True(check.IsKnownMissing);
        Assert.Equal(["chromium_headless_shell-1223"], check.Found);
    }

    [Fact]
    public void APlatformOverride_CountsAsTheBuild()
    {
        // WebKit pins older builds for some systems; whichever this machine needed is the one it has.
        this.Install("webkit-2092", complete: true);

        Assert.True(PlaywrightBuilds.Check("webkit", headless: true, Pins(), [this.cache]).IsInstalled);
    }

    [Fact]
    public void WithoutPins_ItCannotBeTold()
        => Assert.False(PlaywrightBuilds.Check("chromium", headless: true, null, [this.cache]).IsKnown);

    [Fact]
    public void ThePinsArePlaywrightsOwnFile()
    {
        string path = Path.Combine(this.cache, "browsers.json");
        File.WriteAllText(path, """
            { "browsers": [
                { "name": "chromium", "revision": "1234" },
                { "name": "webkit", "revision": "2336", "revisionOverrides": { "ubuntu20.04-x64": "2092" } } ] }
            """);

        IReadOnlyDictionary<string, IReadOnlyList<string>>? pins = PlaywrightBuilds.ReadPins(path);

        Assert.NotNull(pins);
        Assert.Equal(["1234"], pins["chromium"]);
        Assert.Equal(["2336", "2092"], pins["webkit"]);
    }

    [Fact]
    public void AnUnreadablePinsFile_MeansUnknown_NotAFailure()
    {
        string path = Path.Combine(this.cache, "browsers.json");
        File.WriteAllText(path, "not json");

        Assert.Null(PlaywrightBuilds.ReadPins(path));
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> Pins() => new Dictionary<string, IReadOnlyList<string>>
    {
        ["chromium"] = ["1234"],
        ["chromium-headless-shell"] = ["1234"],
        ["firefox"] = ["1538"],
        ["webkit"] = ["2336", "2092"],
    };

    private void Install(string folder, bool complete)
    {
        string path = Directory.CreateDirectory(Path.Combine(this.cache, folder)).FullName;
        if (complete)
        {
            File.WriteAllText(Path.Combine(path, PlaywrightBuilds.InstallationCompleteMarker), string.Empty);
        }
    }
}
