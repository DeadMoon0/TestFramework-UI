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
