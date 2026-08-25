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
            await UiFailureBundle
                .CaptureAsync(
                    session,
                    UiFailureBundle.DirectoryFor(runState, observation.Label, session.App, observation.Attempt),
                    PictureOf(run.Variables, session.App),
                    run.Logger)
                .ConfigureAwait(false);
        }

        await HoldAsync(sessions, run).ConfigureAwait(false);
    }

    /// <summary>
    /// Keeps the browser open on the failure, when the machine asked for it.
    /// </summary>
    /// <remarks>
    /// Deliberately unbounded: a person set an environment variable in order to look at the page, so the
    /// run waits until they are done. The engine says out loud that an observer is holding it, which is the
    /// notice that makes this legible rather than a hang.
    /// </remarks>
    private static async Task HoldAsync(IReadOnlyList<UiSession> sessions, RunContext run)
    {
        if (!UiEnvironmentOverrides.PauseOnFailure || sessions.Count == 0)
        {
            return;
        }

        run.Logger.LogWarning(
            "The browser is being held open on the failure because {0} is set. Inspect the page, then let the run continue.",
            UiEnvironmentOverrides.PauseOnFailureVariable);

        foreach (UiSession session in sessions)
        {
            await session.Page.PauseAsync().ConfigureAwait(false);
        }
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
