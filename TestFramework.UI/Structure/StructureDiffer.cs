using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace TestFramework.UI.Structure;

/// <summary>
/// Compares what a test expected of a page's structure against what the page actually was.
/// </summary>
/// <remarks>
/// <para>
/// Knows nothing about browsers: it walks expected nodes against snapshot nodes. That is what lets every
/// rule of the comparison - cardinalities, subset-by-default, escalation to exact, ordering, and the
/// wording of every difference - be tested exhaustively without a browser, and what will let a
/// non-web UI reuse the same algebra.
/// </para>
/// <para>
/// Children are matched as a set rather than pairwise by position, because position is the thing a page
/// changes most often and cares about least. Each expected child claims the actual children that satisfy
/// it, the claim is checked against its cardinality, and only what a test explicitly asked to be exact is
/// held to more than that.
/// </para>
/// </remarks>
public static class StructureDiffer
{
    /// <summary>
    /// Compares a structure against a snapshot.
    /// </summary>
    /// <param name="expected">What the test expects.</param>
    /// <param name="actual">What the page was.</param>
    /// <returns>Every difference found, empty when the page matched.</returns>
    public static IReadOnlyList<UiDifference> Compare(UiExpectedNode expected, UiElementSnapshot actual)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(actual);

        List<UiDifference> differences = new List<UiDifference>();

        CompareNode(expected, actual, expected.Describe(), differences);

        return differences;
    }

    private static void CompareNode(
        UiExpectedNode expected,
        UiElementSnapshot actual,
        string path,
        List<UiDifference> differences)
    {
        if (expected.Tag is { } tag && !TagMatches(tag, actual.Tag))
        {
            differences.Add(new UiDifference(
                UiDifferenceKind.RuleFailed,
                path,
                $"expected a <{tag}>, found a <{actual.Tag}>"));

            // Comparing the inside of the wrong element would bury the one difference that matters under
            // a list of consequences.
            return;
        }

        foreach (UiNodeRule rule in expected.Rules)
        {
            if (!rule.Predicate(actual))
            {
                differences.Add(new UiDifference(
                    UiDifferenceKind.RuleFailed,
                    path,
                    $"{rule.Description} — was {Describe(actual)}"));
            }
        }

        CompareChildren(expected, actual, path, differences);
    }

    private static void CompareChildren(
        UiExpectedNode expected,
        UiElementSnapshot actual,
        string path,
        List<UiDifference> differences)
    {
        if (expected.Children.Count == 0 && !expected.ChildrenExact)
        {
            return;
        }

        HashSet<int> claimed = new HashSet<int>();
        int lastClaimedIndex = -1;

        foreach (UiExpectedNode child in expected.Children)
        {
            string childPath = $"{path} > {child.Describe()}";

            // Only children nothing else has claimed, so two expectations for the same kind of element
            // cannot both be satisfied by one of them.
            List<int> matches = Enumerable
                .Range(0, actual.Children.Count)
                .Where(index => !claimed.Contains(index) && Satisfies(child, actual.Children[index]))
                .ToList();

            if (!child.Cardinality.Accepts(matches.Count))
            {
                differences.Add(new UiDifference(
                    matches.Count < child.Cardinality.Min ? UiDifferenceKind.Missing : UiDifferenceKind.Surplus,
                    childPath,
                    Describe(child, matches.Count, actual)));

                foreach (int index in matches)
                {
                    claimed.Add(index);
                }

                continue;
            }

            if (expected.ChildrenInOrder && matches.Count > 0)
            {
                if (matches[0] < lastClaimedIndex)
                {
                    differences.Add(new UiDifference(
                        UiDifferenceKind.OutOfOrder,
                        childPath,
                        "the test asked for these children in order, and this one comes earlier on the page than the one before it"));
                }

                lastClaimedIndex = matches[^1];
            }

            foreach (int index in matches)
            {
                claimed.Add(index);

                // Every match has to hold up on the inside too: "many rows, each with an order number" is
                // a statement about all of them.
                CompareNode(child, actual.Children[index], childPath, differences);
            }
        }

        if (!expected.ChildrenExact)
        {
            return;
        }

        // Only here, because a test asked for it: anything the expectation did not account for.
        foreach (int index in Enumerable.Range(0, actual.Children.Count).Where(index => !claimed.Contains(index)))
        {
            differences.Add(new UiDifference(
                UiDifferenceKind.Surplus,
                $"{path} > {actual.Children[index].Tag}",
                $"surplus — the page has {Describe(actual.Children[index])} and the structure allows no others"));
        }
    }

    private static bool Satisfies(UiExpectedNode expected, UiElementSnapshot actual)
        => (expected.Tag is null || TagMatches(expected.Tag, actual.Tag))
        && expected.Rules.All(rule => rule.Predicate(actual));

    private static bool TagMatches(string expected, string actual)
        => string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase);

    private static string Describe(UiElementSnapshot actual) => actual.ToString();

    private static string Describe(UiExpectedNode child, int found, UiElementSnapshot parent)
    {
        string what = child.Rules.Count == 0
            ? $"<{child.Describe()}>"
            : $"<{child.Describe()}> where {string.Join(" and ", child.Rules.Select(static rule => rule.Description))}";

        string verdict = found < child.Cardinality.Min ? "missing" : "surplus";

        // What the page does have instead is the fastest way to see whether the expectation or the page is
        // wrong - the same reason a not-found failure lists the names a page does offer.
        string instead = found == 0 && parent.Children.Count > 0
            ? $" The page has {string.Join(", ", parent.Children.Select(static c => $"<{c.Tag}>").Distinct().Take(8))} inside it."
            : string.Empty;

        return string.Format(
            CultureInfo.InvariantCulture,
            "expected {0} of {1}, found {2} ({3}).{4}",
            child.Cardinality,
            what,
            found,
            verdict,
            instead);
    }
}
