using System;
using System.Linq;
using TestFramework.Core.Exceptions;
using TestFramework.Core.Steps;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Timelines;
using TestFramework.Core.Variables;
using TestFramework.UI.Browser.Events;
using TestFramework.UI.Browser.Targeting;
using TestFramework.UI.Session;

namespace TestFramework.UI.Browser.Tests.Events;

/// <summary>
/// The wait events as declarations: their IO and their freezing.
/// </summary>
/// <remarks>
/// Three cases used to live here, pinning the margin by which a wait cancelled itself before its own
/// timeout - a sixth, clamped between 200 ms and a second, tuned twice against loaded CI runners. There is
/// nothing left to pin: the engine now cancels at the deadline and waits a grace window in which a step's
/// own account is what surfaces, so a wait no longer has to finish first to be heard. What replaced them
/// is Core's own deadline and grace-window suites.
/// </remarks>
public class UiEventModelTests
{
    [Fact]
    public void AWaitWritesTheSameSessionVariableTheFlowsDo()
    {
        // Which is what keeps a wait ordered between the steps of its application's session.
        UiElementVisibleEvent waitEvent = BrowserExt.Events.ElementVisible("shop", Target.Text("Done"));

        StepIOContract contract = new StepIOContract();
        waitEvent.DeclareIO(contract);

        StepIOEntry output = Assert.Single(contract.Outputs);
        Assert.Equal(UiSessionVariable.For("shop"), output.Key);
        Assert.Equal(typeof(UiSessionPicture), output.DeclaredType);
    }

    [Fact]
    public void AVariableFedPatternIsDeclaredAsAnInput()
    {
        UiUrlMatchesEvent waitEvent = BrowserExt.Events.UrlMatches("shop", Var.Ref<string>("expectedLanding"));

        StepIOContract contract = new StepIOContract();
        waitEvent.DeclareIO(contract);

        Assert.Contains(contract.Inputs, static entry => entry.Key == "expectedLanding");
    }

    [Fact]
    public void ADialCannotBeTurnedOnAFrozenEvent()
    {
        // The runner freezes the instance it executes; a dial turned after that must refuse rather than
        // change a wait that is already in flight. (The object a test holds stays workable until then -
        // each run executes its own clone.)
        UiUrlMatchesEvent waitEvent = BrowserExt.Events.UrlMatches("shop", "confirmation");

        waitEvent.Freeze();

        Assert.Throws<FrameworkStateException>(() => waitEvent.AsRegex());
    }

    [Fact]
    public void TheNewerWaitsWriteTheSameSessionVariableToo()
    {
        // Attribute, change and count waits joined later; the ordering contract must hold for them the
        // same way it does for the original waits.
        UiEvent<UiAttributeEqualsEvent> equals = BrowserExt.Events.AttributeEquals("shop", Target.TestId("sync-state"), "data-state", "ready");
        UiEvent<UiAttributeChangedEvent> changed = BrowserExt.Events.AttributeChanged("shop", Target.TestId("sync-state"), "data-heartbeat");
        UiEvent<UiElementCountEvent> count = BrowserExt.Events.CountIs("shop", Target.Css("app-order-row"), 3);

        foreach (StepGeneric waitEvent in new StepGeneric[] { equals, changed, count })
        {
            StepIOContract contract = new StepIOContract();
            waitEvent.DeclareIO(contract);

            Assert.Contains(contract.Outputs, static entry => entry.Key == UiSessionVariable.For("shop"));
        }
    }

    [Fact]
    public void AWaitDescribesItselfInTheWordsTheTestUsed()
    {
        Assert.Contains(
            "attribute 'data-state' of test id 'sync-state' on 'shop' reads a value that is 'ready'",
            BrowserExt.Events.AttributeEquals("shop", Target.TestId("sync-state"), "data-state", "ready").Description,
            StringComparison.Ordinal);

        Assert.Contains(
            "numbers exactly 3",
            BrowserExt.Events.CountIs("shop", Target.Css("app-order-row"), 3).Description,
            StringComparison.Ordinal);

        Assert.Contains(
            "numbers at least 2",
            BrowserExt.Events.CountAtLeast("shop", Target.Css("app-order-row"), 2).Description,
            StringComparison.Ordinal);
    }

    [Fact]
    public void AWaitRefusesWhatItCouldNeverAnswer()
    {
        // A negative count and a nameless attribute are authoring mistakes, refused where they are
        // written rather than discovered against a browser.
        Assert.ThrowsAny<ArgumentException>(() => BrowserExt.Events.CountIs("shop", Target.Css("app-order-row"), -1));
        Assert.ThrowsAny<ArgumentException>(() => BrowserExt.Events.AttributeEquals("shop", Target.TestId("x"), " ", "ready"));
        Assert.ThrowsAny<ArgumentException>(() => BrowserExt.Events.AttributeChanged("shop", Target.TestId("x"), ""));
        Assert.ThrowsAny<ArgumentException>(() => BrowserExt.Events.TextDisappears("shop", " "));
    }
}
