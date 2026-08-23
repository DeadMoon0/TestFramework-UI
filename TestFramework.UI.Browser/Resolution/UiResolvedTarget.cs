namespace TestFramework.UI.Browser.Resolution;

/// <summary>
/// The outcome of looking for a target: which lookup found it, which of the matches to act on, and
/// how good that match was.
/// </summary>
/// <param name="Spec">The lookup that found the element.</param>
/// <param name="Index">Which of that lookup's matches to act on, zero-based.</param>
/// <param name="Rank">How far down the ladder the match came from, where 0 is the strongest channel
/// of the exact sweep. A test that only passes at rank 3 is passing because the framework guessed
/// well, and this number is what makes that visible.</param>
/// <param name="CandidateCount">How many elements the lookup found. More than one means the test
/// allowed a choice to be made.</param>
/// <param name="Snippet">A short excerpt of the element that matched.</param>
internal sealed record UiResolvedTarget(
    UiQuerySpec Spec,
    int Index,
    int Rank,
    int CandidateCount,
    string? Snippet)
{
    /// <summary>
    /// True when the run matched something other than what the test literally said: a fuzzy match on the
    /// text, or a choice made among several candidates.
    /// </summary>
    /// <remarks>
    /// Deliberately not "a weaker channel matched". A field that only has a placeholder is named by its
    /// placeholder, and matching it there exactly is precise - the test said the right thing and the page
    /// agreed. Reporting that as loose would fill an audit with entries nobody can act on, and an audit
    /// that cries wolf is one a suite learns to ignore. <see cref="Rank"/> remains available for a team
    /// that does want to hold its pages to the strongest channel.
    /// </remarks>
    public bool IsLoose => !this.Spec.Exact || this.CandidateCount > 1;

    /// <summary>
    /// The channel name to record in the session picture.
    /// </summary>
    /// <returns>The name, for example <c>RoleExact</c>.</returns>
    public string DescribeMatch() => this.Spec.DescribeMatch();
}
