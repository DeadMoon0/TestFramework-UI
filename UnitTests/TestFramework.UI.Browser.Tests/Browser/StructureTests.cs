using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using TestFramework.Core.Timelines;
using TestFramework.UI.Browser.Exceptions;
using TestFramework.UI.Browser.Structure;
using TestFramework.UI.Browser.Targeting;
using TestFramework.UI.Browser.Tests.Shared;
using TestFramework.UI.Structure;
using Xunit;
using Xunit.Abstractions;

namespace TestFramework.UI.Browser.Tests.Browser;

/// <summary>
/// Structure and table comparison against a real application built from components.
/// </summary>
/// <remarks>
/// The orders page is the honest test for this: its markup comes from Angular components nobody arranged
/// for a test's convenience, its element names are the components' own, and its data arrives over HTTP
/// after the page has already rendered once.
/// </remarks>
[Collection(SampleAppCollection.Name)]
public class StructureTests(SampleAppFixture fixture, ITestOutputHelper output)
{
    /// <summary>
    /// What the order list is, written as a field a suite can share - and against the components the
    /// application is built from rather than against the markup they render into.
    /// </summary>
    private static readonly WebElementStructure OrderList = WebElementStructure
        .OneElement("app-order-list")
            .WithAttribute("role", "table")
            .WithAttribute("data-count", static value => int.Parse(value ?? "0", CultureInfo.InvariantCulture) > 0, "a count above zero")
            .Containing(x =>
            {
                x.ManyElements("app-order-row").WithAttribute("data-order-id");
                x.OneElement("p").WithText(Cell.Contains("€"));
            });

    [BrowserFact]
    public async Task AStructureBuiltFromComponentsIsCheckedByItsOwnElementNames()
    {
        Timeline timeline = Timeline.Create()
            .Trigger(BrowserExt.Session("shop").Navigate("/orders").Expect("Anvil")).Name("open")
            .Trigger(BrowserExt.Page("shop").CompareStructure(Target.Section("Orders All"), OrderList)).Name("shape")
            .Build();

        TimelineRun run = await timeline.SetupRun(fixture.Services(), output).RunAsync();

        run.EnsureRanToCompletion();
        run.UiDifferences("shape").Should().HaveNoItems();

        // The inspection is part of the session story too, not a thing that happened off to one side.
        run.UiTrace("shop").Should().Match(
            static entries => entries.Any(entry => entry.Action == "CompareStructure"),
            "a recorded structure comparison");
    }

    [BrowserFact]
    public async Task ExtraMarkupDoesNotBreakAStructureThatDidNotMentionIt()
    {
        // The list also contains a header row and a total the expectation above never mentioned. Subset
        // matching is what makes a structure test survive somebody adding to the page.
        Timeline timeline = Timeline.Create()
            .Trigger(BrowserExt.Session("shop").Navigate("/orders").Expect("Anvil")).Name("open")
            .Trigger(BrowserExt.Page("shop").CompareStructure(
                Target.Section("Orders All"),
                WebElementStructure.OneElement("app-order-list")
                    .Containing(x => x.AtLeast(2, "app-order-row")))).Name("rows")
            .Build();

        TimelineRun run = await timeline.SetupRun(fixture.Services(), output).RunAsync();

        run.EnsureRanToCompletion();
    }

    [BrowserFact]
    public async Task ATableIsComparedAsRowsRatherThanAsMarkup()
    {
        // The page builds its "table" out of components and ARIA roles, not out of a <table>. A test about
        // the data should not have to know that, and this one does not.
        Timeline timeline = Timeline.Create()
            .Trigger(BrowserExt.Session("shop").Navigate("/orders").Expect("Anvil")).Name("open")
            .Trigger(BrowserExt.Page("shop").CompareTable(
                Target.Section("Orders All"),
                ExpectedTable.WithHeader("Order", "Product", "Qty", "Price")
                    .Row("A-1001", "Anvil", "1", Cell.Contains("129"))
                    .Row("A-1002", "Rope", "3", Cell.Contains("9")))).Name("orders")
            .Build();

        TimelineRun run = await timeline.SetupRun(fixture.Services(), output).RunAsync();

        run.EnsureRanToCompletion();
        run.UiDifferences("orders").Should().HaveNoItems();
    }

    [BrowserFact]
    public async Task ATableCanBeReadAsDataAndAssertedOnInTheTestsOwnTerms()
    {
        Timeline timeline = Timeline.Create()
            .Trigger(BrowserExt.Session("shop").Navigate("/orders").Expect("Anvil")).Name("open")
            .Trigger(BrowserExt.Page("shop").ReadTable(Target.Section("Orders All"), "orderRows")).Name("read")
            .Build();

        TimelineRun run = await timeline.SetupRun(fixture.Services(), output).RunAsync();

        run.EnsureRanToCompletion();

        // Keyed by column name, so this assertion survives a column being inserted.
        run.UiColumn("read", "Order").Should().HaveItems();
        run.UiTable("read").Should().Match(
            static rows => rows.All(row => row["Order"].StartsWith("A-", StringComparison.Ordinal)),
            "every row carrying an order number");

        // And the rows are an ordinary variable, so a later step could take them further.
        run.Variable<System.Collections.Generic.IReadOnlyList<System.Collections.Generic.IReadOnlyDictionary<string, string>>>("orderRows")
            .Should()
            .Exist();
    }

    [BrowserFact]
    public async Task AStructureMismatchHandsBackTheExpectationThatWouldPass()
    {
        Timeline timeline = Timeline.Create()
            .Trigger(BrowserExt.Session("shop").Navigate("/orders").Expect("Anvil")).Name("open")
            .Trigger(BrowserExt.Page("shop").CompareStructure(
                Target.Section("Orders All"),
                WebElementStructure.OneElement("app-order-list")
                    .Containing(x =>
                    {
                        x.Exactly(9, "app-order-row");
                        x.OneElement("app-pagination");
                    }))).Name("wrong")
            .Build();

        TimelineRun run = await timeline.SetupRun(fixture.Services(), output).RunAsync();

        UiStructureMismatchException failure = Assert.IsType<UiStructureMismatchException>(
            run.Step("wrong").LastResult.Exception);

        // Both problems are reported, each where it is.
        Assert.Equal(2, failure.Differences.Count);
        Assert.Contains(failure.Differences, difference => difference.Contains("app-pagination", StringComparison.Ordinal));

        // What the page actually is, and the expectation that would have passed - in the form the test is
        // written in, ready to read and paste.
        Assert.Contains("app-order-row", failure.Actual, StringComparison.Ordinal);
        Assert.NotNull(failure.SuggestedCode);
        Assert.Contains("WebElementStructure", failure.SuggestedCode!, StringComparison.Ordinal);
        Assert.Contains(".OneElement(\"app-order-list\")", failure.SuggestedCode!, StringComparison.Ordinal);

        output.WriteLine(failure.Message);
    }

    [BrowserFact]
    public async Task ATableMismatchPointsAtTheCellRatherThanTheRow()
    {
        Timeline timeline = Timeline.Create()
            .Trigger(BrowserExt.Session("shop").Navigate("/orders").Expect("Anvil")).Name("open")
            .Trigger(BrowserExt.Page("shop").CompareTable(
                Target.Section("Orders All"),
                ExpectedTable.WithHeader("Order", "Qty")
                    .Row("A-1001", "7")
                    .AllowExtraRows())).Name("wrong")
            .Build();

        TimelineRun run = await timeline.SetupRun(fixture.Services(), output).RunAsync();

        UiStructureMismatchException failure = Assert.IsType<UiStructureMismatchException>(
            run.Step("wrong").LastResult.Exception);

        // The row was found by its order number, so the difference is about the quantity - not about a
        // missing row, which is what a positional comparison would have said.
        string difference = Assert.Single(failure.Differences);
        Assert.Contains("column 'Qty'", difference, StringComparison.Ordinal);
        Assert.Contains("expected is '7', found '1'", difference, StringComparison.Ordinal);

        output.WriteLine(failure.Message);
    }

    /// <summary>
    /// The suggestion a failing comparison printed for this page, transcribed by hand exactly as a reader
    /// would paste it - nested lambda names, counts, attributes and all.
    /// </summary>
    private static readonly WebElementStructure AsSuggested = WebElementStructure
        .OneElement("app-order-list")
            .WithAttribute("aria-label", "Orders All")
            .WithAttribute("data-count", "2")
            .WithAttribute("role", "table")
            .Containing(x =>
            {
                x.OneElement("div")
                    .WithAttribute("role", "row")
                    .Containing(x2 => x2.Exactly(6, "span"));
                x.Exactly(2, "app-order-row");
                x.OneElement("p")
                    .WithAttribute("data-testid", "orders-total")
                    .WithText("€156.00");
            });

    [BrowserFact]
    public async Task TheCodeAFailureSuggestsIsCodeThatPasses()
    {
        // The round trip that makes the suggestion trustworthy: what a failure printed, pasted back in,
        // has to describe the page it came from. If the renderer ever drifts from the builder - a wrong
        // count, a shadowed lambda name, an attribute rendered the wrong way round - this goes red.
        Timeline timeline = Timeline.Create()
            .Trigger(BrowserExt.Session("shop").Navigate("/orders").Expect("Anvil")).Name("open")
            .Trigger(BrowserExt.Page("shop").CompareStructure(Target.Section("Orders All"), AsSuggested)).Name("pasted")
            .Build();

        TimelineRun run = await timeline.SetupRun(fixture.Services(), output).RunAsync();

        run.EnsureRanToCompletion();
        run.UiDifferences("pasted").Should().HaveNoItems();
    }

    [BrowserFact]
    public async Task AStructureCanBeCapturedForLaterRunsToBeComparedAgainst()
    {
        Timeline timeline = Timeline.Create()
            .Trigger(BrowserExt.Session("shop").Navigate("/orders").Expect("Anvil")).Name("open")
            .Trigger(BrowserExt.Page("shop").CaptureStructure(Target.Section("Orders All"), "orderListShape")).Name("capture")
            .Build();

        TimelineRun run = await timeline.SetupRun(fixture.Services(), output).RunAsync();

        run.EnsureRanToCompletion();

        // An ordinary variable, which is what the framework's value comparison sees: a later run of the same
        // test gets told when this changed, without anybody having written the shape down.
        run.Variable<string>("orderListShape").Should().Exist().Contain("app-order-row");

        // And it carries no framework noise, no styling and no transport bookkeeping, so a rebuild, a
        // restyle or a re-render does not read as drift. Each of these leaked at some point while this was
        // being built, and each one would have made the comparison worthless.
        run.UiCapturedStructure("capture").Should().NotContain("_ngcontent");
        run.UiCapturedStructure("capture").Should().NotContain("_nghost");
        run.UiCapturedStructure("capture").Should().NotContain("class=");
        run.UiCapturedStructure("capture").Should().NotContain("$id");

        output.WriteLine(run.Step("capture").UiCapture().Structure);
    }
}
