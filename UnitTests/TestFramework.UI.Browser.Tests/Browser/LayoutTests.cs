using System;
using System.Threading.Tasks;
using TestFramework.Core.Timelines;
using TestFramework.UI.Browser.Exceptions;
using TestFramework.UI.Browser.Layouting;
using TestFramework.UI.Browser.Reading;
using TestFramework.UI.Browser.Targeting;
using TestFramework.UI.Browser.Tests.Shared;
using Xunit.Abstractions;

namespace TestFramework.UI.Browser.Tests.Browser;

/// <summary>
/// Layout as relations between the things a person sees, and the raw style read underneath.
/// </summary>
[Collection(SampleAppCollection.Name)]
public class LayoutTests(SampleAppFixture fixture, ITestOutputHelper output)
{
    /// <summary>
    /// What the checkout form's layout means, stated once - none of it mentions a coordinate, so none of
    /// it cares what machine renders the page.
    /// </summary>
    private static readonly ExpectedLayout CheckoutLayout = ExpectedLayout
        .Above(Target.Field("Email"), Target.Button("Place order"))
        .AndAbove(Target.Field("Email"), Target.Checkbox("Accept terms"))
        .AndNotOverlapping(Target.Field("Email"), Target.Button("Place order"))
        .AndInViewport(Target.Field("Email"))
        .AndNoHorizontalScroll();

    [BrowserFact]
    public async Task ALayoutIsCheckedAsRelationsNotCoordinates()
    {
        Timeline timeline = Timeline.Create()
            .Trigger(BrowserExt.Session("shop").Navigate("/checkout").Expect(Target.Role("heading", "Checkout"))).Name("open")
            .Trigger(BrowserExt.Page("shop").CheckLayout(CheckoutLayout)).Name("layout")
            .Build();

        TimelineRun run = await timeline.SetupRun(fixture.Services(), output).RunAsync();

        run.EnsureRanToCompletion();
        run.UiDifferences("layout").Should().HaveNoItems();
    }

    [BrowserFact]
    public async Task TheSameRelationsHoldOnAPhone()
    {
        // The point of relations: the phone renders everything narrower and taller, and not one line of
        // the expectation changes.
        Timeline timeline = Timeline.Create()
            .Trigger(BrowserExt.Session("shop-mobile").Navigate("/checkout").Expect(Target.Role("heading", "Checkout"))).Name("open")
            .Trigger(BrowserExt.Page("shop-mobile").CheckLayout(ExpectedLayout
                .Above(Target.Field("Email"), Target.Button("Place order"))
                .AndNoHorizontalScroll())).Name("layout")
            .Build();

        TimelineRun run = await timeline.SetupRun(fixture.Services(), output).RunAsync();

        run.EnsureRanToCompletion();
    }

    [BrowserFact]
    public async Task AViolatedRelationNamesBothActualRectangles()
    {
        // The reversed claim: the button above the field it follows. The failure must hand the reader the
        // page's answer - both rectangles - rather than sending them to the browser to look.
        Timeline timeline = Timeline.Create()
            .Trigger(BrowserExt.Session("shop").Navigate("/checkout").Expect(Target.Role("heading", "Checkout"))).Name("open")
            .Trigger(BrowserExt.Page("shop").CheckLayout(ExpectedLayout
                .Above(Target.Button("Place order"), Target.Field("Email")))).Name("upside-down")
            .Build();

        TimelineRun run = await timeline.SetupRun(fixture.Services(), output).RunAsync();

        UiLayoutMismatchException failure = Assert.IsType<UiLayoutMismatchException>(
            run.Step("upside-down").LastResult.Exception);

        Assert.Contains("expected button 'Place order' above field 'Email'", failure.Message, StringComparison.Ordinal);
        Assert.Contains("button 'Place order' is at (", failure.Message, StringComparison.Ordinal);
        Assert.Contains("field 'Email' is at (", failure.Message, StringComparison.Ordinal);
        Assert.Contains("retried until the page settled", failure.Message, StringComparison.Ordinal);

        output.WriteLine(failure.Message);
    }

    [BrowserFact]
    public async Task AMissingElementReadsAsAFactNotACrash()
    {
        Timeline timeline = Timeline.Create()
            .Trigger(BrowserExt.Session("shop").Navigate("/checkout").Expect(Target.Role("heading", "Checkout"))).Name("open")
            .Trigger(BrowserExt.Page("shop").CheckLayout(ExpectedLayout
                .InViewport(Target.Button("No Such Button"))))
                .WithTimeOut(TimeSpan.FromSeconds(8)).Name("nobody-home")
            .Build();

        TimelineRun run = await timeline.SetupRun(fixture.Services(), output).RunAsync();

        UiLayoutMismatchException failure = Assert.IsType<UiLayoutMismatchException>(
            run.Step("nobody-home").LastResult.Exception);

        Assert.Contains("button 'No Such Button' is not on the page", failure.Message, StringComparison.Ordinal);
    }

    [BrowserFact]
    public async Task TheGeometryOfAScopeCanBeCapturedForDriftDetection()
    {
        Timeline timeline = Timeline.Create()
            .Trigger(BrowserExt.Session("shop").Navigate("/orders").Expect("Anvil")).Name("open")
            .Trigger(BrowserExt.Page("shop").CaptureLayout(Target.Section("Orders All"), "orderGeometry")).Name("capture")
            .Build();

        TimelineRun run = await timeline.SetupRun(fixture.Services(), output).RunAsync();

        run.EnsureRanToCompletion();

        // An ordinary variable, positions relative to the scope and snapped to the grid: the page moving
        // as a whole is not drift, and neither is a rounding pixel.
        run.Variable<string>("orderGeometry").Should().Exist().Contain("app-order-row");
        run.Variable<string>("orderGeometry").Should().Match(
            static value => value is not null && value.StartsWith("app-order-list (0, 0", StringComparison.Ordinal),
            "coordinates relative to the scope itself");

        output.WriteLine(run.Step("capture").UiCapture().Structure);
    }

    [BrowserFact]
    public async Task AComputedStyleCanBeReadWhenThePropertyIsTheRequirement()
    {
        Timeline timeline = Timeline.Create()
            .Trigger(BrowserExt.Session("shop")
                .Navigate("/checkout")
                .Expect(Target.Role("heading", "Checkout"))
                .Read(Value.Style(Target.Button("Place order"), "text-transform"), "transform"))
                .Name("read")
            .Build();

        TimelineRun run = await timeline.SetupRun(fixture.Services(), output).RunAsync();

        run.EnsureRanToCompletion();

        // The one style property this suite genuinely requires: nothing a test reads may be
        // text-transformed, because innerText is the RENDERED text and "PLACE ORDER" is not
        // "Place order". The read is the requirement's own enforcement.
        run.Variable<string>("transform").Should().Be("none");
    }

    [BrowserFact]
    public async Task AMistypedStylePropertyIsAMessageNotAnEmptyVariable()
    {
        Timeline timeline = Timeline.Create()
            .Trigger(BrowserExt.Session("shop")
                .Navigate("/checkout")
                .Expect(Target.Role("heading", "Checkout"))
                .Read(Value.Style(Target.Button("Place order"), "background-colr"), "x"))
                .Name("typo")
            .Build();

        TimelineRun run = await timeline.SetupRun(fixture.Services(), output).RunAsync();

        UiActionFailedException failure = Assert.IsType<UiActionFailedException>(run.Step("typo").LastResult.Exception);

        Assert.Contains("no value for 'background-colr'", failure.Message, StringComparison.Ordinal);
        Assert.Contains("'background-color'", failure.Message, StringComparison.Ordinal);
    }
}
