using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TestFramework.Core.Timelines;
using TestFramework.UI.Browser.Exceptions;
using TestFramework.UI.Browser.Targeting;
using TestFramework.UI.Browser.Tests.Shared;
using TestFramework.UI.Session;
using Xunit;
using Xunit.Abstractions;

namespace TestFramework.UI.Browser.Tests.Browser;

/// <summary>
/// The claims this package exists for, checked against a real application.
/// </summary>
/// <remarks>
/// Each test here is one promise: a control that was renamed is still pressed, a control that moved is
/// still found, a name that means three things fails rather than guessing, a broken application is
/// reported as broken. If one of these ever goes red, the feature has stopped being what it claims to be -
/// which is why they are stated as tests rather than as documentation.
/// </remarks>
[Collection(SampleAppCollection.Name)]
public class ResilienceTests(SampleAppFixture fixture, ITestOutputHelper output)
{
    [BrowserFact]
    public async Task ARenamedControlIsStillPressed()
    {
        // The page's button now says "Save changes". A test written when it said "Save" keeps working.
        Timeline timeline = Timeline.Create()
            .Trigger(BrowserExt.Session("shop")
                .Navigate("/renamed")
                .Click("Save")
                .Expect("Settings saved"))
                .Name("save")
            .Build();

        TimelineRun run = await timeline.SetupRun(fixture.Services(), output).RunAsync();

        run.EnsureRanToCompletion();

        // And the run says out loud that it had to reach for it, so a suite can hold the line if it wants
        // every match to be exact.
        run.UiLooseMatches("shop").Should().HaveItems();
        run.UiWeakestMatch("shop").Should().Contain("Loose");
    }

    [BrowserFact]
    public async Task AControlThatMovedIsFoundWhereverItEndedUp()
    {
        // Same button, now several wrappers deep in a restyled footer with generated class names. A target
        // never said where the button was, so there is nothing here for the layout to have broken.
        Timeline timeline = Timeline.Create()
            .Trigger(BrowserExt.Session("shop")
                .Navigate("/moved")
                .Click("Save changes")
                .Expect("Settings saved"))
                .Name("save")
            .Build();

        TimelineRun run = await timeline.SetupRun(fixture.Services(), output).RunAsync();

        run.EnsureRanToCompletion();
        run.UiLooseMatches("shop").Should().HaveNoItems();
    }

    [BrowserFact]
    public async Task ANameThatMeansThreeThingsFailsWithTheWayOut()
    {
        Timeline timeline = Timeline.Create()
            .Trigger(BrowserExt.Session("shop").Navigate("/ambiguous").Click("Delete")).Name("delete")
            .Build();

        TimelineRun run = await timeline.SetupRun(fixture.Services(), output).RunAsync();

        UiActionFailedException failure = Assert.IsType<UiActionFailedException>(run.Step("delete").LastResult.Exception);

        Assert.IsType<UiAmbiguousTargetException>(failure.InnerException);

        // Every candidate is named by the section it lives in, and each dial that would resolve it is
        // spelled out as the line to write. A reader fixes this without opening the page.
        Assert.Contains("matches 3 elements", failure.Message, StringComparison.Ordinal);
        Assert.Contains("Saved cards", failure.Message, StringComparison.Ordinal);
        Assert.Contains("Addresses", failure.Message, StringComparison.Ordinal);
        Assert.Contains(".InSection(\"Saved cards\")", failure.Message, StringComparison.Ordinal);
        Assert.Contains(".First()", failure.Message, StringComparison.Ordinal);

        output.WriteLine(failure.Message);
    }

    [BrowserFact]
    public async Task ScopingToASectionResolvesTheAmbiguity()
    {
        // The fix the failure message suggested, applied - and it works.
        Timeline timeline = Timeline.Create()
            .Trigger(BrowserExt.Session("shop")
                .Navigate("/ambiguous")
                .Click(Target.Button("Delete").InSection("Saved cards"))
                .Expect("Deleted: card"))
                .Name("delete")
            .Build();

        TimelineRun run = await timeline.SetupRun(fixture.Services(), output).RunAsync();

        run.EnsureRanToCompletion();
    }

    [BrowserFact]
    public async Task LenientMatchingIsAvailableAndStillRecorded()
    {
        // Configuration alone opts a whole application into accepting the first of several matches. The
        // choice is still in the session, so nothing became invisible by being allowed.
        Timeline timeline = Timeline.Create()
            .Trigger(BrowserExt.Session("shop-lenient")
                .Navigate("/ambiguous")
                .Click("Delete"))
                .Name("delete")
            .Build();

        TimelineRun run = await timeline.SetupRun(fixture.Services(), output).RunAsync();

        run.EnsureRanToCompletion();

        run.UiTrace("shop-lenient").Should().Match(
            static entries => entries.Any(entry => entry.Action == "Click" && entry.CandidateCount == 3),
            "a click that chose among three candidates");

        // And the choice counts as loose, so an audit can still refuse it.
        run.UiLooseMatches("shop-lenient").Should().HaveItems();
    }

    [BrowserFact]
    public async Task ABrokenApplicationIsReportedAsBrokenRatherThanAsAMissingElement()
    {
        // The page throws when the button is pressed, so what the test waits for never appears. The
        // difference between "your locator is wrong" and "the application crashed" is the difference
        // between an hour of debugging and a minute.
        Timeline timeline = Timeline.Create()
            .Trigger(BrowserExt.Session("shop")
                .Navigate("/broken")
                .Click("Generate report")
                .Expect("Report ready"))
                .Name("report")
            .Build();

        TimelineRun run = await timeline.SetupRun(fixture.Services(), output).RunAsync();

        Exception? failure = run.Step("report").LastResult.Exception;

        Assert.NotNull(failure);
        Assert.Contains("error(s) while this step ran", failure.Message, StringComparison.Ordinal);
        Assert.Contains("may be broken rather than the test", failure.Message, StringComparison.Ordinal);

        // The application's own complaint is in the session too, not only in the message.
        run.UiConsoleErrors("shop").Should().HaveItems();

        output.WriteLine(failure.Message);
    }

    [BrowserFact]
    public async Task AFailureLeavesEvidenceOnDisk()
    {
        Timeline timeline = Timeline.Create()
            .Trigger(BrowserExt.Session("shop")
                .Navigate("/renamed")
                .Click("Delete everything"))
                .Name("nonsense")
            .Build();

        TimelineRun run = await timeline.SetupRun(fixture.Services(), output).RunAsync();

        UiActionFailedException failure = Assert.IsType<UiActionFailedException>(run.Step("nonsense").LastResult.Exception);

        Assert.NotNull(failure.FailureBundlePath);
        Assert.True(Directory.Exists(failure.FailureBundlePath), "the bundle folder must exist");

        // Attachable to a ticket as it is: what it looked like, what the markup was, what the run had
        // done, and what the page complained about.
        Assert.True(File.Exists(Path.Combine(failure.FailureBundlePath!, "screenshot.png")));
        Assert.True(File.Exists(Path.Combine(failure.FailureBundlePath!, "page.html")));
        Assert.True(File.Exists(Path.Combine(failure.FailureBundlePath!, "session-picture.json")));

        // And the message says what the page does offer instead of what was asked for.
        Assert.Contains("The page does offer", failure.Message, StringComparison.Ordinal);

        output.WriteLine(failure.Message);
    }

    [BrowserFact]
    public async Task OneRunNeverSeesAnotherRunsState()
    {
        Timeline addToCart = Timeline.Create()
            .Trigger(BrowserExt.Session("shop")
                .Navigate("/products")
                .Click("Add Anvil to cart")
                .Expect("1 item in cart"))
                .Name("add")
            .Build();

        TimelineRun first = await addToCart.SetupRun(fixture.Services(), output).RunAsync();
        first.EnsureRanToCompletion();

        // The cart is kept in the browser's own storage, so a second run inheriting the first one's context
        // would start with an item in it.
        Timeline freshVisit = Timeline.Create()
            .Trigger(BrowserExt.Session("shop")
                .Navigate("/products")
                .Expect("0 items in cart"))
                .Name("visit")
            .Build();

        TimelineRun second = await freshVisit.SetupRun(fixture.Services(), output).RunAsync();

        second.EnsureRanToCompletion();
    }
}
