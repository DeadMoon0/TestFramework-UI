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
/// <param name="MatchRank">How far down the ladder the match came from, where 0 is the strongest
/// channel. Null when the action had no target. A weaker channel is not by itself a problem - a field
/// with only a placeholder is named by its placeholder - so this is for diagnosis rather than for the
/// audit.</param>
/// <param name="MatchWasFuzzy">True when the text matched loosely rather than exactly: the run found
/// something other than what the test literally said. This, together with
/// <paramref name="CandidateCount"/>, is what an audit reports on.</param>
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
    bool MatchWasFuzzy,
    int CandidateCount,
    string? MatchedSnippet,
    string? Detail,
    string Url,
    IReadOnlyList<string> ConsoleErrors,
    double DurationMs)
{
    /// <summary>
    /// True when the run matched something other than what the test literally said - the text matched
    /// loosely, or one of several candidates was chosen.
    /// </summary>
    public bool IsLooseMatch => this.MatchWasFuzzy || this.CandidateCount > 1;

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
