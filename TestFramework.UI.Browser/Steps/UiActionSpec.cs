using TestFramework.Core.Variables;
using TestFramework.UI.Browser.Reading;
using TestFramework.UI.Browser.Scripting;
using TestFramework.UI.Browser.Resolution;
using TestFramework.UI.Browser.Targeting;

namespace TestFramework.UI.Browser.Steps;

/// <summary>
/// What a flow does, one entry per verb the test wrote.
/// </summary>
public enum UiActionKind
{
    /// <summary>Go to an address.</summary>
    Navigate = 0,

    /// <summary>Press something.</summary>
    Click,

    /// <summary>Type into something.</summary>
    Fill,

    /// <summary>Choose from a native list by value or label.</summary>
    Select,

    /// <summary>Choose from whatever kind of list control the page has.</summary>
    Choose,

    /// <summary>Tick something.</summary>
    Check,

    /// <summary>Untick something.</summary>
    Uncheck,

    /// <summary>Press keys.</summary>
    Press,

    /// <summary>Move the pointer onto something.</summary>
    Hover,

    /// <summary>Wait until something is there, and fail if it never is.</summary>
    Expect,

    /// <summary>Wait until something is gone, and fail if it never goes.</summary>
    ExpectNot,

    /// <summary>Read a value off the page into a variable.</summary>
    Read,

    /// <summary>Photograph the page.</summary>
    Screenshot,

    /// <summary>Run JavaScript for its effect, ignoring what it returns.</summary>
    Execute,

    /// <summary>Run JavaScript and keep what it returns in a variable.</summary>
    Evaluate,
}

/// <summary>
/// One action of a flow, as written rather than as performed.
/// </summary>
/// <remarks>
/// Values stay as variable references until the step runs, so a flow declared in a static field can be
/// filled from whatever the run happens to know.
/// </remarks>
/// <param name="Kind">What to do.</param>
/// <param name="Target">What to do it to, or null for actions without a target.</param>
/// <param name="Value">The text to type, option to choose, address to visit or keys to press.</param>
/// <param name="CaptureName">The variable a read writes, or the name a screenshot is filed under.</param>
/// <param name="Sensitive">True when the value must never appear in a log, a trace or a failure message.</param>
/// <param name="Source">What a read reads, for the read action.</param>
/// <param name="Script">The JavaScript, for the script actions.</param>
/// <param name="ResultBinder">Writes an evaluation's result in the type the test asked for.</param>
internal sealed record UiActionSpec(
    UiActionKind Kind,
    UiTarget? Target = null,
    VariableReference<string>? Value = null,
    string? CaptureName = null,
    bool Sensitive = false,
    UiValueSource? Source = null,
    JsScript? Script = null,
    IUiScriptResultBinder? ResultBinder = null)
{
    /// <summary>
    /// What a plain string target means for this verb.
    /// </summary>
    public UiSmartContext Context => this.Kind switch
    {
        UiActionKind.Click or UiActionKind.Hover => UiSmartContext.Clickable,
        UiActionKind.Fill or UiActionKind.Select or UiActionKind.Choose => UiSmartContext.Fillable,
        UiActionKind.Check or UiActionKind.Uncheck => UiSmartContext.Checkable,
        UiActionKind.Read => this.Source?.Kind switch
        {
            UiValueKind.FieldValue or UiValueKind.SelectedOption => UiSmartContext.Fillable,
            UiValueKind.Checked => UiSmartContext.Checkable,
            _ => UiSmartContext.Text,
        },
        _ => UiSmartContext.Text,
    };

    /// <summary>
    /// How this action reads in a trace or a failure message.
    /// </summary>
    /// <returns>The description.</returns>
    public string Describe() => this switch
    {
        { Kind: UiActionKind.Read, Source: { } source } => $"Read {source.Describe()}",
        { Kind: UiActionKind.Execute or UiActionKind.Evaluate, Script: { } script } =>
            this.Target is { } scriptTarget
                ? $"{this.Kind} script '{script.Name}' on {scriptTarget.Describe()}"
                : $"{this.Kind} script '{script.Name}'",
        { Target: { } target } => $"{this.Kind} {target.Describe()}",
        _ => this.Kind.ToString(),
    };
}
