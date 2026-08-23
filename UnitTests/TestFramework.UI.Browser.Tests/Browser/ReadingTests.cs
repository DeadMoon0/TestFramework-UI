using System;
using System.Threading.Tasks;
using TestFramework.Core.Timelines;
using TestFramework.UI.Browser.Exceptions;
using TestFramework.UI.Browser.Reading;
using TestFramework.UI.Browser.Targeting;
using TestFramework.UI.Browser.Tests.Shared;
using Xunit.Abstractions;

namespace TestFramework.UI.Browser.Tests.Browser;

/// <summary>
/// Reading values off the page, and choosing from list controls of both kinds.
/// </summary>
/// <remarks>
/// The checkout page carries a native <c>&lt;select&gt;</c> and a Material combobox side by side on
/// purpose: the same verbs must drive both, and the reads must answer in their own types.
/// </remarks>
[Collection(SampleAppCollection.Name)]
public class ReadingTests(SampleAppFixture fixture, ITestOutputHelper output)
{
    [BrowserFact]
    public async Task WhatAFormHoldsComesBackTyped()
    {
        Timeline timeline = Timeline.Create()
            .Trigger(BrowserExt.Session("shop")
                .Navigate("/checkout")
                .Fill("Email", "showroom@example.test")
                .Check("Accept terms")
                .Choose("Shipping", "Express")                                  // native select, by LABEL - its value is 'express'
                .Choose("Country", "Austria")                                   // Material combobox, options in an overlay
                .Read(Value.FieldValue(Target.Field("Email")), "email")
                .Read(Value.Checked(Target.Checkbox("Accept terms")), "accepted")
                .Read(Value.SelectedOption(Target.Field("Shipping")), "shipping")
                .Read(Value.SelectedOption(Target.Field("Country")), "country")
                .Read(Value.Url(), "where"))
                .Name("form")
            .Build();

        TimelineRun run = await timeline.SetupRun(fixture.Services(), output).RunAsync();

        run.EnsureRanToCompletion();

        // A field's value, not its text - the text of an input is empty however much was typed.
        run.Variable<string>("email").Should().Be("showroom@example.test");

        // A tick is a bool, not the string "true".
        run.Variable<bool>("accepted").Should().Be(true);

        // Both list controls answer with what a person sees, whichever way they are built.
        run.Variable<string>("shipping").Should().Be("Express");
        run.Variable<string>("country").Should().Be("Austria");

        run.Variable<string>("where").Should().Contain("/checkout");
    }

    [BrowserFact]
    public async Task CountsAndAttributesAnswerAboutThePage()
    {
        Timeline timeline = Timeline.Create()
            .Trigger(BrowserExt.Session("shop")
                .Navigate("/orders")
                .Expect("Anvil")
                .Read(Value.Count(Target.Css("app-order-row")), "rows")
                .Read(Value.Count(Target.Css("app-invoice-row")), "invoices")   // zero is an answer
                .Read(Value.Attribute(Target.Section("Orders All"), "data-count"), "declared"))
                .Name("orders")
            .Build();

        TimelineRun run = await timeline.SetupRun(fixture.Services(), output).RunAsync();

        run.EnsureRanToCompletion();

        // The page and its own attribute agree - and the count is an int, so this comparison is numeric.
        run.Variable<int>("rows").Should().Be(2);
        run.Variable<int>("invoices").Should().Be(0);
        run.Variable<string>("declared").Should().Be("2");
    }

    [BrowserFact]
    public async Task WhatTheApplicationStoresIsReadable()
    {
        Timeline timeline = Timeline.Create()
            .Trigger(BrowserExt.Session("shop")
                .Navigate("/products")
                .Click("Add Anvil to cart")
                .Expect("1 item in cart")
                .Read(Value.LocalStorage("sample-app.cart"), "storedCart"))
                .Name("cart")
            .Build();

        TimelineRun run = await timeline.SetupRun(fixture.Services(), output).RunAsync();

        run.EnsureRanToCompletion();

        // The raw stored string: whether it is JSON and what its culture is are the application's
        // business, and a test that cares parses it with a Transform of its own.
        run.Variable<string>("storedCart").Should().Contain("Anvil");
    }

    [BrowserFact]
    public async Task TheQueryStringIsReadableParameterByParameter()
    {
        Timeline timeline = Timeline.Create()
            .Trigger(BrowserExt.Session("shop")
                .Navigate("/confirmation?total=138&shipping=express")
                .Expect("Thank you")
                .Read(Value.QueryParam("shipping"), "shipping"))
                .Name("landed")
            .Build();

        TimelineRun run = await timeline.SetupRun(fixture.Services(), output).RunAsync();

        run.EnsureRanToCompletion();
        run.Variable<string>("shipping").Should().Be("express");
    }

    [BrowserFact]
    public async Task AMissingValueNamesWhatIsThereInstead()
    {
        // The reader's failure contract, once per family: the fix should be a copy, not an expedition.
        Timeline attributes = Timeline.Create()
            .Trigger(BrowserExt.Session("shop")
                .Navigate("/orders")
                .Expect("Anvil")
                .Read(Value.Attribute(Target.Section("Orders All"), "data-nope"), "x"))
                .Name("read")
            .Build();

        TimelineRun run = await attributes.SetupRun(fixture.Services(), output).RunAsync();

        UiActionFailedException failure = Assert.IsType<UiActionFailedException>(run.Step("read").LastResult.Exception);

        Assert.Contains("no attribute 'data-nope'", failure.Message, StringComparison.Ordinal);
        Assert.Contains("'data-count'", failure.Message, StringComparison.Ordinal);

        Timeline parameters = Timeline.Create()
            .Trigger(BrowserExt.Session("shop")
                .Navigate("/confirmation?total=138")
                .Expect("Thank you")
                .Read(Value.QueryParam("nope"), "x"))
                .Name("read")
            .Build();

        run = await parameters.SetupRun(fixture.Services(), output).RunAsync();
        failure = Assert.IsType<UiActionFailedException>(run.Step("read").LastResult.Exception);

        Assert.Contains("no query parameter 'nope'", failure.Message, StringComparison.Ordinal);
        Assert.Contains("'total'", failure.Message, StringComparison.Ordinal);
    }

    [BrowserFact]
    public async Task AFillThatMissesNamesTheFieldsThePageOffers()
    {
        // Regression: collecting these suggestions once built GetByPlaceholder(null) and crashed with a
        // NullReferenceException - so every transiently mis-timed Fill died on the message instead of
        // retrying, and a genuinely mistyped field name got a crash instead of the way out.
        Timeline timeline = Timeline.Create()
            .Trigger(BrowserExt.Session("shop")
                .Navigate("/checkout")
                .Fill("Emial", "typo@example.test"))
                .Name("typo")
            .Build();

        TimelineRun run = await timeline.SetupRun(fixture.Services(), output).RunAsync();

        UiActionFailedException failure = Assert.IsType<UiActionFailedException>(run.Step("typo").LastResult.Exception);

        Assert.DoesNotContain("Object reference", failure.Message, StringComparison.Ordinal);

        // The fields the page does offer, read off the role channels - including the placeholder-only
        // one, whose accessible name is its placeholder.
        Assert.Contains("Email", failure.Message, StringComparison.Ordinal);
        Assert.Contains("Street", failure.Message, StringComparison.Ordinal);

        output.WriteLine(failure.Message);
    }

    [BrowserFact]
    public async Task SelectRefusesAComboboxAndNamesTheVerbThatDrivesIt()
    {
        // The most likely mistake a Material shop will make, met with the answer rather than a mystery:
        // Select is the native-only verb, and the message says what to use instead.
        Timeline timeline = Timeline.Create()
            .Trigger(BrowserExt.Session("shop")
                .Navigate("/checkout")
                .Select("Country", "Austria"))
                .Name("wrong-verb")
            .Build();

        TimelineRun run = await timeline.SetupRun(fixture.Services(), output).RunAsync();

        UiActionFailedException failure = Assert.IsType<UiActionFailedException>(run.Step("wrong-verb").LastResult.Exception);

        Assert.Contains("not a native <select>", failure.Message, StringComparison.Ordinal);
        Assert.Contains("Choose(", failure.Message, StringComparison.Ordinal);

        output.WriteLine(failure.Message);
    }

    [BrowserFact]
    public async Task AnOptionNobodyOffersFailsListingTheOnesOffered()
    {
        Timeline timeline = Timeline.Create()
            .Trigger(BrowserExt.Session("shop")
                .Navigate("/checkout")
                .Choose("Country", "France"))
                .Name("choose")
            .Build();

        TimelineRun run = await timeline.SetupRun(fixture.Services(), output).RunAsync();

        UiActionFailedException failure = Assert.IsType<UiActionFailedException>(run.Step("choose").LastResult.Exception);

        Assert.Contains("no option 'France'", failure.Message, StringComparison.Ordinal);
        Assert.Contains("'Germany'", failure.Message, StringComparison.Ordinal);
        Assert.Contains("'Austria'", failure.Message, StringComparison.Ordinal);

        output.WriteLine(failure.Message);
    }
}
