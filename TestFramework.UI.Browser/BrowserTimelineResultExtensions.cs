using System;
using System.Collections.Generic;
using System.Linq;
using TestFramework.Core.Timelines;
using TestFramework.Core.Timelines.Assertions;
using TestFramework.UI.Browser.Identifier;
using TestFramework.UI.Browser.Steps;
using TestFramework.UI.Session;

namespace TestFramework.UI.Browser;

/// <summary>
/// What a finished run can be asked about its browser sessions.
/// </summary>
/// <remarks>
/// The session picture is the source for most of these, because the interesting questions are about the
/// session rather than about one step: where the run ended up, what it read, and whether it had to guess
/// at anything along the way.
/// </remarks>
public static class BrowserTimelineResultExtensions
{
    /// <summary>
    /// Everything that happened in one application's session.
    /// </summary>
    /// <param name="run">The finished run.</param>
    /// <param name="app">The application.</param>
    /// <returns>The session picture.</returns>
    public static ValueHandle<UiSessionPicture> UiSession(this TimelineRun run, WebAppIdentifier app)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(app);

        return run.Assert(SessionOf(run, app), $"the UI session of '{app}'");
    }

    /// <summary>
    /// The address the session ended on.
    /// </summary>
    /// <param name="run">The finished run.</param>
    /// <param name="app">The application.</param>
    /// <returns>The address.</returns>
    public static ValueHandle<string> UiUrl(this TimelineRun run, WebAppIdentifier app)
    {
        ArgumentNullException.ThrowIfNull(run);

        return run.Assert(SessionOf(run, app).Url, $"the address '{app}' ended on");
    }

    /// <summary>
    /// The title of the page the session ended on.
    /// </summary>
    /// <param name="run">The finished run.</param>
    /// <param name="app">The application.</param>
    /// <returns>The title.</returns>
    public static ValueHandle<string> UiTitle(this TimelineRun run, WebAppIdentifier app)
    {
        ArgumentNullException.ThrowIfNull(run);

        return run.Assert(SessionOf(run, app).Title, $"the title '{app}' ended on");
    }

    /// <summary>
    /// Everything the session did, or everything one step did.
    /// </summary>
    /// <param name="run">The finished run.</param>
    /// <param name="app">The application.</param>
    /// <param name="stepLabel">A step to narrow to, or null for the whole session.</param>
    /// <returns>The entries, oldest first.</returns>
    public static ValueHandle<IReadOnlyList<UiSessionEntry>> UiTrace(
        this TimelineRun run,
        WebAppIdentifier app,
        string? stepLabel = null)
    {
        ArgumentNullException.ThrowIfNull(run);

        UiSessionPicture picture = SessionOf(run, app);
        IReadOnlyList<UiSessionEntry> entries = stepLabel is null ? picture.Entries : picture.For(stepLabel);

        return run.Assert(entries, stepLabel is null ? $"what '{app}' did" : $"what '{stepLabel}' did");
    }

    /// <summary>
    /// Every match the run had to reach for: found through a weaker channel than the strongest, or chosen
    /// from among several candidates.
    /// </summary>
    /// <remarks>
    /// The audit a suite asserts on to keep tolerance honest. Asserting this is empty says the test passed
    /// because the page was what it claimed to be, not because the framework guessed well - and it says it
    /// without the test having to know which channel matched what.
    /// </remarks>
    /// <param name="run">The finished run.</param>
    /// <param name="app">The application.</param>
    /// <returns>The loose matches.</returns>
    public static ValueHandle<IReadOnlyList<UiSessionEntry>> UiLooseMatches(this TimelineRun run, WebAppIdentifier app)
    {
        ArgumentNullException.ThrowIfNull(run);

        return run.Assert(SessionOf(run, app).LooseMatches(), $"the matches '{app}' had to reach for");
    }

    /// <summary>
    /// Every script the session ran - the uses of the escape hatch.
    /// </summary>
    /// <remarks>
    /// A script bypasses what the verbs guarantee, so how many a suite runs is worth watching the same
    /// way loose matches are: <c>run.UiScripts("shop").Should().HaveNoItems()</c> is a suite saying its
    /// tests speak only in what a person could do.
    /// </remarks>
    /// <param name="run">The finished run.</param>
    /// <param name="app">The application.</param>
    /// <returns>The script actions, in the order they ran.</returns>
    public static ValueHandle<IReadOnlyList<UiSessionEntry>> UiScripts(this TimelineRun run, WebAppIdentifier app)
    {
        ArgumentNullException.ThrowIfNull(run);

        IReadOnlyList<UiSessionEntry> scripts = SessionOf(run, app).Entries
            .Where(static entry => entry.Action is nameof(UiActionKind.Execute) or nameof(UiActionKind.Evaluate))
            .ToList();

        return run.Assert(scripts, $"the scripts '{app}' ran");
    }

    /// <summary>
    /// How the loosest match of the session was found, or null when nothing was matched loosely.
    /// </summary>
    /// <param name="run">The finished run.</param>
    /// <param name="app">The application.</param>
    /// <returns>The channel name, for example <c>RoleLoose</c>, or null.</returns>
    public static ValueHandle<string?> UiWeakestMatch(this TimelineRun run, WebAppIdentifier app)
    {
        ArgumentNullException.ThrowIfNull(run);

        return run.Assert(SessionOf(run, app).WeakestMatch()?.ResolvedVia, $"the loosest match '{app}' relied on");
    }

    /// <summary>
    /// The file a screenshot was written to.
    /// </summary>
    /// <param name="run">The finished run.</param>
    /// <param name="app">The application.</param>
    /// <param name="name">The name the screenshot was taken under.</param>
    /// <returns>The path, or null when no screenshot of that name was taken.</returns>
    public static ValueHandle<string?> UiScreenshot(this TimelineRun run, WebAppIdentifier app, string name)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        string? path = SessionOf(run, app).Entries
            .Where(static entry => string.Equals(entry.Action, nameof(UiActionKind.Screenshot), StringComparison.Ordinal))
            .Select(static entry => entry.Detail)
            .LastOrDefault(detail => detail is not null && detail.Contains(name, StringComparison.OrdinalIgnoreCase));

        return run.Assert(path, $"the '{name}' screenshot of '{app}'");
    }

    /// <summary>
    /// Everything the application itself complained about while the run drove it.
    /// </summary>
    /// <param name="run">The finished run.</param>
    /// <param name="app">The application.</param>
    /// <returns>The console errors and page exceptions.</returns>
    public static ValueHandle<IReadOnlyList<string>> UiConsoleErrors(this TimelineRun run, WebAppIdentifier app)
    {
        ArgumentNullException.ThrowIfNull(run);

        return run.Assert(SessionOf(run, app).ConsoleErrors(), $"what '{app}' reported as broken");
    }

    /// <summary>
    /// The result of one browser step, with a message naming the step when it produced something else.
    /// </summary>
    /// <param name="handle">The step handle.</param>
    /// <returns>The step's result.</returns>
    public static UiFlowResultContext UiResult(this StepHandle handle)
    {
        ArgumentNullException.ThrowIfNull(handle);

        return handle.LastResult.Result as UiFlowResultContext
            ?? throw new InvalidOperationException(
                $"The step did not produce a browser result. It produced {Describe(handle.LastResult.Result)}.");
    }

    /// <summary>
    /// How a structure or table comparison differed from what the test expected.
    /// </summary>
    /// <remarks>
    /// A comparison step already fails on its own when the page does not match, so this is for a test that
    /// wants to say something more specific about the differences than "there were none".
    /// </remarks>
    /// <param name="run">The finished run.</param>
    /// <param name="label">The comparison step's label.</param>
    /// <returns>The differences, empty when the page matched.</returns>
    public static ValueHandle<IReadOnlyList<string>> UiDifferences(this TimelineRun run, string label)
    {
        ArgumentNullException.ThrowIfNull(run);

        UiCompareResultContext result = Result<UiCompareResultContext>(run, label, "a structure or table comparison");

        return run.Assert(result.Differences, $"how '{label}' differed");
    }

    /// <summary>
    /// What the page actually was, as the comparison saw it.
    /// </summary>
    /// <param name="run">The finished run.</param>
    /// <param name="label">The comparison step's label.</param>
    /// <returns>The rendered structure or table.</returns>
    public static ValueHandle<string> UiActualStructure(this TimelineRun run, string label)
    {
        ArgumentNullException.ThrowIfNull(run);

        UiCompareResultContext result = Result<UiCompareResultContext>(run, label, "a structure or table comparison");

        return run.Assert(result.Actual, $"what '{label}' found on the page");
    }

    /// <summary>
    /// The rows a table step read, each keyed by column name.
    /// </summary>
    /// <remarks>
    /// Keyed rather than positional, so an assertion that says <c>row["Price"]</c> keeps working when a
    /// column is inserted - which is the whole reason this is not a list of arrays.
    /// </remarks>
    /// <param name="run">The finished run.</param>
    /// <param name="label">The table step's label.</param>
    /// <returns>The rows.</returns>
    public static ValueHandle<IReadOnlyList<IReadOnlyDictionary<string, string>>> UiTable(this TimelineRun run, string label)
    {
        ArgumentNullException.ThrowIfNull(run);

        UiTableResultContext result = Result<UiTableResultContext>(run, label, "a table read");

        return run.Assert(result.Rows, $"the rows '{label}' read");
    }

    /// <summary>
    /// One column of a table a step read.
    /// </summary>
    /// <param name="run">The finished run.</param>
    /// <param name="label">The table step's label.</param>
    /// <param name="column">The column's header text.</param>
    /// <returns>The column's values, top to bottom.</returns>
    public static ValueHandle<IReadOnlyList<string>> UiColumn(this TimelineRun run, string label, string column)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentException.ThrowIfNullOrWhiteSpace(column);

        UiTableResultContext result = Result<UiTableResultContext>(run, label, "a table read");

        IReadOnlyList<string> values = result.Rows
            .Select(row => row.TryGetValue(column, out string? value) ? value : string.Empty)
            .ToList();

        return run.Assert(values, $"the '{column}' column '{label}' read");
    }

    /// <summary>
    /// The structure a capture step recorded.
    /// </summary>
    /// <param name="run">The finished run.</param>
    /// <param name="label">The capture step's label.</param>
    /// <returns>The recorded structure.</returns>
    public static ValueHandle<string> UiCapturedStructure(this TimelineRun run, string label)
    {
        ArgumentNullException.ThrowIfNull(run);

        UiCaptureResultContext result = Result<UiCaptureResultContext>(run, label, "a structure capture");

        return run.Assert(result.Structure, $"the structure '{label}' recorded");
    }

    /// <summary>
    /// What a structure or table comparison step found.
    /// </summary>
    /// <param name="handle">The step handle.</param>
    /// <returns>The comparison result.</returns>
    public static UiCompareResultContext UiCompare(this StepHandle handle)
    {
        ArgumentNullException.ThrowIfNull(handle);

        return handle.LastResult.Result as UiCompareResultContext
            ?? throw new InvalidOperationException(
                $"The step is not a comparison. It produced {Describe(handle.LastResult.Result)}.");
    }

    /// <summary>
    /// What a table step read.
    /// </summary>
    /// <param name="handle">The step handle.</param>
    /// <returns>The table result.</returns>
    public static UiTableResultContext UiTableResult(this StepHandle handle)
    {
        ArgumentNullException.ThrowIfNull(handle);

        return handle.LastResult.Result as UiTableResultContext
            ?? throw new InvalidOperationException(
                $"The step is not a table read. It produced {Describe(handle.LastResult.Result)}.");
    }

    /// <summary>
    /// What a capture step recorded.
    /// </summary>
    /// <param name="handle">The step handle.</param>
    /// <returns>The capture result.</returns>
    public static UiCaptureResultContext UiCapture(this StepHandle handle)
    {
        ArgumentNullException.ThrowIfNull(handle);

        return handle.LastResult.Result as UiCaptureResultContext
            ?? throw new InvalidOperationException(
                $"The step is not a structure capture. It produced {Describe(handle.LastResult.Result)}.");
    }

    private static TResult Result<TResult>(TimelineRun run, string label, string what)
        where TResult : class
        => run.Step(label).LastResult.Result as TResult
            ?? throw new InvalidOperationException(
                $"The step '{label}' is not {what}. It produced {Describe(run.Step(label).LastResult.Result)}.");

    private static UiSessionPicture SessionOf(TimelineRun run, WebAppIdentifier app)
    {
        string identifier = UiSessionVariable.For(app);

        return run.VariableStore.TryGetVariable(identifier, out UiSessionPicture? picture) && picture is not null
            ? picture
            : UiSessionPicture.Empty(app);
    }

    private static string Describe(object? result)
        => result is null ? "nothing" : $"a {result.GetType().Name}";
}
