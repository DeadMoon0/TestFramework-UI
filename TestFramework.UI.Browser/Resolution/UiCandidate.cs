namespace TestFramework.UI.Browser.Resolution;

/// <summary>
/// One of several elements that answered to the same name, described well enough for a test author to
/// tell them apart without opening the page.
/// </summary>
public sealed record UiCandidate
{
    internal UiCandidate(int index, string snippet, string? accessibleName = null, string? sectionName = null, string? testId = null)
    {
        this.Index = index;
        this.Snippet = snippet;
        this.AccessibleName = accessibleName;
        this.SectionName = sectionName;
        this.TestId = testId;
    }

    /// <summary>
    /// The zero-based position among the matches, so it can be named with <c>.Nth(index)</c>.
    /// </summary>
    public int Index { get; }

    /// <summary>
    /// A short excerpt of the element's markup.
    /// </summary>
    public string Snippet { get; }

    /// <summary>
    /// The name assistive technology would announce, when it has one.
    /// </summary>
    public string? AccessibleName { get; }

    /// <summary>
    /// The accessible name of the region, group or form containing it, when it sits in one. This is usually what tells two identical buttons apart.
    /// </summary>
    public string? SectionName { get; }

    /// <summary>
    /// The element's test id, when it carries one.
    /// </summary>
    public string? TestId { get; }

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
