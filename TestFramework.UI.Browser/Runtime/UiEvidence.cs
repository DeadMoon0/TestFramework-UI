using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Playwright;
using Newtonsoft.Json;
using TestFramework.Core.Debugger;
using TestFramework.Core.Logging;
using TestFramework.Core.Steps;
using TestFramework.UI.Session;

namespace TestFramework.UI.Browser.Runtime;

/// <summary>
/// Turns what the browser is showing into evidence the run keeps.
/// </summary>
/// <remarks>
/// <para>
/// A failure on a build server is only as useful as what it left behind: the picture, the markup, the
/// story of the session and the page's own complaints — enough to attach to a ticket, and enough to
/// tell "the test looked for the wrong thing" apart from "the application was broken".
/// </para>
/// <para>
/// All of it goes through the run's own widget channel. What this replaced wrote the same four files
/// into a directory of its own, named after a timestamp and a fresh identifier — which shared no key
/// with the run that produced them, so nothing could find them afterwards and no tool could be told
/// where to look without this package and that tool agreeing privately.
/// </para>
/// <para>
/// Every part of it is best effort. A failing screenshot must never replace the failure it was meant to
/// explain, so nothing here is allowed to throw.
/// </para>
/// </remarks>
internal static class UiEvidence
{
    /// <summary>
    /// How long a capture waits for the page to be free before giving up on it.
    /// </summary>
    /// <remarks>
    /// Evidence is taken from outside the step that was driving the page — after a failure, or between
    /// actions — so another step in the same layer may be holding the gate. Waiting is right; waiting
    /// forever would turn a screenshot into a hang, which is the one thing evidence must never cost.
    /// </remarks>
    private static readonly TimeSpan GateWait = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Photographs a session, and records it against the step that is running.
    /// </summary>
    /// <param name="session">The session to photograph.</param>
    /// <param name="run">The run, for its widget channel and its log.</param>
    /// <param name="name">What the picture is of.</param>
    /// <param name="takeGate">
    /// Whether to take the session's gate. False only when the caller already holds it — a step
    /// photographing its own page mid-flow — because it is not reentrant.
    /// </param>
    /// <returns>Where it was written, or null when it could not be taken.</returns>
    public static async Task<string?> ScreenshotAsync(UiSession session, RunContext run, string name, bool takeGate = true)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(run);

        byte[]? png = await ShootAsync(session, run, takeGate).ConfigureAwait(false);

        if (png is null)
            return null;

        return run.Widgets.Publish(new Widget
        {
            Kind = WidgetKinds.Screenshot,
            Name = name,
            Form = DebugPreviewForm.Image,
            Bytes = png,
            Summary = $"{session.App}: {name}",
            Badges = [session.App]
        })?.Path;
    }

    /// <summary>
    /// Records everything worth keeping about a session that has just gone wrong.
    /// </summary>
    /// <remarks>
    /// Four widgets rather than one folder, because they are four different things and a reader wants
    /// them separately: the picture is looked at, the markup is searched, the session story is read in
    /// order, and the console is scanned for the error nobody noticed.
    /// </remarks>
    /// <returns>Where the picture was written, or null when there was none to take.</returns>
    public static async Task<string?> CaptureFailureAsync(UiSession session, RunContext run, UiSessionPicture picture, string label)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(run);

        string prefix = $"{Safe(label)}-{Safe(session.App)}";

        string? shot = await ScreenshotAsync(session, run, prefix).ConfigureAwait(false);

        await KeepMarkupAsync(session, run, prefix).ConfigureAwait(false);

        KeepStory(run, picture, prefix, session.App);
        KeepConsole(run, picture, prefix, session.App);

        return shot;
    }

    /// <summary>Takes the picture, holding the page still while it happens.</summary>
    private static async Task<byte[]?> ShootAsync(UiSession session, RunContext run, bool takeGate)
    {
        bool held = false;

        try
        {
            if (takeGate)
            {
                held = await session.Gate.WaitAsync(GateWait, run.Deadline.Token).ConfigureAwait(false);

                if (!held)
                {
                    // Said out loud rather than passed over: a missing picture with no explanation reads
                    // as a step that never ran, which is a worse story than the true one.
                    run.Logger.LogWarning(
                        "The page for '{0}' was busy, so no picture of it was taken.",
                        session.App);

                    return null;
                }
            }

            return await session.Page.ScreenshotAsync(new PageScreenshotOptions { FullPage = true }).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is PlaywrightException or ObjectDisposedException or OperationCanceledException)
        {
            // A page that has already gone cannot be photographed, and a run being cancelled is not the
            // moment to start failing over evidence.
            return null;
        }
        finally
        {
            if (held)
                session.Gate.Release();
        }
    }

    private static async Task KeepMarkupAsync(UiSession session, RunContext run, string prefix)
    {
        try
        {
            string html = await session.Page.ContentAsync().ConfigureAwait(false);

            run.Widgets.Publish(new Widget
            {
                Kind = WidgetKinds.Document,
                Name = prefix + "-page",
                Form = DebugPreviewForm.Markup,
                Text = html,
                Summary = $"The markup {session.App} was showing",
                Badges = [session.App]
            });
        }
        catch (Exception exception) when (exception is PlaywrightException or ObjectDisposedException)
        {
        }
    }

    private static void KeepStory(RunContext run, UiSessionPicture picture, string prefix, string app)
    {
        try
        {
            run.Widgets.Publish(new Widget
            {
                Kind = WidgetKinds.Document,
                Name = prefix + "-session",
                Form = DebugPreviewForm.Json,
                Text = JsonConvert.SerializeObject(picture, Formatting.Indented),
                Summary = $"What {app} was asked to do, in order",
                Badges = [app]
            });
        }
        catch (JsonException)
        {
            // A picture that cannot be serialised costs the widget, never the run.
        }
    }

    private static void KeepConsole(RunContext run, UiSessionPicture picture, string prefix, string app)
    {
        IReadOnlyList<string> errors = picture.ConsoleErrors();

        if (errors.Count == 0)
            return;

        StringBuilder builder = new StringBuilder();

        foreach (string error in errors)
            builder.AppendLine(CultureInfo.InvariantCulture, $"{error}");

        run.Widgets.Publish(new Widget
        {
            Kind = WidgetKinds.LogStream,
            Name = prefix + "-console",
            Form = DebugPreviewForm.Text,
            Text = builder.ToString(),
            Summary = errors.Count == 1 ? "One console error" : $"{errors.Count} console errors",
            Badges = [app]
        });
    }

    /// <summary>
    /// A name that survives being a file name.
    /// </summary>
    /// <remarks>
    /// Bounded as well as sanitised: a step label is a sentence a test author wrote, and the run's own
    /// store trims what it is given — so trimming here keeps the name it trims to readable.
    /// </remarks>
    private static string Safe(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "unnamed";

        StringBuilder builder = new StringBuilder(value.Length);

        foreach (char character in value)
        {
            builder.Append(char.IsLetterOrDigit(character) || character is '-' or '_' or '.'
                ? char.ToLowerInvariant(character)
                : '-');
        }

        string safe = builder.ToString().Trim('-');

        return safe.Length switch
        {
            0 => "unnamed",
            > 32 => safe[..32].Trim('-'),
            _ => safe
        };
    }
}
