using System.Globalization;
using TestFramework.Core.Steps;

namespace TestFramework.UI.Browser.Events;

/// <summary>
/// What a wait waited for, and how long it took.
/// </summary>
public sealed record UiWaitResultContext : StepResultContext
{
    internal UiWaitResultContext(string app, string url, string waited, string? resolvedVia, double waitedForMs, int polls)
    {
        this.App = app;
        this.Url = url;
        this.Waited = waited;
        this.ResolvedVia = resolvedVia;
        this.WaitedForMs = waitedForMs;
        this.Polls = polls;
    }

    /// <summary>
    /// The application that was watched.
    /// </summary>
    public string App { get; }

    /// <summary>
    /// The address the page was on when the wait was over.
    /// </summary>
    public string Url { get; }

    /// <summary>
    /// What was waited for, in the words the test used.
    /// </summary>
    public string Waited { get; }

    /// <summary>
    /// Which lookup channel answered, when the wait was for an element.
    /// </summary>
    public string? ResolvedVia { get; }

    /// <summary>
    /// How long the wait took.
    /// </summary>
    public double WaitedForMs { get; }

    /// <summary>
    /// How many times the page was asked.
    /// </summary>
    public int Polls { get; }

    /// <summary>
    /// Returns a readable summary.
    /// </summary>
    /// <returns>The summary.</returns>
    public override string ToString()
        => string.Format(
            CultureInfo.InvariantCulture,
            "{0} on '{1}' after {2:F1}s ({3} poll(s))",
            this.Waited,
            this.App,
            this.WaitedForMs / 1000,
            this.Polls);
}
