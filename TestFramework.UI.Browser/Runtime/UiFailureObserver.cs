using System.Linq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TestFramework.Core.Steps;
using TestFramework.Core.Variables;
using TestFramework.UI.Session;

namespace TestFramework.UI.Browser.Runtime;

/// <summary>
/// Photographs the browser when a step goes wrong, and holds it open when asked to.
/// </summary>
/// <remarks>
/// <para>
/// This used to be a try/catch inside every step that drove a page - the flow, the wait, the inspection -
/// three copies of the same idea, each slightly different, and the next verb would have been a fourth.
/// None of it was ever the step's job: a step's job is to do the thing and say what went wrong with it.
/// Whether a failure is worth a screenshot is a question about the run, so the engine asks it, once, of
/// whoever is listening.
/// </para>
/// <para>
/// Failing and running out of time are both worth evidence, and are separate hooks because they are
/// separate things - an element that never appeared and an element that appeared wrong want the same
/// photograph but not the same explanation. An exception the timeline was told to expect reaches neither
/// hook, which is right: a tolerated exception did not fail anything, and a folder of evidence for each one
/// would bury the failures worth opening.
/// </para>
/// <para>
/// Every session the run has open is photographed, not a guessed single one. A timeline driving a shop and
/// an admin console has two pages, and the step label alone does not say which of them the failure was
/// about - so the answer is both, in folders named after the application.
/// </para>
/// </remarks>
internal sealed class UiFailureObserver : IStepObserver
{
    /// <inheritdoc />
    public Task OnStepStartingAsync(StepObservation observation, RunContext run) => Task.CompletedTask;

    /// <inheritdoc />
    public Task OnStepFailedAsync(StepObservation observation, Exception exception, RunContext run) => CaptureAsync(observation, run);

    /// <inheritdoc />
    public Task OnStepTimedOutAsync(StepObservation observation, RunContext run) => CaptureAsync(observation, run);

    private static async Task CaptureAsync(StepObservation observation, RunContext run)
    {
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentNullException.ThrowIfNull(run);

        // Asked rather than created: an observer watches every step of every run, and most runs never open
        // a browser at all. Nothing to photograph is the normal answer.
        if (!run.State.TryGet(out UiRunState? runState) || runState is null)
        {
            return;
        }

        IReadOnlyList<UiSession> sessions = runState.OpenSessions();

        foreach (UiSession session in sessions)
        {
            await UiEvidence
                .CaptureFailureAsync(session, run, PictureOf(run.Variables, session.App), observation.Label)
                .ConfigureAwait(false);
        }

        await HoldAsync(runState, sessions, run).ConfigureAwait(false);
    }

    /// <summary>
    /// Keeps the browser open on the failure, when the machine asked for it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Deliberately unbounded: a person set an environment variable in order to look at the page, so the
    /// run waits until they are done. The engine says out loud that an observer is holding it, which is the
    /// notice that makes this legible rather than a hang.
    /// </para>
    /// <para>
    /// Recorded on the run for the length of the hold, because a page stopped in the inspector cannot be
    /// photographed — it waits for the same person. Anything that would take a picture from outside the
    /// step has to be able to find that out, and the run is the only thing both of them hold.
    /// </para>
    /// </remarks>
    private static async Task HoldAsync(UiRunState runState, IReadOnlyList<UiSession> sessions, RunContext run)
    {
        switch (PlanHold(UiEnvironmentOverrides.PauseOnFailure, [.. sessions.Select(static session => session.Config.EffectiveHeadless)]))
        {
            case UiHoldPlan.Nothing:
                return;

            case UiHoldPlan.HeadlessOnly:
                run.Logger.LogWarning(
                    "{0} is set, but every browser here runs headless, where there is no window to hold open. Set {1}=1 as well to look at the page.",
                    UiEnvironmentOverrides.PauseOnFailureVariable,
                    UiEnvironmentOverrides.HeadedVariable);
                return;
        }

        run.Logger.LogWarning(
            "The browser is being held open on the failure because {0} is set. Inspect the page, then let the run continue.",
            UiEnvironmentOverrides.PauseOnFailureVariable);

        using IDisposable hold = runState.HoldForInspection();

        foreach (UiSession session in sessions.Where(static session => !session.Config.EffectiveHeadless))
        {
            await session.Page.PauseAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Whether a failure holds the browser open, given what was asked and how each browser runs.
    /// </summary>
    /// <remarks>
    /// Playwright's pause returns at once in a headless browser, so a hold there held nothing while the log said
    /// the browser was being held open. Headed browsers are held; headless-only says why it cannot.
    /// </remarks>
    /// <param name="requested">Whether the pause-on-failure variable is set.</param>
    /// <param name="headless">Whether each open session's browser runs headless.</param>
    /// <returns>What to do.</returns>
    internal static UiHoldPlan PlanHold(bool requested, IReadOnlyList<bool> headless)
    {
        if (!requested || headless.Count == 0)
            return UiHoldPlan.Nothing;

        return headless.All(static isHeadless => isHeadless) ? UiHoldPlan.HeadlessOnly : UiHoldPlan.Hold;
    }

    /// <summary>
    /// The story of a session so far, which the step that failed has already written.
    /// </summary>
    /// <remarks>
    /// Read from the run's variables rather than passed in, because the picture is the step's declared
    /// output and a step records it before it throws. Evidence gathering reads what the run says happened;
    /// it does not get its own version of it.
    /// </remarks>
    private static UiSessionPicture PictureOf(VariableStore variables, string app)
        => variables.TryGetVariable(UiSessionVariable.For(app), out UiSessionPicture? picture) && picture is not null
            ? picture
            : UiSessionPicture.Empty(app);
}

/// <summary>
/// What a failure does with the browser when pause-on-failure is asked for.
/// </summary>
internal enum UiHoldPlan
{
    /// <summary>Not asked for, or nothing is open.</summary>
    Nothing,

    /// <summary>Every open browser is headless: there is no window to hold, and the run says so.</summary>
    HeadlessOnly,

    /// <summary>The headed browsers are held open until the person lets the run continue.</summary>
    Hold,
}
