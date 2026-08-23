using System;
using System.Linq;
using System.Threading.Tasks;
using TestFramework.Core.Timelines;
using TestFramework.UI.Browser.Events;
using TestFramework.UI.Browser.Structure;
using TestFramework.UI.Browser.Targeting;
using TestFramework.UI.Browser.Tests.Shared;
using TestFramework.UI.Structure;
using Xunit.Abstractions;

namespace TestFramework.UI.Browser.Tests.Browser;

/// <summary>
/// Waiting on what a part of the page is built like.
/// </summary>
/// <remarks>
/// The orders page earns its keep again: its list exists only after data arrives over HTTP, and its
/// third row only after a person asks for more - one shape produced by loading, one by acting.
/// </remarks>
[Collection(SampleAppCollection.Name)]
public class ShapeEventTests(SampleAppFixture fixture, ITestOutputHelper output)
{
    /// <summary>
    /// What the loaded order list is built like - declared once, shared by waits and checks alike.
    /// </summary>
    private static readonly WebElementStructure LoadedOrderList = WebElementStructure
        .OneElement("app-order-list")
            .WithAttribute("role", "table")
            .Containing(x =>
            {
                x.AtLeast(2, "app-order-row").WithAttribute("data-order-id");
                x.OneElement("p").WithText(Cell.Contains("€"));
            });

    [BrowserFact]
    public async Task TheShapeLoadingProducesCanBeWaitedOn()
    {
        // No Expect anywhere: the wait itself carries the run from "navigated" to "the list is built",
        // while the section does not even exist on the first polls.
        Timeline timeline = Timeline.Create()
            .Trigger(BrowserExt.Session("shop").Navigate("/orders")).Name("open")
            .WaitForEvent(BrowserExt.Events.StructureMatches(
                    "shop",
                    Target.Section("Orders All"),
                    LoadedOrderList,
                    pollDelay: TimeSpan.FromMilliseconds(100)))
                .WithTimeOut(TimeSpan.FromSeconds(10)).Name("list-built")
            .Build();

        TimelineRun run = await timeline.SetupRun(fixture.Services(), output).RunAsync();

        run.EnsureRanToCompletion();

        // The wait is part of the session story, like every other wait.
        run.UiTrace("shop").Should().Match(
            static entries => entries.Any(entry => entry.Action == "WaitStructure"),
            "a recorded structure wait");
    }

    [BrowserFact]
    public async Task TheRowsAnActionAddsCanBeWaitedOn()
    {
        // The third order hides behind "Load more"; the table wait states the whole final row set, and
        // is only satisfied once the click has produced it.
        Timeline timeline = Timeline.Create()
            .Trigger(BrowserExt.Session("shop")
                .Navigate("/orders")
                .Expect("Anvil")
                .Click("Load more")).Name("more")
            .WaitForEvent(BrowserExt.Events.TableMatches(
                    "shop",
                    Target.Section("Orders All"),
                    ExpectedTable.WithHeader("Order", "Product")
                        .Row("A-1001", "Anvil")
                        .Row("A-1002", "Rope")
                        .Row("A-1003", "Crate")))
                .WithTimeOut(TimeSpan.FromSeconds(10)).Name("all-rows")
            .Build();

        TimelineRun run = await timeline.SetupRun(fixture.Services(), output).RunAsync();

        run.EnsureRanToCompletion();

        UiWaitResultContext waited = Assert.IsType<UiWaitResultContext>(run.Step("all-rows").LastResult.Result);
        Assert.Contains("expected 3 row(s)", waited.Waited, StringComparison.Ordinal);
    }

    [BrowserFact]
    public async Task AShapeWaitThatTimesOutReportsItsLastLookDifferenceByDifference()
    {
        Timeline timeline = Timeline.Create()
            .Trigger(BrowserExt.Session("shop").Navigate("/orders").Expect("Anvil")).Name("open")
            .WaitForEvent(BrowserExt.Events.TableMatches(
                    "shop",
                    Target.Section("Orders All"),
                    ExpectedTable.WithHeader("Order", "Qty")
                        .Row("A-1001", "7")
                        .AllowExtraRows()))
                .WithTimeOut(TimeSpan.FromSeconds(3)).Name("hopeless")
            .Build();

        TimelineRun run = await timeline.SetupRun(fixture.Services(), output).RunAsync();

        TimeoutException failure = Assert.IsType<TimeoutException>(run.Step("hopeless").LastResult.Exception);

        // The last look, difference by difference - a wait that never matched ends as readably as a
        // comparison that failed, including the expectation the page would have satisfied.
        Assert.Contains("row 1, column 'Qty'", failure.Message, StringComparison.Ordinal);
        Assert.Contains("expected is '7', found '1'", failure.Message, StringComparison.Ordinal);
        Assert.Contains("ExpectedTable.WithHeader", failure.Message, StringComparison.Ordinal);
        Assert.Contains("Screenshot, markup and session story", failure.Message, StringComparison.Ordinal);

        output.WriteLine(failure.Message);
    }
}
