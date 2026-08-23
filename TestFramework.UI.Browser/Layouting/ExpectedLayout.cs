using System;
using System.Collections.Generic;
using TestFramework.UI.Browser.Targeting;

namespace TestFramework.UI.Browser.Layouting;

/// <summary>
/// The kinds of relation a layout can state.
/// </summary>
internal enum UiLayoutRelation
{
    /// <summary>The first element ends above the second begins.</summary>
    Above = 0,

    /// <summary>The first element ends left of where the second begins.</summary>
    LeftOf,

    /// <summary>The first element lies entirely within the second.</summary>
    Inside,

    /// <summary>The two elements do not visibly overlap.</summary>
    NotOverlapping,

    /// <summary>The element lies entirely within the viewport.</summary>
    InViewport,

    /// <summary>The page needs no sideways scrolling.</summary>
    NoHorizontalScroll,
}

/// <summary>
/// One relation of a layout, as written.
/// </summary>
/// <param name="Relation">What is stated.</param>
/// <param name="First">The element the statement is about.</param>
/// <param name="Second">The element it is related to, for the two-element relations.</param>
internal sealed record UiLayoutRule(UiLayoutRelation Relation, UiTarget? First, UiTarget? Second)
{
    /// <summary>
    /// How the rule reads in a difference message.
    /// </summary>
    /// <returns>The description, for example <c>field 'Email' above button 'Place order'</c>.</returns>
    public string Describe() => this.Relation switch
    {
        UiLayoutRelation.Above => $"{this.First!.Describe()} above {this.Second!.Describe()}",
        UiLayoutRelation.LeftOf => $"{this.First!.Describe()} left of {this.Second!.Describe()}",
        UiLayoutRelation.Inside => $"{this.First!.Describe()} inside {this.Second!.Describe()}",
        UiLayoutRelation.NotOverlapping => $"{this.First!.Describe()} not overlapping {this.Second!.Describe()}",
        UiLayoutRelation.InViewport => $"{this.First!.Describe()} within the viewport",
        UiLayoutRelation.NoHorizontalScroll => "no sideways scrolling",
        _ => this.Relation.ToString(),
    };
}

/// <summary>
/// The layout a test expects, stated as relations between the things a person sees.
/// </summary>
/// <remarks>
/// <para>
/// Declared as a field beside the timeline, like an <c>ExpectedTable</c> or a
/// <c>WebElementStructure</c>. Relations rather than coordinates, on purpose: "the email field is above
/// the order button" survives a margin change, a font substitution and another machine's rendering,
/// while a stored rectangle is wrong the moment any of those happen. This is the layout counterpart of
/// naming a button by its words instead of its selector.
/// </para>
/// <para>
/// Every relation shares a two-pixel tolerance, so subpixel layout and rounding never fail a check that
/// a person would call fine.
/// </para>
/// </remarks>
public sealed class ExpectedLayout
{
    private readonly List<UiLayoutRule> rules = new List<UiLayoutRule>();
    private bool frozen;

    private ExpectedLayout()
    {
    }

    /// <summary>Starts a layout: one element must end above where another begins.</summary>
    /// <param name="above">The higher element.</param>
    /// <param name="below">The lower element.</param>
    /// <returns>The layout, to add relations to.</returns>
    public static ExpectedLayout Above(UiTarget above, UiTarget below) => new ExpectedLayout().AndAbove(above, below);

    /// <summary>Starts a layout: one element must end left of where another begins.</summary>
    /// <param name="left">The element further left.</param>
    /// <param name="right">The element further right.</param>
    /// <returns>The layout, to add relations to.</returns>
    public static ExpectedLayout LeftOf(UiTarget left, UiTarget right) => new ExpectedLayout().AndLeftOf(left, right);

    /// <summary>Starts a layout: one element must lie entirely within another.</summary>
    /// <param name="inner">The contained element.</param>
    /// <param name="outer">The containing element.</param>
    /// <returns>The layout, to add relations to.</returns>
    public static ExpectedLayout Inside(UiTarget inner, UiTarget outer) => new ExpectedLayout().AndInside(inner, outer);

    /// <summary>Starts a layout: two elements must not visibly overlap.</summary>
    /// <param name="first">One element.</param>
    /// <param name="second">The other element.</param>
    /// <returns>The layout, to add relations to.</returns>
    public static ExpectedLayout NotOverlapping(UiTarget first, UiTarget second) => new ExpectedLayout().AndNotOverlapping(first, second);

    /// <summary>Starts a layout: an element must lie entirely within the viewport.</summary>
    /// <param name="target">The element.</param>
    /// <returns>The layout, to add relations to.</returns>
    public static ExpectedLayout InViewport(UiTarget target) => new ExpectedLayout().AndInViewport(target);

    /// <summary>Starts a layout: the page must need no sideways scrolling.</summary>
    /// <returns>The layout, to add relations to.</returns>
    public static ExpectedLayout NoHorizontalScroll() => new ExpectedLayout().AndNoHorizontalScroll();

    /// <summary>Adds: one element must end above where another begins.</summary>
    /// <param name="above">The higher element.</param>
    /// <param name="below">The lower element.</param>
    /// <returns>The same layout, for chaining.</returns>
    public ExpectedLayout AndAbove(UiTarget above, UiTarget below)
        => this.Add(UiLayoutRelation.Above, above, below);

    /// <summary>Adds: one element must end left of where another begins.</summary>
    /// <param name="left">The element further left.</param>
    /// <param name="right">The element further right.</param>
    /// <returns>The same layout, for chaining.</returns>
    public ExpectedLayout AndLeftOf(UiTarget left, UiTarget right)
        => this.Add(UiLayoutRelation.LeftOf, left, right);

    /// <summary>Adds: one element must lie entirely within another.</summary>
    /// <param name="inner">The contained element.</param>
    /// <param name="outer">The containing element.</param>
    /// <returns>The same layout, for chaining.</returns>
    public ExpectedLayout AndInside(UiTarget inner, UiTarget outer)
        => this.Add(UiLayoutRelation.Inside, inner, outer);

    /// <summary>Adds: two elements must not visibly overlap.</summary>
    /// <param name="first">One element.</param>
    /// <param name="second">The other element.</param>
    /// <returns>The same layout, for chaining.</returns>
    public ExpectedLayout AndNotOverlapping(UiTarget first, UiTarget second)
        => this.Add(UiLayoutRelation.NotOverlapping, first, second);

    /// <summary>Adds: an element must lie entirely within the viewport.</summary>
    /// <param name="target">The element.</param>
    /// <returns>The same layout, for chaining.</returns>
    public ExpectedLayout AndInViewport(UiTarget target)
        => this.Add(UiLayoutRelation.InViewport, target, second: null);

    /// <summary>Adds: the page must need no sideways scrolling.</summary>
    /// <returns>The same layout, for chaining.</returns>
    public ExpectedLayout AndNoHorizontalScroll()
        => this.Add(UiLayoutRelation.NoHorizontalScroll, first: null, second: null);

    /// <summary>
    /// How this layout reads in one line, for a step's description.
    /// </summary>
    /// <returns>The description.</returns>
    public string Describe() => $"{this.rules.Count} layout relation(s)";

    /// <summary>
    /// The rules as written, frozen against further change.
    /// </summary>
    /// <returns>The rules.</returns>
    internal IReadOnlyList<UiLayoutRule> Compile()
    {
        this.frozen = true;

        return this.rules;
    }

    private ExpectedLayout Add(UiLayoutRelation relation, UiTarget? first, UiTarget? second)
    {
        if (this.frozen)
        {
            throw new InvalidOperationException(
                "This layout has already been used by a step and cannot be changed. A layout is meant to be " +
                "declared once and shared; start a new one for a different shape.");
        }

        if (relation is not UiLayoutRelation.NoHorizontalScroll)
        {
            ArgumentNullException.ThrowIfNull(first);
        }

        if (relation is UiLayoutRelation.Above or UiLayoutRelation.LeftOf or UiLayoutRelation.Inside or UiLayoutRelation.NotOverlapping)
        {
            ArgumentNullException.ThrowIfNull(second);
        }

        this.rules.Add(new UiLayoutRule(relation, first, second));

        return this;
    }
}
