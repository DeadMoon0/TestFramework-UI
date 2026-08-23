using System;
using System.Linq;
using TestFramework.Core.Steps.Options;
using TestFramework.UI.Browser.Reading;
using TestFramework.UI.Browser.Resolution;
using TestFramework.UI.Browser.Steps;
using TestFramework.UI.Browser.Targeting;

namespace TestFramework.UI.Browser.Tests.Reading;

/// <summary>
/// The value-source model: what each source says about itself, and what a flow declares for it.
/// </summary>
public class UiValueSourceTests
{
    [Fact]
    public void EachSourceProducesItsNaturalType()
    {
        // A tick is a bool and a count is an int, so the step consuming the variable gets a value it can
        // reason about rather than a string that happens to hold digits.
        Assert.Equal(typeof(string), Value.Text("Total").ValueType);
        Assert.Equal(typeof(string), Value.FieldValue(Target.Field("Email")).ValueType);
        Assert.Equal(typeof(bool), Value.Checked(Target.Checkbox("Accept terms")).ValueType);
        Assert.Equal(typeof(int), Value.Count(Target.Css("app-order-row")).ValueType);
        Assert.Equal(typeof(string), Value.Attribute(Target.Section("Orders"), "data-count").ValueType);
        Assert.Equal(typeof(string), Value.SelectedOption(Target.Field("Country")).ValueType);
        Assert.Equal(typeof(string), Value.Url().ValueType);
        Assert.Equal(typeof(string), Value.QueryParam("total").ValueType);
        Assert.Equal(typeof(string), Value.LocalStorage("cart").ValueType);
    }

    [Fact]
    public void ASourceDescribesItselfTheWayATraceNeedsIt()
    {
        Assert.Equal("value of field 'Email'", Value.FieldValue(Target.Field("Email")).Describe());
        Assert.Equal("whether checkbox 'Accept terms' is checked", Value.Checked(Target.Checkbox("Accept terms")).Describe());
        Assert.Equal("query parameter 'total'", Value.QueryParam("total").Describe());
        Assert.Equal("local storage 'sample-app.cart'", Value.LocalStorage("sample-app.cart").Describe());
        Assert.Equal("the page address", Value.Url().Describe());
    }

    [Fact]
    public void SourcesThatNeedANameRefuseAnEmptyOne()
    {
        Assert.ThrowsAny<ArgumentException>(() => Value.Attribute(Target.Section("Orders"), " "));
        Assert.ThrowsAny<ArgumentException>(() => Value.QueryParam(""));
        Assert.ThrowsAny<ArgumentException>(() => Value.LocalStorage(" "));
    }

    [Fact]
    public void ReadingATargetIsReadingItsText()
    {
        // The old spelling and the new one are the same read, so there is exactly one behavior to trust.
        UiBrowserFlow flow = BrowserExt.Session("shop").Read(Target.TestId("order-number"), "orderNo");

        UiActionSpec action = Assert.Single(flow.ActionsForTesting);

        Assert.Equal(UiActionKind.Read, action.Kind);
        Assert.Equal(UiValueKind.Text, action.Source!.Kind);
        Assert.Equal("orderNo", action.CaptureName);
    }

    [Fact]
    public void AReadDeclaresItsVariableInTheSourcesType()
    {
        UiBrowserFlow flow = BrowserExt.Session("shop")
            .Read(Value.Checked(Target.Checkbox("Accept terms")), "accepted")
            .Read(Value.Count(Target.Css("app-order-row")), "rows")
            .Read(Value.Url(), "where");

        StepIOContract contract = new StepIOContract();
        flow.DeclareIO(contract);

        Assert.Equal(typeof(bool), contract.Outputs.Single(static entry => entry.Key == "accepted").DeclaredType);
        Assert.Equal(typeof(int), contract.Outputs.Single(static entry => entry.Key == "rows").DeclaredType);
        Assert.Equal(typeof(string), contract.Outputs.Single(static entry => entry.Key == "where").DeclaredType);
    }

    [Fact]
    public void EachSourceResolvesItsTargetTheWayItsVerbWould()
    {
        // A field value is looked for where Fill would look, a tick where Check would - the read and the
        // action that produced the state must agree on what the words mean.
        Assert.Equal(UiSmartContext.Fillable, SpecFor(Value.FieldValue("Email")).Context);
        Assert.Equal(UiSmartContext.Fillable, SpecFor(Value.SelectedOption("Country")).Context);
        Assert.Equal(UiSmartContext.Checkable, SpecFor(Value.Checked("Accept terms")).Context);
        Assert.Equal(UiSmartContext.Text, SpecFor(Value.Text("Thank you")).Context);
    }

    [Fact]
    public void ChoosingLooksWhereFillingWould()
    {
        UiBrowserFlow flow = BrowserExt.Session("shop").Choose("Country", "Austria");

        UiActionSpec action = Assert.Single(flow.ActionsForTesting);

        Assert.Equal(UiActionKind.Choose, action.Kind);
        Assert.Equal(UiSmartContext.Fillable, action.Context);
    }

    [Fact]
    public void AReadDescribesWhatItReadsRatherThanJustSayingRead()
    {
        UiBrowserFlow flow = BrowserExt.Session("shop").Read(Value.QueryParam("total"), "total");

        Assert.Equal("Read query parameter 'total'", Assert.Single(flow.ActionsForTesting).Describe());
    }

    private static UiActionSpec SpecFor(UiValueSource source)
        => Assert.Single(((UiBrowserFlow)BrowserExt.Session("shop").Read(source, "x")).ActionsForTesting);
}
