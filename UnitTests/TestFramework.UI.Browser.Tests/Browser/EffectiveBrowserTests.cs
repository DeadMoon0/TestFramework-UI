using System;
using System.Linq;
using System.Threading.Tasks;
using TestFramework.Core.Runner;
using TestFramework.Core.Timelines;
using TestFramework.UI.Browser.Tests.Shared;
using Xunit;
using Xunit.Abstractions;

namespace TestFramework.UI.Browser.Tests.Browser;

/// <summary>
/// A run that passed can say which browser proved it.
/// </summary>
/// <remarks>
/// The gate finds a browser rather than demanding one, which is right and means the browser can differ
/// between two machines running the same suite. Without this the two runs look identical and neither names
/// what it drove, so a green result cannot be compared with another green result.
/// </remarks>
[Collection(SampleAppCollection.Name)]
public class EffectiveBrowserTests(SampleAppFixture fixture, ITestOutputHelper output)
{
    [BrowserFact]
    public async Task AFinishedRunNamesTheBrowserThatDroveIt()
    {
        Timeline timeline = Timeline.Create()
            .Trigger(BrowserExt.Session("shop").Navigate("/orders")).Name("open")
            .Build();

        TimelineRun run = await timeline.SetupRun(fixture.Services(), output).RunAsync();

        run.EnsureRanToCompletion();

        EffectiveSetting browser = Assert.Single(
            run.EffectiveSettings.Snapshot(),
            setting => setting.Name == "shop:Browser");

        Assert.Equal("TestFramework.UI.Browser", browser.Source);
        Assert.False(string.IsNullOrWhiteSpace(browser.Value), "the run recorded an empty browser name");

        // Whether the window was shown is the other half of "what proved this", and it defaults.
        Assert.Contains(run.EffectiveSettings.Snapshot(), setting => setting.Name == "shop:Headless");

        // And how much evidence was kept, so a run with no pictures says whether that was the policy.
        EffectiveSetting widgets = Assert.Single(
            run.EffectiveSettings.Snapshot(),
            setting => setting.Name == "shop:WidgetCapture");

        Assert.Equal("OnFailure", widgets.Value);

        output.WriteLine(string.Join(Environment.NewLine, run.EffectiveSettings.Snapshot()));
    }

    [BrowserFact]
    public async Task TheRecordIsClosedOnceTheRunIsDone()
    {
        Timeline timeline = Timeline.Create()
            .Trigger(BrowserExt.Session("shop").Navigate("/orders")).Name("open")
            .Build();

        TimelineRun run = await timeline.SetupRun(fixture.Services(), output).RunAsync();

        run.EnsureRanToCompletion();

        Assert.Throws<TestFramework.Core.Exceptions.FrameworkStateException>(
            () => run.EffectiveSettings.Record("someone", "shop:Browser", "firefox"));
    }
}
