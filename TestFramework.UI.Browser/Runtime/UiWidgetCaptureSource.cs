using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TestFramework.Core.Steps;
using TestFramework.UI.Session;

namespace TestFramework.UI.Browser.Runtime;

/// <summary>
/// Photographs the browser when someone watching the run asks to see it.
/// </summary>
/// <remarks>
/// <para>
/// The companion to <see cref="UiFailureObserver"/>, and the difference is who decides when. The
/// observer is told a step failed and photographs what it was looking at; this is asked, and the
/// moment it is asked is the moment a step is held at a breakpoint — when the page on screen is one
/// nothing has photographed, because the step that navigated there has not finished yet.
/// </para>
/// <para>
/// Every session the run has open, not a guessed single one, for the reason the observer gives: a
/// timeline driving a shop and an admin console has two pages, and the reader asked to see the run
/// rather than one of them.
/// </para>
/// </remarks>
internal sealed class UiWidgetCaptureSource : IWidgetCaptureSource
{
    /// <inheritdoc />
    public async Task CaptureAsync(RunContext run)
    {
        ArgumentNullException.ThrowIfNull(run);

        // Asked rather than created. Most runs never open a browser, and the answer for those is
        // nothing to show - which the run reports as a capture that produced nothing.
        if (!run.State.TryGet(out UiRunState? runState) || runState is null)
        {
            return;
        }

        // A page stopped in Playwright's own inspector does not answer a screenshot request, it
        // waits - and it waits for a person, so the wait has no bound worth having. Said out loud
        // because the reason travels to whoever asked: the run's log is what they are watching.
        if (runState.IsHeldForInspection)
        {
            run.Logger.LogWarning(
                "The browser is being held open for inspection, so it cannot be photographed. Let the hold go first.");

            return;
        }

        IReadOnlyList<UiSession> sessions = runState.OpenSessions();

        foreach (UiSession session in sessions)
        {
            // Named for the application rather than for the moment. Two captures of one page differ
            // in their content, so the run's own store keeps them as versions of one name, in the
            // order they were asked for - which is what a reader stepping through a run wants to
            // read them as.
            await UiEvidence.ScreenshotAsync(session, run, $"live-{session.App}").ConfigureAwait(false);
        }
    }
}
