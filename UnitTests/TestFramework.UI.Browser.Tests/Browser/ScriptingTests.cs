using System;
using System.Threading.Tasks;
using TestFramework.Core.Timelines;
using TestFramework.Core.Variables;
using TestFramework.UI.Browser.Exceptions;
using TestFramework.UI.Browser.Reading;
using TestFramework.UI.Browser.Scripting;
using TestFramework.UI.Browser.Targeting;
using TestFramework.UI.Browser.Tests.Shared;
using Xunit.Abstractions;

namespace TestFramework.UI.Browser.Tests.Browser;

/// <summary>
/// The escape hatch: JavaScript in the page, named, argument-fed, typed on the way out, and audited.
/// </summary>
[Collection(SampleAppCollection.Name)]
public class ScriptingTests(SampleAppFixture fixture, ITestOutputHelper output)
{
    [BrowserFact]
    public async Task AScriptCanSeedWhatNoVerbReaches()
    {
        // The honest use of the hatch: putting the application into a state before the test starts -
        // here a pre-filled cart, fed from a timeline variable and visible to the page after a reload.
        Timeline timeline = Timeline.Create()
            .SetVariable("seededCart", Var.Const("""[{"product":"Rope","quantity":2,"price":9}]"""))
            .Trigger(BrowserExt.Session("shop")
                .Navigate("/products")
                .Execute(Js.Inline("args => localStorage.setItem('sample-app.cart', args.cart)")
                    .Named("seed the cart")
                    .WithArgument("cart", Var.Ref<string>("seededCart")))
                .Navigate("/products")
                .Expect("2 items in cart"))
                .Name("seeded")
            .Build();

        TimelineRun run = await timeline.SetupRun(fixture.Services(), output).RunAsync();

        run.EnsureRanToCompletion();

        // The hatch was used once, and the session story says so - tolerance that hides itself is how a
        // suite drifts.
        run.UiScripts("shop").Should().HaveCount(1);
        run.UiScripts("shop").Select(static scripts => scripts[0].Detail).Should().Contain("seed the cart");
    }

    [BrowserFact]
    public async Task AnEvaluationComesBackInTheTypeItWasAskedFor()
    {
        Timeline timeline = Timeline.Create()
            .Trigger(BrowserExt.Session("shop")
                .Navigate("/orders")
                .Expect("Anvil")
                .Evaluate<string>(Js.Inline("() => document.title").Named("the title"), "title")
                .Evaluate<int>(
                    Target.Section("Orders All"),
                    Js.Inline("el => el.querySelectorAll('[role=row]').length").Named("count the rows"),
                    "rows"))
                .Name("measured")
            .Build();

        TimelineRun run = await timeline.SetupRun(fixture.Services(), output).RunAsync();

        run.EnsureRanToCompletion();

        run.Variable<string>("title").Should().Be("SampleApp");

        // The element script ran on the element the SAME resolution found - a header row plus two orders.
        run.Variable<int>("rows").Should().Be(3);
    }

    [BrowserFact]
    public async Task APageErrorNamesTheScriptAndKeepsThePagesOwnWords()
    {
        Timeline timeline = Timeline.Create()
            .Trigger(BrowserExt.Session("shop")
                .Navigate("/products")
                .Execute(Js.Inline("() => { throw new Error('boom from the page') }").Named("the exploder")))
                .Name("explodes")
            .Build();

        TimelineRun run = await timeline.SetupRun(fixture.Services(), output).RunAsync();

        UiActionFailedException failure = Assert.IsType<UiActionFailedException>(run.Step("explodes").LastResult.Exception);

        // Both halves of the diagnosis: which script, and what the page itself said.
        Assert.Contains("the exploder", failure.Message, StringComparison.Ordinal);
        Assert.Contains("boom from the page", failure.Message, StringComparison.Ordinal);

        output.WriteLine(failure.Message);
    }

    [BrowserFact]
    public async Task AScriptThatReturnsNothingCannotFillAVariable()
    {
        // The braced-body trap, caught and explained: '() => { ... }' evaluates to undefined.
        Timeline timeline = Timeline.Create()
            .Trigger(BrowserExt.Session("shop")
                .Navigate("/products")
                .Evaluate<int>(Js.Inline("() => { const x = 1; }").Named("forgets to return"), "x"))
                .Name("empty-handed")
            .Build();

        TimelineRun run = await timeline.SetupRun(fixture.Services(), output).RunAsync();

        UiActionFailedException failure = Assert.IsType<UiActionFailedException>(run.Step("empty-handed").LastResult.Exception);

        Assert.Contains("returned nothing", failure.Message, StringComparison.Ordinal);
        Assert.Contains("without a return returns undefined", failure.Message, StringComparison.Ordinal);
    }

    [BrowserFact]
    public async Task AResultThatIsNotTheAskedTypeSaysWhatCameBackInstead()
    {
        Timeline timeline = Timeline.Create()
            .Trigger(BrowserExt.Session("shop")
                .Navigate("/products")
                .Evaluate<int>(Js.Inline("() => 'not a number'").Named("mistyped"), "x"))
                .Name("mistyped")
            .Build();

        TimelineRun run = await timeline.SetupRun(fixture.Services(), output).RunAsync();

        UiActionFailedException failure = Assert.IsType<UiActionFailedException>(run.Step("mistyped").LastResult.Exception);

        // The raw JSON that came back, quotes and all - what the page said, not a paraphrase.
        Assert.Contains("\"not a number\"", failure.Message, StringComparison.Ordinal);
        Assert.Contains("Int32", failure.Message, StringComparison.Ordinal);
    }

    [BrowserFact]
    public async Task ASuiteWithoutScriptsCanProveIt()
    {
        Timeline timeline = Timeline.Create()
            .Trigger(BrowserExt.Session("shop")
                .Navigate("/products")
                .Click("Add Anvil to cart")
                .Expect("1 item in cart"))
                .Name("plain")
            .Build();

        TimelineRun run = await timeline.SetupRun(fixture.Services(), output).RunAsync();

        run.EnsureRanToCompletion();

        // The audit in the direction that matters most: this test spoke only in what a person could do.
        run.UiScripts("shop").Should().HaveNoItems();
    }
}
