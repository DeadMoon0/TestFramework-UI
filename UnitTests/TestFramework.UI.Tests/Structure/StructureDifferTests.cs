using System;
using System.Collections.Generic;
using System.Linq;
using TestFramework.UI.Structure;

namespace TestFramework.UI.Tests.Structure;

/// <summary>
/// The comparison algebra: cardinalities, subset-by-default, local escalation, and how differences read.
/// </summary>
public class StructureDifferTests
{
    private static UiExpectedNode Node(
        string? tag,
        UiCardinality? cardinality = null,
        IEnumerable<UiNodeRule>? rules = null,
        IEnumerable<UiExpectedNode>? children = null,
        bool exactChildren = false,
        bool inOrder = false)
        => new UiExpectedNode(
            tag,
            cardinality ?? UiCardinality.One,
            rules?.ToList() ?? [],
            children?.ToList() ?? [],
            exactChildren,
            inOrder);

    private static UiNodeRule TextIs(string expected)
        => new UiNodeRule($"text is '{expected}'", element => UiText.EqualsNormalized(element.Text, expected));

    private static UiElementSnapshot Element(string tag, string? text = null, params UiElementSnapshot[] children)
        => UiElementSnapshot.Of(tag, text).With(children);

    [Fact]
    public void AStructureThatMatchesReportsNothing()
    {
        UiExpectedNode expected = Node("app-order-list", children:
        [
            Node("app-order-row", UiCardinality.Many),
        ]);

        UiElementSnapshot actual = Element(
            "app-order-list",
            null,
            Element("app-order-row", "A-1001"),
            Element("app-order-row", "A-1002"));

        Assert.Empty(StructureDiffer.Compare(expected, actual));
    }

    [Fact]
    public void ExtraChildrenAreAllowedByDefault()
    {
        // The default that makes a structure test survive a page being worked on: somebody added a footer,
        // and the test was never about the footer.
        UiExpectedNode expected = Node("div", children: [Node("app-order-row", UiCardinality.Many)]);

        UiElementSnapshot actual = Element(
            "div",
            null,
            Element("app-order-row", "A-1001"),
            Element("footer", "Legal notice"),
            Element("aside", "Advertisement"));

        Assert.Empty(StructureDiffer.Compare(expected, actual));
    }

    [Fact]
    public void OrderIsIgnoredByDefault()
    {
        UiExpectedNode expected = Node("div", children:
        [
            Node("button"),
            Node("h2"),
        ]);

        UiElementSnapshot actual = Element("div", null, Element("h2", "Orders"), Element("button", "Load more"));

        Assert.Empty(StructureDiffer.Compare(expected, actual));
    }

    [Fact]
    public void OrderCanBeRequiredWhereItMatters()
    {
        UiExpectedNode expected = Node(
            "div",
            children: [Node("h2"), Node("button")],
            inOrder: true);

        UiElementSnapshot wrongWayRound = Element("div", null, Element("button", "Load more"), Element("h2", "Orders"));

        IReadOnlyList<UiDifference> differences = StructureDiffer.Compare(expected, wrongWayRound);

        Assert.Single(differences);
        Assert.Equal(UiDifferenceKind.OutOfOrder, differences[0].Kind);
    }

    [Fact]
    public void ExactChildrenTurnAnythingUnaccountedForIntoSurplus()
    {
        UiExpectedNode expected = Node("div", children: [Node("button")], exactChildren: true);

        UiElementSnapshot actual = Element("div", null, Element("button", "Save"), Element("span", "Beta"));

        IReadOnlyList<UiDifference> differences = StructureDiffer.Compare(expected, actual);

        Assert.Single(differences);
        Assert.Equal(UiDifferenceKind.Surplus, differences[0].Kind);
        Assert.Contains("span", differences[0].Path, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0, UiDifferenceKind.Missing)]
    [InlineData(1, null)]
    [InlineData(3, null)]
    public void ManyMeansAtLeastOne(int rowCount, UiDifferenceKind? expectedKind)
    {
        UiExpectedNode expected = Node("div", children: [Node("app-order-row", UiCardinality.Many)]);

        UiElementSnapshot actual = Element(
            "div",
            null,
            Enumerable.Range(0, rowCount).Select(index => Element("app-order-row", $"A-{index}")).ToArray());

        IReadOnlyList<UiDifference> differences = StructureDiffer.Compare(expected, actual);

        if (expectedKind is null)
        {
            Assert.Empty(differences);
        }
        else
        {
            Assert.Equal(expectedKind, differences.Single().Kind);
        }
    }

    [Fact]
    public void ExactlyIsHeldInBothDirections()
    {
        UiExpectedNode expected = Node("div", children: [Node("app-order-row", UiCardinality.Exactly(2))]);

        UiElementSnapshot tooFew = Element("div", null, Element("app-order-row", "A-1"));
        UiElementSnapshot tooMany = Element(
            "div",
            null,
            Element("app-order-row", "A-1"),
            Element("app-order-row", "A-2"),
            Element("app-order-row", "A-3"));

        Assert.Equal(UiDifferenceKind.Missing, StructureDiffer.Compare(expected, tooFew).Single().Kind);
        Assert.Equal(UiDifferenceKind.Surplus, StructureDiffer.Compare(expected, tooMany).Single().Kind);
    }

    [Fact]
    public void AnAbsenceCanBeAsserted()
    {
        // "There is no error on this page" is a claim worth making, and it is not the same as not mentioning
        // errors at all.
        UiExpectedNode expected = Node("div", children: [Node("app-error", UiCardinality.None)]);

        Assert.Empty(StructureDiffer.Compare(expected, Element("div", null, Element("span", "fine"))));
        Assert.Equal(
            UiDifferenceKind.Surplus,
            StructureDiffer.Compare(expected, Element("div", null, Element("app-error", "Boom"))).Single().Kind);
    }

    [Fact]
    public void OptionalMeansEitherWay()
    {
        UiExpectedNode expected = Node("div", children: [Node("button", UiCardinality.Optional)]);

        Assert.Empty(StructureDiffer.Compare(expected, Element("div")));
        Assert.Empty(StructureDiffer.Compare(expected, Element("div", null, Element("button", "More"))));
        Assert.NotEmpty(StructureDiffer.Compare(
            expected,
            Element("div", null, Element("button", "More"), Element("button", "Less"))));
    }

    [Fact]
    public void ARuleIsCheckedAgainstEveryMatch()
    {
        // "Many rows, each with an order number" is a statement about all of them, not about the first one.
        UiExpectedNode expected = Node("div", children:
        [
            Node("app-order-row", UiCardinality.Many, rules: [TextIs("A-1001")]),
        ]);

        UiElementSnapshot actual = Element(
            "div",
            null,
            Element("app-order-row", "A-1001"),
            Element("app-order-row", "A-1001"));

        Assert.Empty(StructureDiffer.Compare(expected, actual));
    }

    [Fact]
    public void ARuleThatFailsNamesItselfAndWhatWasThere()
    {
        UiExpectedNode expected = Node("app-order-row", rules: [TextIs("A-1001")]);

        IReadOnlyList<UiDifference> differences = StructureDiffer.Compare(expected, Element("app-order-row", "A-9999"));

        UiDifference difference = differences.Single();

        Assert.Equal(UiDifferenceKind.RuleFailed, difference.Kind);
        Assert.Contains("text is 'A-1001'", difference.Detail, StringComparison.Ordinal);
        Assert.Contains("A-9999", difference.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void TheWrongKindOfElementIsOneDifferenceRatherThanMany()
    {
        // Comparing the inside of the wrong element would bury the one thing that matters under its
        // consequences.
        UiExpectedNode expected = Node("app-order-list", children: [Node("app-order-row", UiCardinality.Many)]);

        IReadOnlyList<UiDifference> differences = StructureDiffer.Compare(expected, Element("app-invoice-list"));

        Assert.Single(differences);
        Assert.Contains("expected a <app-order-list>, found a <app-invoice-list>", differences[0].Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void ADifferenceSaysWhereItIs()
    {
        UiExpectedNode expected = Node("app-order-list", children:
        [
            Node("app-order-row", UiCardinality.Many, children: [Node("app-price")]),
        ]);

        UiElementSnapshot actual = Element("app-order-list", null, Element("app-order-row", "A-1"));

        UiDifference difference = StructureDiffer.Compare(expected, actual).Single();

        Assert.Equal("app-order-list > app-order-row > app-price", difference.Path);
    }

    [Fact]
    public void AMissingChildSaysWhatThePageHasInstead()
    {
        UiExpectedNode expected = Node("div", children: [Node("app-order-row", UiCardinality.Many)]);

        UiElementSnapshot actual = Element("div", null, Element("app-invoice-row", "I-1"), Element("footer"));

        UiDifference difference = StructureDiffer.Compare(expected, actual).Single();

        Assert.Contains("The page has <app-invoice-row>, <footer> inside it.", difference.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void TwoExpectationsCannotBeSatisfiedByOneElement()
    {
        // Otherwise "one button called Save and one called Cancel" would pass on a page with a single button
        // whose rules happen to fit neither exactly.
        UiExpectedNode expected = Node("div", children:
        [
            Node("button", rules: [TextIs("Save")]),
            Node("button", rules: [TextIs("Save")]),
        ]);

        UiElementSnapshot actual = Element("div", null, Element("button", "Save"));

        Assert.Single(StructureDiffer.Compare(expected, actual));
    }

    [Fact]
    public void AnyKindMatchesWhateverIsThere()
    {
        UiExpectedNode expected = Node("div", children: [Node(null, rules: [TextIs("Anything")])]);

        Assert.Empty(StructureDiffer.Compare(expected, Element("div", null, Element("span", "Anything"))));
    }
}
