using System.Globalization;
using TestFramework.UI.Browser.Targeting;

namespace TestFramework.UI.Browser.Resolution;

/// <summary>
/// One concrete lookup on a page: this channel, this text, this strictness, optionally inside this
/// other lookup.
/// </summary>
/// <remarks>
/// A spec is the boundary between deciding and doing. The resolver produces specs and judges how many
/// elements each one finds; only the browser adapter turns a spec into a real locator. That split is
/// what lets the whole matching ladder be tested without a browser, and it keeps the ladder honest -
/// it can only ask questions the adapter can answer.
/// </remarks>
/// <param name="Channel">The way of looking.</param>
/// <param name="Text">The name, label, placeholder, text or test id being looked for.</param>
/// <param name="Exact">True to require a full-string match rather than a fragment.</param>
/// <param name="Role">The ARIA role, when <paramref name="Channel"/> is
/// <see cref="UiMatchChannel.Role"/>.</param>
/// <param name="Css">The selector, when <paramref name="Channel"/> is
/// <see cref="UiMatchChannel.Css"/>.</param>
/// <param name="NearText">Text the element must sit near, or null.</param>
/// <param name="Within">The lookup whose single match this one searches inside, or null for the whole
/// page.</param>
/// <param name="WithinIndex">Which match of <paramref name="Within"/> to search inside.</param>
internal sealed record UiQuerySpec(
    UiMatchChannel Channel,
    string? Text,
    bool Exact,
    string? Role = null,
    string? Css = null,
    string? NearText = null,
    UiQuerySpec? Within = null,
    int WithinIndex = 0)
{
    /// <summary>
    /// How this lookup reads in a failure message that lists what was tried.
    /// </summary>
    /// <returns>The description, for example <c>role=button (exact)</c>.</returns>
    public string Describe()
    {
        string head = this.Channel switch
        {
            UiMatchChannel.Role => $"role={this.Role}",
            UiMatchChannel.Label => "label",
            UiMatchChannel.Placeholder => "placeholder",
            UiMatchChannel.TestId => "test id",
            UiMatchChannel.Text => "text",
            UiMatchChannel.Css => $"selector '{this.Css}'",
            UiMatchChannel.ElementId => "element id",
            _ => this.Channel.ToString(),
        };

        // A selector has no loose form, so labelling one is noise rather than information.
        string strictness = this.Channel is UiMatchChannel.Css or UiMatchChannel.ElementId or UiMatchChannel.TestId
            ? string.Empty
            : this.Exact ? " (exact)" : " (loose)";

        string scope = this.Within is null
            ? string.Empty
            : string.Format(CultureInfo.InvariantCulture, " inside {0}", this.Within.Describe());

        return head + strictness + scope;
    }

    /// <summary>
    /// The name this channel reports when it is the one that matched, as it appears in a session
    /// entry and in a resilience assertion.
    /// </summary>
    /// <returns>The name, for example <c>RoleExact</c> or <c>LabelLoose</c>.</returns>
    public string DescribeMatch()
        => this.Channel is UiMatchChannel.Css or UiMatchChannel.ElementId or UiMatchChannel.TestId
            ? this.Channel.ToString()
            : this.Channel + (this.Exact ? "Exact" : "Loose");
}
