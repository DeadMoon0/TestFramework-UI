using System;
using System.Linq;
using TestFramework.Core.Exceptions;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Timelines;
using TestFramework.Core.Variables;
using TestFramework.UI.Browser.Events;
using TestFramework.UI.Browser.Targeting;
using TestFramework.UI.Session;

namespace TestFramework.UI.Browser.Tests.Events;

/// <summary>
/// The wait events as declarations: their deadline arithmetic, their IO, and their freezing.
/// </summary>
public class UiEventModelTests
{
    [Fact]
    public void TheOwnDeadlineIsASixthEarlyWithinItsClamp()
    {
        // A sixth of the timeout, never below 200 ms, never above a second - measured margins; smaller
        // ones were swallowed by loaded CI runners and the generic timeout message won.
        Assert.Equal(TimeSpan.FromSeconds(5) - TimeSpan.FromMilliseconds(833 + 1.0 / 3), UiEventDeadline.For(TimeSpan.FromSeconds(5)));
        Assert.Equal(TimeSpan.FromMilliseconds(800), UiEventDeadline.For(TimeSpan.FromSeconds(1)));
        Assert.Equal(TimeSpan.FromSeconds(59), UiEventDeadline.For(TimeSpan.FromSeconds(60)));
    }

    [Fact]
    public void ATimeoutTooShortForTheMarginKeepsAUsableSlice()
        => Assert.Equal(TimeSpan.FromMilliseconds(50), UiEventDeadline.For(TimeSpan.FromMilliseconds(100)));

    [Fact]
    public void AnUnboundedTimeoutNeedsNoOwnDeadline()
    {
        Assert.Equal(TimeSpan.Zero, UiEventDeadline.For(TimeSpan.Zero));
        Assert.Equal(TimeSpan.Zero, UiEventDeadline.For(TimeSpan.FromDays(2)));
    }

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
}
