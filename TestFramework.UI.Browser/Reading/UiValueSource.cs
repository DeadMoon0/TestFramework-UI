using System;
using TestFramework.UI.Browser.Targeting;

namespace TestFramework.UI.Browser.Reading;

/// <summary>
/// The kinds of value a page can be asked for.
/// </summary>
public enum UiValueKind
{
    /// <summary>An element's visible text.</summary>
    Text = 0,

    /// <summary>What a form control currently holds - its value, not its label.</summary>
    FieldValue,

    /// <summary>Whether a checkbox, radio button or switch is on.</summary>
    Checked,

    /// <summary>How many elements answer to a target.</summary>
    Count,

    /// <summary>One attribute of an element.</summary>
    Attribute,

    /// <summary>Which option a list control currently shows.</summary>
    SelectedOption,

    /// <summary>The address the page is on.</summary>
    Url,

    /// <summary>One parameter of the page's query string.</summary>
    QueryParam,

    /// <summary>One entry of the page's local storage.</summary>
    LocalStorage,

    /// <summary>One computed style property of an element.</summary>
    Style,
}

/// <summary>
/// One value a test wants off the page, described rather than fetched.
/// </summary>
/// <remarks>
/// <para>
/// Built where the timeline is built and read when the step runs, like everything else in a flow. Each
/// source has one natural type - text is a string, a tick is a bool, a count is an int - and that is the
/// type the variable receives. Anything richer is a parsing decision the test should state itself, with
/// the timeline's own <c>Transform</c>: turning <c>"€129.00"</c> into a number involves a culture, and a
/// framework that guessed one would be wrong quietly.
/// </para>
/// <para>
/// Reads are snapshots. "Wait until the total says €156" is an expectation and belongs to
/// <c>Expect</c> or a wait event; a read answers what the page says now, at its place in the flow.
/// </para>
/// </remarks>
public sealed record UiValueSource
{
    private UiValueSource(UiValueKind kind, UiTarget? target, string? argument)
    {
        this.Kind = kind;
        this.Target = target;
        this.Argument = argument;
    }

    /// <summary>What kind of value this is.</summary>
    public UiValueKind Kind { get; }

    /// <summary>The element it is read from, when it is read from one.</summary>
    public UiTarget? Target { get; }

    /// <summary>The attribute name, query parameter or storage key, for the kinds that take one.</summary>
    public string? Argument { get; }

    /// <summary>
    /// The .NET type this source produces: <see cref="bool"/> for a tick, <see cref="int"/> for a count,
    /// <see cref="string"/> for everything else.
    /// </summary>
    public Type ValueType => this.Kind switch
    {
        UiValueKind.Checked => typeof(bool),
        UiValueKind.Count => typeof(int),
        _ => typeof(string),
    };

    /// <summary>
    /// How this source reads in a trace or a failure message.
    /// </summary>
    /// <returns>The description, for example <c>value of field 'Email'</c>.</returns>
    public string Describe() => this.Kind switch
    {
        UiValueKind.Text => $"text of {this.Target!.Describe()}",
        UiValueKind.FieldValue => $"value of {this.Target!.Describe()}",
        UiValueKind.Checked => $"whether {this.Target!.Describe()} is checked",
        UiValueKind.Count => $"how many match {this.Target!.Describe()}",
        UiValueKind.Attribute => $"attribute '{this.Argument}' of {this.Target!.Describe()}",
        UiValueKind.SelectedOption => $"selected option of {this.Target!.Describe()}",
        UiValueKind.Url => "the page address",
        UiValueKind.QueryParam => $"query parameter '{this.Argument}'",
        UiValueKind.LocalStorage => $"local storage '{this.Argument}'",
        UiValueKind.Style => $"style '{this.Argument}' of {this.Target!.Describe()}",
        _ => this.Kind.ToString(),
    };

    /// <summary>
    /// Returns <see cref="Describe"/>.
    /// </summary>
    /// <returns>The description.</returns>
    public override string ToString() => this.Describe();

    internal static UiValueSource OfElement(UiValueKind kind, UiTarget target, string? argument = null)
    {
        ArgumentNullException.ThrowIfNull(target);

        return new UiValueSource(kind, target, argument);
    }

    internal static UiValueSource OfPage(UiValueKind kind, string? argument = null)
        => new UiValueSource(kind, null, argument);
}

/// <summary>
/// The values a flow can read off the page.
/// </summary>
/// <remarks>
/// <c>Read(source, into)</c> on a browser flow takes any of these and writes the result to an ordinary timeline variable, in the type the source produces.
/// </remarks>
public static class Value
{
    /// <summary>An element's visible text, whitespace-normalized.</summary>
    /// <param name="target">The element.</param>
    /// <returns>The source.</returns>
    public static UiValueSource Text(UiTarget target) => UiValueSource.OfElement(UiValueKind.Text, target);

    /// <summary>
    /// What a form control currently holds.
    /// </summary>
    /// <remarks>
    /// The control's value, which its visible text is not: an input's text is empty however much has been
    /// typed into it. Works on the native form controls; anything else fails with a pointer to
    /// <see cref="Text"/>.
    /// </remarks>
    /// <param name="field">The control.</param>
    /// <returns>The source.</returns>
    public static UiValueSource FieldValue(UiTarget field) => UiValueSource.OfElement(UiValueKind.FieldValue, field);

    /// <summary>Whether a checkbox, radio button or switch is on. Produces a <see cref="bool"/>.</summary>
    /// <param name="target">The control.</param>
    /// <returns>The source.</returns>
    public static UiValueSource Checked(UiTarget target) => UiValueSource.OfElement(UiValueKind.Checked, target);

    /// <summary>
    /// How many elements answer to a target. Produces an <see cref="int"/>; zero is an answer, not a
    /// failure.
    /// </summary>
    /// <remarks>
    /// Counted on the first lookup channel that finds anything, in the same order resolution tries them -
    /// so what gets counted is what the same target would act on. The <c>Nth</c> and <c>First</c> dials
    /// pick one match and therefore have no meaning here.
    /// </remarks>
    /// <param name="target">What to count.</param>
    /// <returns>The source.</returns>
    public static UiValueSource Count(UiTarget target) => UiValueSource.OfElement(UiValueKind.Count, target);

    /// <summary>One attribute of an element. A missing attribute fails, naming the ones it does carry.</summary>
    /// <param name="target">The element.</param>
    /// <param name="name">The attribute name.</param>
    /// <returns>The source.</returns>
    public static UiValueSource Attribute(UiTarget target, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return UiValueSource.OfElement(UiValueKind.Attribute, target, name);
    }

    /// <summary>
    /// Which option a list control currently shows: a native list's selected label, or what an
    /// ARIA combobox displays.
    /// </summary>
    /// <param name="field">The list control.</param>
    /// <returns>The source.</returns>
    public static UiValueSource SelectedOption(UiTarget field) => UiValueSource.OfElement(UiValueKind.SelectedOption, field);

    /// <summary>The address the page is on.</summary>
    /// <returns>The source.</returns>
    public static UiValueSource Url() => UiValueSource.OfPage(UiValueKind.Url);

    /// <summary>
    /// One parameter of the page's query string. A parameter the address does not carry fails, naming
    /// the ones it does.
    /// </summary>
    /// <param name="name">The parameter name.</param>
    /// <returns>The source.</returns>
    public static UiValueSource QueryParam(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return UiValueSource.OfPage(UiValueKind.QueryParam, name);
    }

    /// <summary>
    /// One computed style property of an element, as the browser resolved it.
    /// </summary>
    /// <remarks>
    /// The raw escape hatch of the layout checks, and ranked below them for the same reason raw
    /// selectors rank below roles: a computed value is the page's implementation, and a test pinning
    /// <c>rgb(168, 71, 28)</c> fails on the next restyle whether a person could tell or not. Reach for
    /// it when the property IS the requirement - and note that colours come back resolved, as
    /// <c>rgb(...)</c>, whatever notation the stylesheet used.
    /// </remarks>
    /// <param name="target">The element.</param>
    /// <param name="property">The CSS property name, for example <c>background-color</c>.</param>
    /// <returns>The source.</returns>
    public static UiValueSource Style(UiTarget target, string property)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(property);

        return UiValueSource.OfElement(UiValueKind.Style, target, property);
    }

    /// <summary>
    /// One entry of the page's local storage. A key the page does not hold fails, naming the ones it
    /// does.
    /// </summary>
    /// <param name="key">The storage key.</param>
    /// <returns>The source.</returns>
    public static UiValueSource LocalStorage(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        return UiValueSource.OfPage(UiValueKind.LocalStorage, key);
    }
}
