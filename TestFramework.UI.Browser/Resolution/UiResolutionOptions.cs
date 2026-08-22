namespace TestFramework.UI.Browser.Resolution;

/// <summary>
/// The dials that apply to every lookup in one application's session.
/// </summary>
/// <remarks>
/// Passed in rather than read from a static, so a step can tighten or loosen matching for itself and
/// the resolver stays a pure decision over the answers a page gives.
/// </remarks>
/// <param name="AmbiguityMode">What to do when several elements answer to the same name.</param>
/// <param name="MaxCandidatesReported">How many candidates a failure message lists at most.</param>
/// <param name="MaxNamesSuggested">How many of the page's own names a not-found failure lists at
/// most.</param>
internal sealed record UiResolutionOptions(
    UiAmbiguityMode AmbiguityMode = UiAmbiguityMode.Strict,
    int MaxCandidatesReported = 10,
    int MaxNamesSuggested = 15)
{
    /// <summary>
    /// The options a run uses when nothing configured otherwise.
    /// </summary>
    public static UiResolutionOptions Default { get; } = new UiResolutionOptions();
}
