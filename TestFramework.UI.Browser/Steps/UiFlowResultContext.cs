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
public sealed record UiFlowResultContext : StepResultContext
{
    internal UiFlowResultContext(string app, string url, string title, IReadOnlyList<UiSessionEntry> entries)
    {
        this.App = app;
        this.Url = url;
        this.Title = title;
        this.Entries = entries;
    }

    /// <summary>
    /// The application this step drove.
    /// </summary>
    public string App { get; }

    /// <summary>
    /// The address the page was on when the step finished.
    /// </summary>
    public string Url { get; }

    /// <summary>
    /// The page's title when the step finished.
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// What the step did, in order.
    /// </summary>
    public IReadOnlyList<UiSessionEntry> Entries { get; }

    /// <summary>
    /// Returns a readable summary of the step.
    /// </summary>
    /// <returns>The summary.</returns>
    public override string ToString()
        => $"{this.Entries.Count} action(s) on '{this.App}', ending at {this.Url}";
}
