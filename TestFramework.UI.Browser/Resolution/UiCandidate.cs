namespace TestFramework.UI.Browser.Resolution;

/// <summary>
/// One of several elements that answered to the same name, described well enough for a test author to
/// tell them apart without opening the page.
/// </summary>
/// <param name="Index">The zero-based position among the matches, so it can be named with
/// <c>.Nth(index)</c>.</param>
/// <param name="Snippet">A short excerpt of the element's markup.</param>
/// <param name="AccessibleName">The name assistive technology would announce, when it has one.</param>
/// <param name="SectionName">The accessible name of the region, group or form containing it, when it
/// sits in one. This is usually what tells two identical buttons apart.</param>
/// <param name="TestId">The element's test id, when it carries one.</param>
public sealed record UiCandidate(
    int Index,
    string Snippet,
    string? AccessibleName = null,
    string? SectionName = null,
    string? TestId = null)
{
    /// <summary>
    /// Renders the candidate as one line of a failure message.
    /// </summary>
    /// <returns>The line.</returns>
    public override string ToString()
    {
        string section = this.SectionName is null ? string.Empty : $" in section '{this.SectionName}'";
        string testId = this.TestId is null ? string.Empty : $" [test id '{this.TestId}']";

        return this.Snippet + section + testId;
    }
}
