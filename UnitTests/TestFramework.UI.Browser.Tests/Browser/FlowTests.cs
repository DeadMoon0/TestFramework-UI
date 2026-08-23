using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TestFramework.Core.Timelines;
using TestFramework.UI.Browser.Targeting;
using TestFramework.UI.Browser.Tests.Shared;
using Xunit;
using Xunit.Abstractions;

namespace TestFramework.UI.Browser.Tests.Browser;

/// <summary>
/// What a browser step does when everything works: the everyday flow, and waiting for a page that is not
/// ready yet.
/// </summary>
[Collection(SampleAppCollection.Name)]
public class FlowTests(SampleAppFixture fixture, ITestOutputHelper output)
{
    [BrowserFact]
    public async Task AWholeCheckoutReadsAsWhatAPersonDoes()
    {
        Timeline timeline = Timeline.Create()
            .Trigger(BrowserExt.Session("shop")
                .Navigate("/products")
                .Click("Add Anvil to cart")
                .Expect("1 item in cart"))
                .Name("add-to-cart")
            .Trigger(BrowserExt.Session("shop")
                // The page offers two ways to Checkout - the navigation and a call to action - so the
                // test says which one a person would use, rather than letting the run pick.
                .Click(Target.Link("Checkout").InSection("Main"))
                .Fill("Email", "showroom@example.test")
                .Fill("Street", "Teststr. 1")
                .Fill("City", "Bremen")
                .Select("Shipping", "express")
                .Check("Accept terms")
                .Click("Place order")
                .Expect("Thank you")
                .Read(Target.TestId("order-number"), "orderNo")
                .Screenshot("confirmation"))
                .Name("checkout")
            .Build();

        TimelineRun run = await timeline.SetupRun(fixture.Services(), output).RunAsync();

        run.EnsureRanToCompletion();

        // The order number was never in the test - it was read off the page and is now an ordinary
        // variable, which any later step could use as its own input.
        run.Variable<string>("orderNo").Should()
            .Exist()
            .Match(static value => value is not null && Regex.IsMatch(value, @"^A-\d{4}$"), "an order number like 'A-1234'");

        run.UiUrl("shop").Should().Contain("/confirmation");
        run.UiScreenshot("shop", "confirmation").Should().NotBeNull();

        // Nothing here was found by guesswork: every control was addressed by what it is and what it is
        // called, and the page agreed.
        run.UiLooseMatches("shop").Should().HaveNoItems();
    }

    [BrowserFact]
    public async Task WaitingForAPageThatIsNotReadyNeedsNothingFromTheTest()
    {
        // The page loads its list after a moment, then enables a button after another. There is not a
        // single duration in this test.
        Timeline timeline = Timeline.Create()
            .Trigger(BrowserExt.Session("shop")
                .Navigate("/delayed")
                .Expect("3 orders loaded")
                .Click("Export all")
                .Expect("Export started"))
                .Name("export")
            .Build();

        TimelineRun run = await timeline.SetupRun(fixture.Services(), output).RunAsync();

        run.EnsureRanToCompletion();
        run.Step("export").Should().HaveCompleted();
    }

    [BrowserFact]
    public async Task AnAbsenceExpectationWaitsForTheThingToGoAway()
    {
        // The banner is there when the page loads and leaves on its own after a couple of seconds. A check
        // that ran once would pass or fail depending on how fast the machine is; this one has to wait.
        Timeline timeline = Timeline.Create()
            .Trigger(BrowserExt.Session("shop")
                .Navigate("/delayed")
                .Expect(Target.TestId("banner"))
                .ExpectNot(Target.TestId("banner")))
                .Name("banner")
            .Build();

        TimelineRun run = await timeline.SetupRun(fixture.Services(), output).RunAsync();

        run.EnsureRanToCompletion();
    }

    [BrowserFact]
    public async Task TheSessionCarriesTheWholeStoryToTheAssertions()
    {
        Timeline timeline = Timeline.Create()
            .Trigger(BrowserExt.Session("shop").Navigate("/products").Click("Add Anvil to cart")).Name("first")
            .Trigger(BrowserExt.Session("shop").Click("Add Rope to cart").Expect("2 items in cart")).Name("second")
            .Build();

        TimelineRun run = await timeline.SetupRun(fixture.Services(), output).RunAsync();

        run.EnsureRanToCompletion();

        // Two steps, one session: the second step found the page where the first left it - it never
        // navigated - and the whole sequence reads afterwards as one story rather than two disconnected
        // results.
        run.UiTrace("shop").Should().HaveCount(4);
        run.UiTrace("shop", "first").Should().HaveCount(2);
        run.UiTrace("shop", "second").Should().HaveCount(2);
    }
}
