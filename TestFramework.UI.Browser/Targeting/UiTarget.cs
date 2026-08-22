using System;
using System.Globalization;

namespace TestFramework.UI.Browser.Targeting;

/// <summary>
/// The element a step wants to act on, described the way a test author thinks about it.
/// </summary>
/// <remarks>
/// <para>
/// A target is a value, not a locator: it says what to look for, never where in the document to find
/// it. The search itself happens when the step runs, so a control that moved between writing the
/// test and running it is still the same target.
/// </para>
/// <para>
/// Every dial returns a new target, which is what makes a shared <c>static readonly</c> target safe
/// to narrow differently in two different steps.
/// </para>
/// </remarks>
public sealed record UiTarget
{
    internal UiTarget(UiTargetKind kind, string? name = null, string? role = null, string? css = null)
    {
        this.Kind = kind;
        this.Name = name;
        this.Role = role;
        this.Css = css;
    }

    /// <summary>How the test named this element.</summary>
    public UiTargetKind Kind { get; init; }

    /// <summary>The accessible name, label, placeholder, text or test id being looked for.</summary>
    public string? Name { get; init; }

    /// <summary>The ARIA role, when <see cref="Kind"/> is <see cref="UiTargetKind.Role"/>.</summary>
    public string? Role { get; init; }

    /// <summary>The CSS selector, when <see cref="Kind"/> is <see cref="UiTargetKind.Css"/>.</summary>
    public string? Css { get; init; }

    /// <summary>
    /// True when only an exact name match counts. Text is still normalized - a page wrapping a label
    /// across two lines says the same thing as the test.
    /// </summary>
    public bool Exact { get; init; }

    /// <summary>
    /// True when the first of several matches may be used instead of failing. Off by default, so a
    /// run never quietly picks one of three buttons called Delete.
    /// </summary>
    public bool AllowFirstOfMany { get; init; }

    /// <summary>Which match to use when several are expected, zero-based.</summary>
    public int? Index { get; init; }

    /// <summary>The region to search inside, when the search is scoped.</summary>
    public UiTarget? Scope { get; init; }

    /// <summary>Text an element must be near to qualify.</summary>
    public string? NearText { get; init; }

    /// <summary>
    /// Requires the name to match exactly rather than as a fragment.
    /// </summary>
    /// <returns>A new target.</returns>
    public UiTarget ExactMatch() => this with { Exact = true };

    /// <summary>
    /// Uses the match at the given position when several are expected, instead of failing on ambiguity.
    /// </summary>
    /// <param name="index">The zero-based position.</param>
    /// <returns>A new target.</returns>
    public UiTarget Nth(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);

        return this with { Index = index };
    }

    /// <summary>
    /// Restricts the search to the region, group or form with the given accessible name. The usual
    /// answer when the same control name appears more than once on a page.
    /// </summary>
    /// <param name="sectionName">The section's accessible name.</param>
    /// <returns>A new target.</returns>
    public UiTarget InSection(string sectionName) => this with { Scope = Target.Section(sectionName) };

    /// <summary>
    /// Restricts the search to inside another target.
    /// </summary>
    /// <param name="scope">The containing target.</param>
    /// <returns>A new target.</returns>
    public UiTarget Within(UiTarget scope)
    {
        ArgumentNullException.ThrowIfNull(scope);

        return this with { Scope = scope };
    }

    /// <summary>
    /// Requires the element to sit near the given text - useful for a control whose own name is
    /// generic but whose neighbourhood is not.
    /// </summary>
    /// <param name="text">The nearby text.</param>
    /// <returns>A new target.</returns>
    public UiTarget Near(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        return this with { NearText = text };
    }

    /// <summary>
    /// Accepts the first match when several qualify, instead of failing. Prefer naming the element
    /// more precisely; reach for this when the page genuinely offers equivalent choices.
    /// </summary>
    /// <returns>A new target.</returns>
    public UiTarget First() => this with { AllowFirstOfMany = true };

    /// <summary>
    /// Lets a plain string stand in for a target, so a step reads <c>Click("Save")</c>. What the
    /// string means is decided by the verb that receives it.
    /// </summary>
    /// <param name="name">The name, label or text of the element.</param>
    public static implicit operator UiTarget(string name) => Target.Smart(name);

    /// <summary>
    /// How this target reads in a log, a trace entry or a failure message, for example
    /// <c>button 'Save' in section 'Billing'</c>.
    /// </summary>
    /// <returns>The description.</returns>
    public string Describe()
    {
        string head = this.Kind switch
        {
            UiTargetKind.Smart => $"'{this.Name}'",
            UiTargetKind.Button => $"button '{this.Name}'",
            UiTargetKind.Link => $"link '{this.Name}'",
            UiTargetKind.Field => $"field '{this.Name}'",
            UiTargetKind.Checkbox => $"checkbox '{this.Name}'",
            UiTargetKind.Radio => $"radio button '{this.Name}'",
            UiTargetKind.Text => $"text '{this.Name}'",
            UiTargetKind.Role => this.Name is null ? $"role '{this.Role}'" : $"{this.Role} '{this.Name}'",
            UiTargetKind.Section => $"section '{this.Name}'",
            UiTargetKind.TestId => $"test id '{this.Name}'",
            UiTargetKind.Css => $"selector '{this.Css}'",
            UiTargetKind.ElementId => $"element id '{this.Name}'",
            _ => $"'{this.Name}'",
        };

        string scope = this.Scope is null ? string.Empty : $" in {this.Scope.Describe()}";
        string near = this.NearText is null ? string.Empty : $" near '{this.NearText}'";
        string index = this.Index is null
            ? string.Empty
            : string.Format(CultureInfo.InvariantCulture, " (match {0})", this.Index.Value);

        return head + scope + near + index;
    }

    /// <summary>
    /// Renders this target as the C# that produced it, for a failure message that shows the reader
    /// the line to change.
    /// </summary>
    /// <returns>The source form, for example <c>Target.Button("Save").InSection("Billing")</c>.</returns>
    public string ToSourceCode()
    {
        string head = this.Kind switch
        {
            UiTargetKind.Smart => Quote(this.Name),
            UiTargetKind.Button => $"Target.Button({Quote(this.Name)})",
            UiTargetKind.Link => $"Target.Link({Quote(this.Name)})",
            UiTargetKind.Field => $"Target.Field({Quote(this.Name)})",
            UiTargetKind.Checkbox => $"Target.Checkbox({Quote(this.Name)})",
            UiTargetKind.Radio => $"Target.Radio({Quote(this.Name)})",
            UiTargetKind.Text => $"Target.Text({Quote(this.Name)})",
            UiTargetKind.Role => this.Name is null
                ? $"Target.Role({Quote(this.Role)})"
                : $"Target.Role({Quote(this.Role)}, {Quote(this.Name)})",
            UiTargetKind.Section => $"Target.Section({Quote(this.Name)})",
            UiTargetKind.TestId => $"Target.TestId({Quote(this.Name)})",
            UiTargetKind.Css => $"Target.Css({Quote(this.Css)})",
            UiTargetKind.ElementId => $"Target.Id({Quote(this.Name)})",
            _ => Quote(this.Name),
        };

        if (this.Scope is { Kind: UiTargetKind.Section, Name: { } sectionName })
        {
            head += $".InSection({Quote(sectionName)})";
        }
        else if (this.Scope is not null)
        {
            head += $".Within({this.Scope.ToSourceCode()})";
        }

        if (this.NearText is not null)
        {
            head += $".Near({Quote(this.NearText)})";
        }

        if (this.Exact)
        {
            head += ".ExactMatch()";
        }

        if (this.Index is { } index)
        {
            head += string.Format(CultureInfo.InvariantCulture, ".Nth({0})", index);
        }

        if (this.AllowFirstOfMany)
        {
            head += ".First()";
        }

        return head;
    }

    /// <summary>
    /// Returns <see cref="Describe"/>.
    /// </summary>
    /// <returns>The description.</returns>
    public override string ToString() => this.Describe();

    private static string Quote(string? value)
        => value is null ? "null" : $"\"{value.Replace("\"", "\\\"", StringComparison.Ordinal)}\"";
}
