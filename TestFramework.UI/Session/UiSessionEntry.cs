using System.Collections.Generic;
using System.Globalization;

namespace TestFramework.UI.Session;

/// <summary>
/// One thing that happened in a UI session: an action performed, an expectation checked, a value read.
/// </summary>
/// <remarks>
/// Plain data on purpose. Entries travel to the debugging UI over a pipe and into the run's value
/// files, so nothing here may be a live handle - no page, no element, no delegate. A rule that ran is
/// represented by its description, an element that matched by a short snippet of its markup.
/// </remarks>
/// <param name="StepLabel">The label of the step that produced this entry, or the step's name when it
/// was not labelled.</param>
/// <param name="Action">The action performed, in the tense the DSL reads: <c>Click</c>, <c>Fill</c>,
/// <c>Expect</c>.</param>
/// <param name="Target">How the target was described in the test, for example
/// <c>button 'Place order'</c>. Null for actions without a target, such as navigation.</param>
/// <param name="ResolvedVia">The channel that actually matched, for example <c>RoleExact</c> or
/// <c>LabelFuzzy</c>. Null when the action had no target.</param>
/// <param name="MatchRank">How strong that match was, where 0 is the strongest channel and larger
/// numbers are progressively looser. Null when the action had no target. This is what makes a test
/// that only passes through loose matching visible rather than merely green.</param>
/// <param name="CandidateCount">How many elements the matching channel found. Greater than one means
/// the run picked among candidates, which only happens when the test allowed it.</param>
/// <param name="MatchedSnippet">A short excerpt of the element that matched, for reading a trace
/// without reopening the page.</param>
/// <param name="Detail">What the action did or found: the text filled (redacted when sensitive), the
/// value read, the path of a screenshot.</param>
/// <param name="Url">The address the page was on when this happened.</param>
/// <param name="ConsoleErrors">Browser console errors and page exceptions observed during this
/// action. A page that is broken says so here, so a crash is not mistaken for a bad locator.</param>
/// <param name="DurationMs">How long the action took, in milliseconds.</param>
public sealed record UiSessionEntry(
    string StepLabel,
    string Action,
    string? Target,
    string? ResolvedVia,
    int? MatchRank,
    int CandidateCount,
    string? MatchedSnippet,
    string? Detail,
    string Url,
    IReadOnlyList<string> ConsoleErrors,
    double DurationMs)
{
    /// <summary>
    /// True when this entry's target was found through something other than the strongest channel, or
    /// when more than one element matched - the two cases a resilience audit cares about.
    /// </summary>
    public bool IsLooseMatch => (this.MatchRank is > 0) || this.CandidateCount > 1;

    /// <summary>
    /// Renders the entry as one readable line for a log or a failure message.
    /// </summary>
    /// <returns>The line.</returns>
    public override string ToString()
    {
        string target = this.Target is null ? string.Empty : $" {this.Target}";
        string via = this.ResolvedVia is null ? string.Empty : $" via {this.ResolvedVia}";
        string candidates = this.CandidateCount > 1
            ? string.Format(CultureInfo.InvariantCulture, " ({0} candidates)", this.CandidateCount)
            : string.Empty;
        string detail = this.Detail is null ? string.Empty : $" -> {this.Detail}";

        return string.Format(
            CultureInfo.InvariantCulture,
            "{0}{1}{2}{3}{4} [{5:F0} ms]",
            this.Action,
            target,
            via,
            candidates,
            detail,
            this.DurationMs);
    }
}
