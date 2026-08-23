using System;
using System.Linq;
using System.Threading.Tasks;
using TestFramework.Core.Timelines;
using TestFramework.UI.Browser.Steps;
using TestFramework.UI.Browser.Targeting;
using TestFramework.UI.Browser.Tests.Shared;
using Xunit.Abstractions;

namespace TestFramework.UI.Browser.Tests.Browser;

/// <summary>
/// The pointer and keyboard verbs beyond clicking, against the page where they ARE the behaviour.
/// </summary>
/// <remarks>
/// The interactions page reacts to the input itself: a card that exists only while hovered, a row that
/// renames on double-click, options on right-click, a drop zone, and an input that counts keydowns -
/// which is what separates real keystrokes from a value set in one motion.
/// </remarks>
[Collection(SampleAppCollection.Name)]
public class InteractionTests(SampleAppFixture fixture, ITestOutputHelper output)
{
    [BrowserFact]
    public async Task WhatThePointerRevealsGoesAwayWhenItLeaves()
    {
        // Hover is only half a behaviour; the card disappearing on mouseleave is the other half, and
        // MouseAway is how a test says "the pointer left".
        Timeline timeline = Timeline.Create()
            .Trigger(BrowserExt.Session("shop")
                .Navigate("/interactions")
                .Hover("Delivery details")
                .Expect("Delivered from the Bergen warehouse in 2 to 4 days.")
                .MouseAway()
                .ExpectNot(Target.TestId("hover-card")))
                .Name("hover-cycle")
            .Build();

        TimelineRun run = await timeline.SetupRun(fixture.Services(), output).RunAsync();

        run.EnsureRanToCompletion();

        run.UiTrace("shop").Should().Match(
            static entries => entries.Any(entry => entry.Action == "MouseAway"),
            "the pointer's departure as a trace entry of its own");
    }

    [BrowserFact]
    public async Task TheOtherMouseButtonsAreVerbsOfTheirOwn()
    {
        Timeline timeline = Timeline.Create()
            .Trigger(BrowserExt.Session("shop")
                .Navigate("/interactions")
                .DoubleClick(Target.Text("Quarterly report.pdf"))
                .Expect("Rename started")
                .RightClick(Target.Text("Archive entry"))
                .Expect("Archive options open"))
                .Name("mouse-buttons")
            .Build();

        TimelineRun run = await timeline.SetupRun(fixture.Services(), output).RunAsync();

        run.EnsureRanToCompletion();

        // First-party means auditable like everything else - and never a script.
        run.UiTrace("shop").Should().Match(
            static entries => entries.Any(entry => entry.Action == "DoubleClick")
                && entries.Any(entry => entry.Action == "RightClick"),
            "both buttons in the trace");
        run.UiScripts("shop").Should().HaveNoItems();
    }

    [BrowserFact]
    public async Task ADragIsPressedMovedAndReleasedLikeAHand()
    {
        // The page's drop zone only reacts to the HTML5 drag contract, so this passes only if the drag
        // was real pointer work, not a teleported element.
        Timeline timeline = Timeline.Create()
            .Trigger(BrowserExt.Session("shop")
                .Navigate("/interactions")
                .DragTo(Target.Text("Drag the anvil"), Target.TestId("drop-zone"))
                .Expect("The anvil landed"))
                .Name("drag")
            .Build();

        TimelineRun run = await timeline.SetupRun(fixture.Services(), output).RunAsync();

        run.EnsureRanToCompletion();

        run.UiTrace("shop").Should().Match(
            static entries => entries.Any(entry =>
                entry.Action == "Drag" && entry.Detail is { } detail && detail.Contains("drop-zone", StringComparison.Ordinal)),
            "the drag names its destination");
    }

    [BrowserFact]
    public async Task TypingIsKeystrokesWhereFillingIsAValue()
    {
        // The page counts keydowns. Fill sets the value in one motion and moves the counter not at all;
        // Type presses five keys and the counter says so - which is the entire difference, proven by the
        // page rather than declared by the docs. The chord then lands on the same control, focused first.
        Timeline timeline = Timeline.Create()
            .Trigger(BrowserExt.Session("shop")
                .Navigate("/interactions")
                .Fill("Search", "rope")
                .Expect("0 keys pressed")
                .Type("Search", "anvil")
                .Expect("5 keys pressed")
                .Press(Target.Field("Search"), "Control+Enter")
                .Expect("Search-everywhere opened"))
                .Name("keyboard")
            .Build();

        TimelineRun run = await timeline.SetupRun(fixture.Services(), output).RunAsync();

        run.EnsureRanToCompletion();

        run.UiTrace("shop").Should().Match(
            static entries => entries.Any(entry => entry.Action == "Type")
                && entries.Any(entry => entry.Action == "Press"),
            "typing and the targeted chord in the trace");
    }

    [Fact]
    public void TheNewVerbsRecordWhatTheTestWrote()
    {
        // Spec accumulation is browser-free: what a verb appends is what the runner will perform, and
        // what a reader of the trace will be told.
        UiBrowserFlow flow = BrowserExt.Session("shop")
            .DragTo(Target.Text("Drag the anvil"), Target.TestId("drop-zone"))
            .Press(Target.Field("Search"), "Control+Enter")
            .Type("Search", "anvil")
            .MouseAway();

        Assert.Collection(
            flow.ActionsForTesting,
            static action =>
            {
                Assert.Equal(UiActionKind.Drag, action.Kind);
                Assert.Equal("Drag text 'Drag the anvil' to test id 'drop-zone'", action.Describe());
            },
            static action => Assert.Equal(UiActionKind.Press, action.Kind),
            static action => Assert.Equal(UiActionKind.Type, action.Kind),
            static action => Assert.Equal(UiActionKind.MouseAway, action.Kind));
    }
}
