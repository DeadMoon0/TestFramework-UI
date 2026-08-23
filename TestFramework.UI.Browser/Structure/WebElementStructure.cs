using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TestFramework.UI;
using TestFramework.UI.Structure;

namespace TestFramework.UI.Browser.Structure;

/// <summary>
/// The structure a page is expected to have, written as a field next to the timeline that uses it.
/// </summary>
/// <remarks>
/// <para>
/// An expected structure is C# rather than a snapshot string, and that is the point. It can be named,
/// shared between tests, refactored by a tool, reviewed as code, and it holds real lambdas for the rules
/// that are not simple equality. A stringly snapshot can do none of those, which is why snapshot suites
/// tend to end up regenerated in bulk rather than read.
/// </para>
/// <para>
/// Component element names are first class: <c>OneElement("app-order-list")</c> means exactly what it
/// says, so a test can be written against the components an application is actually built from rather
/// than against the markup they happen to render into.
/// </para>
/// <para>
/// Children are matched as a subset, in any order, unless a test says otherwise - so somebody adding a
/// column or wrapping a panel in a layout element has not broken a test about something else. Escalate
/// locally with <see cref="ContainingExactly"/> and <see cref="InDocumentOrder"/>.
/// </para>
/// <example>
/// <code>
/// private static readonly WebElementStructure OrderList = WebElementStructure
///     .OneElement("app-order-list")
///         .WithAttribute("data-count", value =&gt; int.Parse(value ?? "0") &gt; 0, "a count above zero")
///         .Containing(x =&gt; x
///             .ManyElements("app-order-row").WithText(Cell.Matches(@"A-\d+"))
///             .OptionalElement("button").WithText("Load more"));
/// </code>
/// </example>
/// </remarks>
public sealed class WebElementStructure
{
    private readonly string? tag;
    private readonly UiCardinality cardinality;
    private readonly List<UiNodeRule> rules = new List<UiNodeRule>();
    private readonly List<WebElementStructure> children = new List<WebElementStructure>();
    private bool childrenExact;
    private bool childrenInOrder;
    private bool frozen;

    private WebElementStructure(string? tag, UiCardinality cardinality)
    {
        this.tag = tag?.ToLowerInvariant();
        this.cardinality = cardinality;
    }

    /// <summary>Exactly one element of this kind.</summary>
    /// <param name="tag">The element kind, including a component's own name.</param>
    /// <returns>The structure, to add rules and children to.</returns>
    public static WebElementStructure OneElement(string tag)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);

        return new WebElementStructure(tag, UiCardinality.One);
    }

    /// <summary>Exactly one <c>div</c>.</summary>
    /// <returns>The structure.</returns>
    public static WebElementStructure OneDiv() => OneElement("div");

    /// <summary>Exactly one element, whatever kind it is.</summary>
    /// <returns>The structure.</returns>
    public static WebElementStructure OneAnyElement() => new WebElementStructure(null, UiCardinality.One);

    /// <summary>
    /// Requires an attribute to have this value, compared normalized.
    /// </summary>
    /// <param name="name">The attribute name.</param>
    /// <param name="expected">The value it must have.</param>
    /// <returns>The same structure, for chaining.</returns>
    public WebElementStructure WithAttribute(string name, string expected)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return this.WithAttribute(name, Cell.Exactly(expected));
    }

    /// <summary>
    /// Requires an attribute to satisfy a rule.
    /// </summary>
    /// <param name="name">The attribute name.</param>
    /// <param name="rule">What the value must be.</param>
    /// <returns>The same structure, for chaining.</returns>
    public WebElementStructure WithAttribute(string name, CellRule rule)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(rule);

        return this.AddRule(
            $"attribute '{name}' {rule.Description}",
            element => rule.Matches(element.Attribute(name)));
    }

    /// <summary>
    /// Requires an attribute to satisfy a predicate.
    /// </summary>
    /// <param name="name">The attribute name.</param>
    /// <param name="rule">The predicate, receiving the raw value or null when the attribute is absent.</param>
    /// <param name="description">How the rule reads in a difference, for example <c>a count above zero</c>.</param>
    /// <returns>The same structure, for chaining.</returns>
    public WebElementStructure WithAttribute(string name, Func<string?, bool> rule, string description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        return this.AddRule($"attribute '{name}' is {description}", element => rule(element.Attribute(name)));
    }

    /// <summary>
    /// Requires an attribute to have this value after being transformed - for a value the page formats
    /// differently from the way a test wants to state it.
    /// </summary>
    /// <param name="name">The attribute name.</param>
    /// <param name="transform">Turns the page's value into the form the expectation is written in.</param>
    /// <param name="expected">The transformed value it must equal.</param>
    /// <returns>The same structure, for chaining.</returns>
    public WebElementStructure WithAttribute(string name, Func<string?, string?> transform, string expected)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(transform);

        return this.AddRule(
            $"attribute '{name}', transformed, is '{expected}'",
            element => UiText.EqualsNormalized(transform(element.Attribute(name)), expected));
    }

    /// <summary>
    /// Requires the element to carry this attribute at all, whatever its value.
    /// </summary>
    /// <param name="name">The attribute name.</param>
    /// <returns>The same structure, for chaining.</returns>
    public WebElementStructure WithAttribute(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return this.AddRule($"has attribute '{name}'", element => element.Attribute(name) is not null);
    }

    /// <summary>
    /// Requires the element's text to be this, compared normalized.
    /// </summary>
    /// <param name="expected">The text.</param>
    /// <returns>The same structure, for chaining.</returns>
    public WebElementStructure WithText(string expected) => this.WithText(Cell.Exactly(expected));

    /// <summary>
    /// Requires the element's text to satisfy a rule.
    /// </summary>
    /// <param name="rule">What the text must be.</param>
    /// <returns>The same structure, for chaining.</returns>
    public WebElementStructure WithText(CellRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);

        return this.AddRule($"text {rule.Description}", element => rule.Matches(element.Text));
    }

    /// <summary>
    /// Requires anything a test can say about the element.
    /// </summary>
    /// <param name="rule">The predicate over the whole element.</param>
    /// <param name="description">How it reads in a difference.</param>
    /// <returns>The same structure, for chaining.</returns>
    public WebElementStructure Where(Func<UiElementSnapshot, bool> rule, string description)
    {
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        return this.AddRule(description, rule);
    }

    /// <summary>
    /// Declares what must be inside this element. Other children are allowed, in any order.
    /// </summary>
    /// <remarks>
    /// Each call on the block adds a child and returns <em>that child</em>, so rules chain onto it. Two or
    /// more children are therefore declared as statements rather than as one chain:
    /// <example>
    /// <code>
    /// .Containing(x =&gt;
    /// {
    ///     x.ManyElements("app-order-row").WithAttribute("data-order-id");
    ///     x.OneElement("p").WithText(Cell.Contains("€"));
    /// })
    /// </code>
    /// </example>
    /// </remarks>
    /// <param name="children">Declares the children.</param>
    /// <returns>The same structure, for chaining.</returns>
    public WebElementStructure Containing(Action<IStructureChildren> children)
    {
        ArgumentNullException.ThrowIfNull(children);

        this.EnsureNotFrozen();
        children(new StructureChildren(this));

        return this;
    }

    /// <summary>
    /// Declares everything that may be inside this element - anything else is surplus.
    /// </summary>
    /// <param name="children">Declares the children.</param>
    /// <returns>The same structure, for chaining.</returns>
    public WebElementStructure ContainingExactly(Action<IStructureChildren> children)
    {
        this.Containing(children);
        this.childrenExact = true;

        return this;
    }

    /// <summary>
    /// Requires the declared children to appear in the order they were declared.
    /// </summary>
    /// <returns>The same structure, for chaining.</returns>
    public WebElementStructure InDocumentOrder()
    {
        this.EnsureNotFrozen();
        this.childrenInOrder = true;

        return this;
    }

    /// <summary>
    /// How this structure reads in one line, for a step's description.
    /// </summary>
    /// <returns>The description.</returns>
    public string Describe()
    {
        string what = $"<{this.tag ?? "*"}>";
        string inside = this.children.Count == 0
            ? string.Empty
            : $" containing {string.Join(", ", this.children.Select(static child => $"<{child.tag ?? "*"}>"))}";

        return what + inside;
    }

    /// <summary>
    /// Returns <see cref="Describe"/>.
    /// </summary>
    /// <returns>The description.</returns>
    public override string ToString() => this.Describe();

    /// <summary>
    /// Turns the structure into the neutral node tree the comparison walks, and freezes it.
    /// </summary>
    /// <remarks>
    /// Freezing on first use is what makes a <c>static readonly</c> structure safe to share: two timelines
    /// using the same field cannot change it out from under each other, and an attempt to says so.
    /// </remarks>
    /// <returns>The compiled expectation.</returns>
    internal UiExpectedNode Compile()
    {
        this.frozen = true;

        return new UiExpectedNode(
            this.tag,
            this.cardinality,
            this.rules,
            this.children.Select(static child => child.Compile()).ToList(),
            this.childrenExact,
            this.childrenInOrder);
    }

    internal WebElementStructure AddChild(string? tag, UiCardinality cardinality)
    {
        this.EnsureNotFrozen();

        WebElementStructure child = new WebElementStructure(tag, cardinality);
        this.children.Add(child);

        return child;
    }

    private WebElementStructure AddRule(string description, Func<UiElementSnapshot, bool> predicate)
    {
        this.EnsureNotFrozen();
        this.rules.Add(new UiNodeRule(description, predicate));

        return this;
    }

    private void EnsureNotFrozen()
    {
        if (this.frozen)
        {
            throw new InvalidOperationException(
                "This structure has already been used by a step and cannot be changed. A structure is meant " +
                "to be declared once and shared; build a new one with WebElementStructure.OneElement(...) " +
                "if a test needs a different shape.");
        }
    }
}

/// <summary>
/// Declares what is inside an element, and how many of each.
/// </summary>
/// <remarks>
/// Each call adds a child and returns it, so rules chain onto the child rather than onto its parent - which
/// is what makes the nesting read the way the markup does.
/// </remarks>
public interface IStructureChildren
{
    /// <summary>Exactly one element of this kind.</summary>
    /// <param name="tag">The element kind.</param>
    /// <returns>The child, to add rules and children to.</returns>
    WebElementStructure OneElement(string tag);

    /// <summary>One or more elements of this kind.</summary>
    /// <param name="tag">The element kind.</param>
    /// <returns>The child.</returns>
    WebElementStructure ManyElements(string tag);

    /// <summary>None or one element of this kind.</summary>
    /// <param name="tag">The element kind.</param>
    /// <returns>The child.</returns>
    WebElementStructure OptionalElement(string tag);

    /// <summary>Exactly this many elements of this kind.</summary>
    /// <param name="count">How many.</param>
    /// <param name="tag">The element kind.</param>
    /// <returns>The child.</returns>
    WebElementStructure Exactly(int count, string tag);

    /// <summary>This many elements of this kind or more.</summary>
    /// <param name="count">The minimum.</param>
    /// <param name="tag">The element kind.</param>
    /// <returns>The child.</returns>
    WebElementStructure AtLeast(int count, string tag);

    /// <summary>
    /// No element of this kind at all - an absence the test is asserting rather than assuming.
    /// </summary>
    /// <param name="tag">The element kind.</param>
    /// <returns>The child.</returns>
    WebElementStructure NoElement(string tag);

    /// <summary>Exactly one <c>div</c>.</summary>
    /// <returns>The child.</returns>
    WebElementStructure OneDiv();
}

/// <summary>
/// Adds children to the structure that opened the block.
/// </summary>
internal sealed class StructureChildren(WebElementStructure parent) : IStructureChildren
{
    /// <inheritdoc />
    public WebElementStructure OneElement(string tag) => this.Add(tag, UiCardinality.One);

    /// <inheritdoc />
    public WebElementStructure ManyElements(string tag) => this.Add(tag, UiCardinality.Many);

    /// <inheritdoc />
    public WebElementStructure OptionalElement(string tag) => this.Add(tag, UiCardinality.Optional);

    /// <inheritdoc />
    public WebElementStructure Exactly(int count, string tag) => this.Add(tag, UiCardinality.Exactly(count));

    /// <inheritdoc />
    public WebElementStructure AtLeast(int count, string tag) => this.Add(tag, UiCardinality.AtLeast(count));

    /// <inheritdoc />
    public WebElementStructure NoElement(string tag) => this.Add(tag, UiCardinality.None);

    /// <inheritdoc />
    public WebElementStructure OneDiv() => this.Add("div", UiCardinality.One);

    private WebElementStructure Add(string tag, UiCardinality cardinality)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);

        return parent.AddChild(tag, cardinality);
    }
}
