using System;
using System.Collections.Generic;
using System.Linq;
using TestFramework.UI.Browser.Structure;
using TestFramework.UI.Structure;

namespace TestFramework.UI.Browser.Tests.Structure;

/// <summary>
/// The code a failure hands back.
/// </summary>
/// <remarks>
/// The promise is that a reader can paste it, so these are mostly about it being real C# rather than
/// something that merely looks like it. Nested lambdas reusing a parameter name compiled fine in a
/// message and not at all in a file, which is exactly the kind of thing a suggestion must not do.
/// </remarks>
public class StructureCodeRendererTests
{
    private static UiElementSnapshot Element(
        string tag,
        string? text = null,
        (string Name, string Value)[]? attributes = null,
        params UiElementSnapshot[] children)
        => UiElementSnapshot
            .Of(tag, text, attributes?.ToDictionary(pair => pair.Name, pair => pair.Value, StringComparer.OrdinalIgnoreCase))
            .With(children);

    [Fact]
    public void NestedBlocksNeverReuseALambdaParameter()
    {
        UiElementSnapshot snapshot = Element(
            "app-order-list",
            null,
            null,
            Element("div", null, null, Element("span", "Order"), Element("span", "Price")),
            Element("app-order-row", "A-1001"));

        string code = StructureCodeRenderer.Render(snapshot);

        Assert.Contains(".Containing(x => x", code, StringComparison.Ordinal);
        Assert.Contains(".Containing(x2 => x2", code, StringComparison.Ordinal);

        // Which is to say: every level introduces its own name, so none shadows the one above it.
        int outerBlocks = code.Split(".Containing(x => x", StringSplitOptions.None).Length - 1;
        Assert.Equal(1, outerBlocks);
    }

    [Fact]
    public void RepeatedElementsBecomeACount()
    {
        // What a person would have written, and a tenth of the noise.
        UiElementSnapshot snapshot = Element(
            "app-order-list",
            null,
            null,
            Element("app-order-row", "A-1"),
            Element("app-order-row", "A-2"),
            Element("app-order-row", "A-3"));

        string code = StructureCodeRenderer.Render(snapshot);

        Assert.Contains(".Exactly(3, \"app-order-row\")", code, StringComparison.Ordinal);
    }

    [Fact]
    public void OnlyAttributesWorthPinningAreSuggested()
    {
        // Suggesting a generated width would teach the reader to write brittle expectations.
        UiElementSnapshot snapshot = Element(
            "app-order-list",
            null,
            [("role", "table"), ("data-count", "2"), ("width", "640"), ("tabindex", "-1")]);

        string code = StructureCodeRenderer.Render(snapshot);

        Assert.Contains(".WithAttribute(\"role\", \"table\")", code, StringComparison.Ordinal);
        Assert.Contains(".WithAttribute(\"data-count\", \"2\")", code, StringComparison.Ordinal);
        Assert.DoesNotContain("width", code, StringComparison.Ordinal);
        Assert.DoesNotContain("tabindex", code, StringComparison.Ordinal);
    }

    [Fact]
    public void TextIsOnlySuggestedWhereThereIsNothingInside()
    {
        // A container's text is its descendants' text, so pinning it would pin everything below it at once.
        string leaf = StructureCodeRenderer.Render(Element("p", "€156.00"));
        string container = StructureCodeRenderer.Render(Element("div", "€156.00", null, Element("p", "€156.00")));

        Assert.Contains(".WithText(\"€156.00\")", leaf, StringComparison.Ordinal);

        // Everything before the container's block is what was said about the container itself.
        string aboutTheContainer = container.Split(".Containing(", StringSplitOptions.None)[0];
        Assert.DoesNotContain(".WithText", aboutTheContainer, StringComparison.Ordinal);
    }

    [Fact]
    public void QuotesInTextSurviveAsEscapes()
    {
        string code = StructureCodeRenderer.Render(Element("p", "She said \"no\""));

        Assert.Contains("\\\"no\\\"", code, StringComparison.Ordinal);
    }

    [Fact]
    public void ATableRendersAsAnExpectedTable()
    {
        UiTableSnapshot table = new UiTableSnapshot(
            ["Order", "Qty"],
            [["A-1001", "1"], ["A-1002", "3"]]);

        string code = StructureCodeRenderer.Render(table);

        Assert.Equal(
            """
            ExpectedTable.WithHeader("Order", "Qty")
                .Row("A-1001", "1")
                .Row("A-1002", "3")
            """.ReplaceLineEndings(),
            code.ReplaceLineEndings());
    }
}
