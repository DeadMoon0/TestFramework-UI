using System;
using System.Collections.Generic;
using System.Linq;

namespace TestFramework.UI.Browser.Session;

/// <summary>
/// Everything that happened in one application's UI session during a run, in order.
/// </summary>
/// <remarks>
/// <para>
/// This is how a UI session reaches the assertions. It is deliberately a variable rather than an
/// artifact: an artifact carries a lifecycle - something creates it, something tears it down - and
/// what happened in a session has neither. It is data the run accumulates, and the variable store is
/// where a run keeps data.
/// </para>
/// <para>
/// Every UI step declares the session variable as both input and output, so each step receives the
/// picture painted so far and hands on a longer one. That also gives the planner an ordering it can
/// see: steps touching the same application follow each other, while steps on different applications
/// stay free to run side by side.
/// </para>
/// </remarks>
public sealed record UiSessionPicture
{
    internal UiSessionPicture(string app, string url, string title, IReadOnlyList<UiSessionEntry> entries)
    {
        this.App = app;
        this.Url = url;
        this.Title = title;
        this.Entries = entries;
    }

    /// <summary>
    /// The application identifier this session belongs to.
    /// </summary>
    public string App { get; }

    /// <summary>
    /// The address the page is on now.
    /// </summary>
    public string Url { get; internal init; }

    /// <summary>
    /// The page's title now.
    /// </summary>
    public string Title { get; internal init; }

    /// <summary>
    /// What happened, oldest first.
    /// </summary>
    public IReadOnlyList<UiSessionEntry> Entries { get; internal init; }

    /// <summary>
    /// The picture of a session that has not done anything yet.
    /// </summary>
    /// <param name="app">The application identifier.</param>
    /// <returns>An empty picture.</returns>
    public static UiSessionPicture Empty(string app)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(app);

        return new UiSessionPicture(app, string.Empty, string.Empty, Array.Empty<UiSessionEntry>());
    }

    /// <summary>
    /// Adds what a step just did, and moves the current address and title along with it.
    /// </summary>
    /// <param name="entries">The entries to append, oldest first.</param>
    /// <param name="url">The address the page is on after those entries.</param>
    /// <param name="title">The page's title after those entries.</param>
    /// <returns>The longer picture. The original is unchanged.</returns>
    public UiSessionPicture Add(IEnumerable<UiSessionEntry> entries, string url, string title)
    {
        ArgumentNullException.ThrowIfNull(entries);

        List<UiSessionEntry> combined = new List<UiSessionEntry>(this.Entries);
        combined.AddRange(entries);

        return this with { Url = url, Title = title, Entries = combined };
    }

    /// <summary>
    /// The entries a single step contributed.
    /// </summary>
    /// <param name="stepLabel">The step label to filter by.</param>
    /// <returns>That step's entries, oldest first.</returns>
    public IReadOnlyList<UiSessionEntry> For(string stepLabel)
        => this.Entries.Where(entry => string.Equals(entry.StepLabel, stepLabel, StringComparison.Ordinal)).ToList();

    /// <summary>
    /// The loosest match the session relied on, or null when every target was found through the
    /// strongest channel.
    /// </summary>
    /// <remarks>
    /// A suite can assert on this to keep tolerance honest: matching stays forgiving while a test is
    /// being written, and a build can still refuse to go green on tests that only pass because the
    /// framework guessed well.
    /// </remarks>
    /// <returns>The weakest entry, or null when nothing matched loosely.</returns>
    public UiSessionEntry? WeakestMatch()
        => this.LooseMatches()
            .OrderByDescending(static entry => entry.MatchWasFuzzy)
            .ThenByDescending(static entry => entry.MatchRank ?? 0)
            .ThenByDescending(static entry => entry.CandidateCount)
            .FirstOrDefault();

    /// <summary>
    /// Every match the run had to reach for: found through something other than the strongest channel,
    /// or chosen from among several candidates.
    /// </summary>
    /// <remarks>
    /// The primitive an audit asserts on. Asserting that this is empty says "this test passes because
    /// the page is what it claims to be, not because the framework guessed well" - and it says it
    /// without the test having to know which channel each element was found through.
    /// </remarks>
    /// <returns>The loose matches, oldest first.</returns>
    public IReadOnlyList<UiSessionEntry> LooseMatches()
        => this.Entries.Where(static entry => entry.IsLooseMatch).ToList();

    /// <summary>
    /// Every browser console error and page exception seen during the session, in order.
    /// </summary>
    /// <returns>The messages.</returns>
    public IReadOnlyList<string> ConsoleErrors()
        => this.Entries.SelectMany(static entry => entry.ConsoleErrors).ToList();

    /// <summary>
    /// Renders the whole session as numbered lines, for a failure message or a log.
    /// </summary>
    /// <returns>The rendered session.</returns>
    public override string ToString()
    {
        if (this.Entries.Count == 0)
        {
            return $"UI session '{this.App}': nothing happened yet.";
        }

        IEnumerable<string> lines = this.Entries
            .Select((entry, index) => $"  {index + 1}. {entry}");

        return $"UI session '{this.App}' at {this.Url}:{Environment.NewLine}{string.Join(Environment.NewLine, lines)}";
    }
}
