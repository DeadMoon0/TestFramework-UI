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
    /// True when the match came from something other than the strongest channel, or when the lookup
    /// found more than one element - the two cases a resilience audit reports on.
    /// </summary>
    public bool IsLoose => this.Rank > 0 || this.CandidateCount > 1;

    /// <summary>
    /// The channel name to record in the session picture.
    /// </summary>
    /// <returns>The name, for example <c>RoleExact</c>.</returns>
    public string DescribeMatch() => this.Spec.DescribeMatch();
}
