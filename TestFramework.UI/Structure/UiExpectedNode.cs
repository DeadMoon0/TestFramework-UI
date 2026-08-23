using System;
using System.Collections.Generic;
using System.Globalization;

namespace TestFramework.UI.Structure;

/// <summary>
/// How many of something a structure expects.
/// </summary>
/// <param name="Min">The fewest acceptable.</param>
/// <param name="Max">The most acceptable, or null for any number.</param>
public sealed record UiCardinality(int Min, int? Max)
{
    /// <summary>Exactly one.</summary>
    public static UiCardinality One { get; } = new UiCardinality(1, 1);

    /// <summary>At least one, with no upper limit.</summary>
    public static UiCardinality Many { get; } = new UiCardinality(1, null);

    /// <summary>None or one.</summary>
    public static UiCardinality Optional { get; } = new UiCardinality(0, 1);

    /// <summary>None at all - an asserted absence.</summary>
    public static UiCardinality None { get; } = new UiCardinality(0, 0);

    /// <summary>Exactly this many.</summary>
    /// <param name="count">The count.</param>
    /// <returns>The cardinality.</returns>
    public static UiCardinality Exactly(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        return new UiCardinality(count, count);
    }

    /// <summary>This many or more.</summary>
    /// <param name="count">The minimum.</param>
    /// <returns>The cardinality.</returns>
    public static UiCardinality AtLeast(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        return new UiCardinality(count, null);
    }

    /// <summary>
    /// Whether a count satisfies this.
    /// </summary>
    /// <param name="count">How many were found.</param>
    /// <returns>True when the count is acceptable.</returns>
    public bool Accepts(int count) => count >= this.Min && (this.Max is null || count <= this.Max);

    /// <summary>
    /// How this reads in a difference message.
    /// </summary>
    /// <returns>The description, for example <c>at least 1</c>.</returns>
    public override string ToString()
        => (this.Min, this.Max) switch
        {
            (0, 0) => "none",
            (1, 1) => "exactly 1",
            (var min, null) => string.Format(CultureInfo.InvariantCulture, "at least {0}", min),
            (0, 1) => "at most 1",
            var (min, max) when min == max => string.Format(CultureInfo.InvariantCulture, "exactly {0}", min),
            var (min, max) => string.Format(CultureInfo.InvariantCulture, "between {0} and {1}", min, max),
        };
}

/// <summary>
/// One thing a structure expects of an element: what must be true of it, and how that reads.
/// </summary>
/// <remarks>
/// The description is what a failure message shows, so the predicate can be anything a test author can
/// write - including a lambda over the whole element - without any of it having to be serializable. Only
/// the description ever leaves the test process.
/// </remarks>
/// <param name="Description">How the rule reads, for example <c>name is 'orders'</c>.</param>
/// <param name="Predicate">What it checks.</param>
public sealed record UiNodeRule(string Description, Func<UiElementSnapshot, bool> Predicate);

/// <summary>
/// An expected element: its kind, how many of it, what must be true of it, and what must be inside it.
/// </summary>
/// <remarks>
/// <para>
/// This is what a fluent structure compiles into, and what the comparison walks. Keeping it separate from
/// the builder means the comparison knows nothing about browsers, tags or the DSL that produced it - it
/// only knows about nodes, rules and counts.
/// </para>
/// <para>
/// Children are a <em>subset</em> in <em>any order</em> unless a test says otherwise. That default is the
/// whole reason a structure expectation survives a page being worked on: somebody adding a column,
/// wrapping a section in a layout div, or reordering two panels has not broken what the test was about.
/// A test that does care says so with <c>ContainingExactly</c> or <c>InDocumentOrder</c>, and then it is
/// the test's own decision rather than an accident of how the expectation was written.
/// </para>
/// </remarks>
/// <param name="Tag">The element kind expected, lower-cased. Null matches any kind.</param>
/// <param name="Cardinality">How many of it are expected among its parent's children.</param>
/// <param name="Rules">What must be true of each match.</param>
/// <param name="Children">What must be inside each match.</param>
/// <param name="ChildrenExact">True when no other children are allowed.</param>
/// <param name="ChildrenInOrder">True when the children must appear in the order given.</param>
public sealed record UiExpectedNode(
    string? Tag,
    UiCardinality Cardinality,
    IReadOnlyList<UiNodeRule> Rules,
    IReadOnlyList<UiExpectedNode> Children,
    bool ChildrenExact = false,
    bool ChildrenInOrder = false)
{
    /// <summary>
    /// How this node reads as a path segment in a difference message.
    /// </summary>
    /// <returns>The segment, for example <c>app-order-row</c> or <c>*</c>.</returns>
    public string Describe() => this.Tag ?? "*";
}
