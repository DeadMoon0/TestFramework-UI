using System;
using System.Linq;
using System.Threading.Tasks;
using TestFramework.Core.Timelines;
using TestFramework.UI.Browser.Events;
using TestFramework.UI.Browser.Scripting;
using TestFramework.UI.Browser.Targeting;
using TestFramework.UI.Browser.Tests.Shared;
using Xunit.Abstractions;

namespace TestFramework.UI.Browser.Tests.Browser;

/// <summary>
/// The wait events, against the page whose whole purpose is to be late.
/// </summary>
/// <remarks>
/// The delayed page loads after 900 ms, offers its action after 1.8 s, and drops its banner after
/// 2.4 s - so every wait here is a real wait, and a test that merely checked once would fail.
/// </remarks>
[Collection(SampleAppCollection.Name)]
public class EventTests(SampleAppFixture fixture, ITestOutputHelper output)
{
    [BrowserFact]
    public async Task AWaitStandsBetweenStepsAndTheTimelineReadsThatWay()
    {
        Timeline timeline = Timeline.Create()
            .Trigger(BrowserExt.Session("shop").Navigate("/delayed")).Name("open")
            .WaitForEvent(BrowserExt.Events.ElementVisible("shop", Target.Button("Export all")))
                .WithTimeOut(TimeSpan.FromSeconds(10)).Name("actions-ready")
            .Trigger(BrowserExt.Session("shop").Click("Export all").Expect("Export started")).Name("export")
            .Build();

        TimelineRun run = await timeline.SetupRun(fixture.Services(), output).RunAsync();

        run.EnsureRanToCompletion();

        // The wait's result says what it cost: the button appears after 1.8s, so the wait really waited
        // and really polled.
        UiWaitResultContext waited = Assert.IsType<UiWaitResultContext>(run.Step("actions-ready").LastResult.Result);
        Assert.True(waited.WaitedForMs > 500, $"waited {waited.WaitedForMs}ms - the page offers the button only after 1.8s");
        Assert.True(waited.Polls > 1, $"{waited.Polls} poll(s) - a real wait polls more than once");

        // And it is part of the session story, between the steps it stood between.
        run.UiTrace("shop").Should().Match(
            static entries => entries.Any(entry => entry.Action == "WaitVisible"),
            "a recorded wait");
    }

    [BrowserFact]
    public async Task ADisappearanceIsWaitedForNotAssumed()
    {
        Timeline timeline = Timeline.Create()
            .Trigger(BrowserExt.Session("shop").Navigate("/delayed").Expect("Loading orders...")).Name("open")
            .WaitForEvent(BrowserExt.Events.ElementHidden("shop", Target.TestId("banner")))
                .WithTimeOut(TimeSpan.FromSeconds(10)).Name("banner-gone")
            .Build();

        TimelineRun run = await timeline.SetupRun(fixture.Services(), output).RunAsync();

        run.EnsureRanToCompletion();

        // The banner drops at 2.4s; a single early check would have seen it still there.
        UiWaitResultContext waited = Assert.IsType<UiWaitResultContext>(run.Step("banner-gone").LastResult.Result);
        Assert.True(waited.Polls > 1, $"{waited.Polls} poll(s) - the banner was still there on the first look");
    }

    [BrowserFact]
    public async Task WhatWasNeverThereIsAlreadyGone()
    {
        // The wait asserts a state, not a transition - so it must not fail when the thing disappeared
        // faster than the first poll, or never existed at all.
        Timeline timeline = Timeline.Create()
            .Trigger(BrowserExt.Session("shop").Navigate("/products").Expect("Anvil")).Name("open")
            .WaitForEvent(BrowserExt.Events.ElementHidden("shop", Target.Text("No Such Banner")))
                .WithTimeOut(TimeSpan.FromSeconds(5)).Name("nothing-there")
            .Build();

        TimelineRun run = await timeline.SetupRun(fixture.Services(), output).RunAsync();

        run.EnsureRanToCompletion();
    }

    [BrowserFact]
    public async Task ANavigationCausedByThePageItselfCanBeWaitedOn()
    {
        Timeline timeline = Timeline.Create()
            .Trigger(BrowserExt.Session("shop")
                .Navigate("/checkout")
                .Fill("Email", "showroom@example.test")
                .Check("Accept terms")
                .Click("Place order")).Name("submit")
            .WaitForEvent(BrowserExt.Events.UrlMatches("shop", @"confirmation\?total=\d+").AsRegex())
                .WithTimeOut(TimeSpan.FromSeconds(10)).Name("landed")
            .Trigger(BrowserExt.Session("shop").Expect("Thank you")).Name("confirmed")
            .Build();

        TimelineRun run = await timeline.SetupRun(fixture.Services(), output).RunAsync();

        run.EnsureRanToCompletion();
        run.UiUrl("shop").Should().Contain("/confirmation");
    }

    [BrowserFact]
    public async Task ApplicationStateNoElementShowsCanBeWaitedOn()
    {
        // The orders arrive over HTTP after the page renders, so the first polls genuinely see less.
        Timeline timeline = Timeline.Create()
            .Trigger(BrowserExt.Session("shop").Navigate("/orders")).Name("open")
            .WaitForEvent(BrowserExt.Events.ScriptIsTrue(
                    "shop",
                    Js.Inline("() => document.querySelectorAll('app-order-row').length >= 2").Named("orders arrived"),
                    pollDelay: TimeSpan.FromMilliseconds(100)))
                .WithTimeOut(TimeSpan.FromSeconds(10)).Name("data-loaded")
            .Build();

        TimelineRun run = await timeline.SetupRun(fixture.Services(), output).RunAsync();

        run.EnsureRanToCompletion();

        // A script wait is still a script - the audit sees it like any other use of the hatch.
        run.UiScripts("shop").Should().HaveCount(1);
    }

    [BrowserFact]
    public async Task AScriptWaitRefusesAQuestionWithoutABooleanAnswer()
    {
        Timeline timeline = Timeline.Create()
            .Trigger(BrowserExt.Session("shop").Navigate("/orders")).Name("open")
            .WaitForEvent(BrowserExt.Events.ScriptIsTrue(
                    "shop",
                    Js.Inline("() => document.querySelectorAll('app-order-row').length").Named("counts instead of asking")))
                .WithTimeOut(TimeSpan.FromSeconds(5)).Name("mistyped")
            .Build();

        TimelineRun run = await timeline.SetupRun(fixture.Services(), output).RunAsync();

        Exception failure = Assert.IsType<InvalidOperationException>(run.Step("mistyped").LastResult.Exception);

        // Immediately and with the fix - not as a timeout that blames the page.
        Assert.Contains("needs a boolean", failure.Message, StringComparison.Ordinal);
        Assert.Contains("=== true", failure.Message, StringComparison.Ordinal);
    }

    [BrowserFact]
    public async Task AWaitThatTimesOutSaysWhatItWatchedAndWhereThePageWas()
    {
        Timeline timeline = Timeline.Create()
            .Trigger(BrowserExt.Session("shop").Navigate("/products").Expect("Anvil")).Name("open")
            .WaitForEvent(BrowserExt.Events.TextAppears("shop", "This Never Appears"))
                .WithTimeOut(TimeSpan.FromSeconds(3)).Name("hopeless")
            .Build();

        TimelineRun run = await timeline.SetupRun(fixture.Services(), output).RunAsync();

        // The event's own message, not the runner's generic one - which is the entire point of the event
        // giving up slightly before its step timeout.
        TimeoutException failure = Assert.IsType<TimeoutException>(run.Step("hopeless").LastResult.Exception);

        Assert.Contains("'This Never Appears'", failure.Message, StringComparison.Ordinal);
        Assert.Contains("never happened", failure.Message, StringComparison.Ordinal);
        Assert.Contains("/app/products", failure.Message, StringComparison.Ordinal);
        Assert.Contains("recorded with the run", failure.Message, StringComparison.Ordinal);

        output.WriteLine(failure.Message);
    }

    [BrowserFact]
    public async Task WhatAComponentSaysOnItsAttributesCanBeWaitedOn()
    {
        // The sync-state element reports on attributes, not in visible words: a heartbeat that ticks
        // every 400 ms, and a data-state that flips to 'ready' at 900 ms. Three waits, none of which a
        // text wait could express - and the banner's disappearance in the words a person would use.
        Timeline timeline = Timeline.Create()
            .Trigger(BrowserExt.Session("shop").Navigate("/delayed")).Name("open")
            .WaitForEvent(BrowserExt.Events.AttributeChanged(
                    "shop",
                    Target.TestId("sync-state"),
                    "data-heartbeat",
                    pollDelay: TimeSpan.FromMilliseconds(100)))
                .WithTimeOut(TimeSpan.FromSeconds(10)).Name("alive")
            .WaitForEvent(BrowserExt.Events.AttributeEquals(
                    "shop",
                    Target.TestId("sync-state"),
                    "data-state",
                    "ready",
                    pollDelay: TimeSpan.FromMilliseconds(100)))
                .WithTimeOut(TimeSpan.FromSeconds(10)).Name("synced")
            .WaitForEvent(BrowserExt.Events.TextDisappears(
                    "shop",
                    "Syncing with the warehouse",
                    pollDelay: TimeSpan.FromMilliseconds(100)))
                .WithTimeOut(TimeSpan.FromSeconds(10)).Name("banner-gone")
            .Build();

        TimelineRun run = await timeline.SetupRun(fixture.Services(), output).RunAsync();

        run.EnsureRanToCompletion();

        // Every wait is part of the session story, under its own action name.
        run.UiTrace("shop").Should().Match(
            static entries => entries.Any(entry => entry.Action == "WaitAttributeChange")
                && entries.Any(entry => entry.Action == "WaitAttribute")
                && entries.Any(entry => entry.Action == "WaitHidden"),
            "the attribute waits and the disappearance in the trace");
    }

    [BrowserFact]
    public async Task AnAttributeWaitThatTimesOutReportsTheValueItSaw()
    {
        // data-state settles on 'ready'; a wait for 'done' must end by naming what the attribute
        // actually read, because that IS the diagnosis.
        Timeline timeline = Timeline.Create()
            .Trigger(BrowserExt.Session("shop").Navigate("/delayed").Expect("3 orders loaded")).Name("open")
            .WaitForEvent(BrowserExt.Events.AttributeEquals(
                    "shop",
                    Target.TestId("sync-state"),
                    "data-state",
                    "done",
                    pollDelay: TimeSpan.FromMilliseconds(100)))
                .WithTimeOut(TimeSpan.FromSeconds(3)).Name("never")
            .Build();

        TimelineRun run = await timeline.SetupRun(fixture.Services(), output).RunAsync();

        TimeoutException failure = Assert.IsType<TimeoutException>(run.Step("never").LastResult.Exception);

        Assert.Contains("'data-state'", failure.Message, StringComparison.Ordinal);
        Assert.Contains("the attribute read 'ready'", failure.Message, StringComparison.Ordinal);

        output.WriteLine(failure.Message);
    }

    [BrowserFact]
    public async Task AGrowingListCanBeWaitedOnByItsCount()
    {
        // The orders arrive over HTTP, the third only after "Load more" - so the first count is reached
        // by loading and the second by acting, and neither wait names an element, only how many.
        Timeline timeline = Timeline.Create()
            .Trigger(BrowserExt.Session("shop").Navigate("/orders")).Name("open")
            .WaitForEvent(BrowserExt.Events.CountAtLeast(
                    "shop",
                    Target.Css("app-order-row"),
                    2,
                    pollDelay: TimeSpan.FromMilliseconds(100)))
                .WithTimeOut(TimeSpan.FromSeconds(10)).Name("loaded")
            .Trigger(BrowserExt.Session("shop").Click("Load more")).Name("more")
            .WaitForEvent(BrowserExt.Events.CountIs(
                    "shop",
                    Target.Css("app-order-row"),
                    3,
                    pollDelay: TimeSpan.FromMilliseconds(100)))
                .WithTimeOut(TimeSpan.FromSeconds(10)).Name("all-three")
            .Build();

        TimelineRun run = await timeline.SetupRun(fixture.Services(), output).RunAsync();

        run.EnsureRanToCompletion();

        run.UiTrace("shop").Should().Match(
            static entries => entries.Count(entry => entry.Action == "WaitCount") == 2,
            "both count waits in the trace");
    }
}
