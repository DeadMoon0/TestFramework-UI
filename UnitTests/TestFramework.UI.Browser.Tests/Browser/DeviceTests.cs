using System;
using System.Threading.Tasks;
using TestFramework.Core.Timelines;
using TestFramework.UI.Browser.Exceptions;
using TestFramework.UI.Browser.Identifier;
using TestFramework.UI.Browser.Reading;
using TestFramework.UI.Browser.Targeting;
using TestFramework.UI.Browser.Tests.Shared;
using Xunit.Abstractions;

namespace TestFramework.UI.Browser.Tests.Browser;

/// <summary>
/// The browser environment as configuration: the same test, a different device, zero code change.
/// </summary>
/// <remarks>
/// The responsive page collapses its navigation behind a Menu button below 768 pixels. 'shop' runs at
/// Desktop 1080p and 'shop-mobile' inherits everything from it except the device - so the difference
/// between the two runs is one line of configuration, which is the whole claim.
/// </remarks>
[Collection(SampleAppCollection.Name)]
public class DeviceTests(SampleAppFixture fixture, ITestOutputHelper output)
{
    /// <summary>
    /// The one timeline both devices run - the application is the only parameter.
    /// </summary>
    private static Timeline CatalogueTimeline(WebAppIdentifier app) => Timeline.Create()
        .Trigger(BrowserExt.Session(app)
            .Navigate("/responsive")
            .Expect("Catalogue")
            .Read(Value.Count(Target.Button("Menu")), "menus"))
            .Name("look")
        .Build();

    [BrowserFact]
    public async Task TheSameTimelineRunsAsDesktopAndAsPhoneByConfigAlone()
    {
        // A loop rather than a [Theory]: Core's test-identity resolver currently crashes under xunit's
        // dynamically-emitted theory invokers (task filed against TestFramework-Core). The claim is
        // unchanged - the timeline below is built by one method, and the device is the only difference.
        foreach ((string app, int perceivableMenus) in new[] { ("shop", 0), ("shop-mobile", 1) })
        {
            TimelineRun run = await CatalogueTimeline(app).SetupRun(fixture.Services(), output).RunAsync();

            run.EnsureRanToCompletion();

            // Below the breakpoint the Menu button exists; above it, display:none takes it out of the
            // accessibility tree, so the desktop run cannot perceive it any more than a person could.
            run.Variable<int>("menus").Should().Be(perceivableMenus);
        }
    }

    [BrowserFact]
    public async Task APhoneOpensTheMenuADesktopNeverSees()
    {
        // The phone's way to the catalogue: through the Menu button.
        Timeline phone = Timeline.Create()
            .Trigger(BrowserExt.Session("shop-mobile")
                .Navigate("/responsive")
                .Click("Menu")
                .Expect("Menu open")
                .Click(Target.Link("Tools")))
                .Name("via-menu")
            .Build();

        TimelineRun phoneRun = await phone.SetupRun(fixture.Services(), output).RunAsync();
        phoneRun.EnsureRanToCompletion();

        // The desktop's way: the links are simply there.
        Timeline desktop = Timeline.Create()
            .Trigger(BrowserExt.Session("shop")
                .Navigate("/responsive")
                .Click(Target.Link("Tools")))
                .Name("directly")
            .Build();

        TimelineRun desktopRun = await desktop.SetupRun(fixture.Services(), output).RunAsync();
        desktopRun.EnsureRanToCompletion();
    }

    [BrowserFact]
    public async Task AnUnknownDeviceNamesTheOnesThatExist()
    {
        Timeline timeline = Timeline.Create()
            .Trigger(BrowserExt.Session("shop").Navigate("/responsive")).Name("open")
            .Build();

        TimelineRun run = await timeline.SetupRun(fixture.Services(device: "Atari 2600"), output).RunAsync();

        // A raw ArgumentException, deliberately: the session never came to be, so there is no flow
        // context and no page to photograph - wrapping it would only add words around the real answer.
        ArgumentException failure = Assert.IsType<ArgumentException>(run.Step("open").LastResult.Exception);

        Assert.Contains("Unknown device 'Atari 2600'", failure.Message, StringComparison.Ordinal);
        Assert.Contains("Desktop 1080p", failure.Message, StringComparison.Ordinal);
        Assert.Contains("iPhone 14", failure.Message, StringComparison.Ordinal);
    }
}
