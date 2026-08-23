using System.Collections.Generic;
using TestFramework.Core.Steps;
using TestFramework.UI.Session;

namespace TestFramework.UI.Browser.Steps;

/// <summary>
/// What one browser step did.
/// </summary>
/// <remarks>
/// Plain data, because a step result travels to the debugging UI over a pipe. The whole session's story
/// lives in the session variable; this is only what this step contributed, which is what
/// <c>run.Step(label)</c> is asked about.
/// </remarks>
/// <param name="App">The application this step drove.</param>
/// <param name="Url">The address the page was on when the step finished.</param>
/// <param name="Title">The page's title when the step finished.</param>
/// <param name="Entries">What the step did, in order.</param>
/// <param name="FailureBundlePath">The folder holding the evidence of a failure, when the step failed.</param>
public sealed record UiFlowResultContext(
    string App,
    string Url,
    string Title,
    IReadOnlyList<UiSessionEntry> Entries,
    string? FailureBundlePath = null) : StepResultContext
{
    /// <summary>
    /// Returns a readable summary of the step.
    /// </summary>
    /// <returns>The summary.</returns>
    public override string ToString()
        => $"{this.Entries.Count} action(s) on '{this.App}', ending at {this.Url}";
}
