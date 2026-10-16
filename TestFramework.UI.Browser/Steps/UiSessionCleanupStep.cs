using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Logging;
using TestFramework.Core.Steps;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Variables;
using TestFramework.UI.Browser.Runtime;

namespace TestFramework.UI.Browser.Steps;

/// <summary>
/// Closes the browser contexts a run opened.
/// </summary>
/// <remarks>
/// <para>
/// Runs in the cleanup stage, which a run reaches whether its steps passed, failed or were cancelled.
/// Contexts are what a run owns - its cookies, its storage - so they close here; the browsers themselves
/// stay in the process pool, because starting one costs far more than the next run should have to pay.
/// </para>
/// <para>
/// A context that will not close is worth a warning and nothing more. The run's verdict was decided by
/// its steps, and a tidy-up problem must not overturn it.
/// </para>
/// </remarks>
internal sealed class UiSessionCleanupStep : Step<EmptyStepResultContext>
{
    /// <inheritdoc />
    public override string Name => "UI Session Cleanup";

    /// <inheritdoc />
    public override string Description => "Closes the browser sessions this run opened";

    /// <inheritdoc />
    public override bool DoesReturn => false;

    /// <inheritdoc />
    public override Step<EmptyStepResultContext> Clone() => new UiSessionCleanupStep().WithClonedOptions(this);

    /// <inheritdoc />
    public override StepInstance<Step<EmptyStepResultContext>, EmptyStepResultContext> GetInstance()
        => new StepInstance<Step<EmptyStepResultContext>, EmptyStepResultContext>(this);

    /// <inheritdoc />
    public override void DeclareIO(StepIOContract contract)
    {
    }

    /// <inheritdoc />
    public override async Task<EmptyStepResultContext?> Execute(RunContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        UiRunState runState = UiRunState.For(context.Variables);
        IReadOnlyList<UiSession> sessions = runState.OpenSessions();
        ScopedLogger logger = context.Logger;

        try
        {
            await CloseEachAsync(
                sessions,
                async session =>
                {
                    await session.DisposeAsync().ConfigureAwait(false);
                    logger?.LogInformation("Closed the browser session for '{0}'.", session.App);
                },
                (session, exception) => logger?.LogWarning("Could not close the browser session for '{0}': {1}", session.App, exception.Message))
                .ConfigureAwait(false);
        }
        finally
        {
            runState.Clear();
        }

        return EmptyStepResultContext.Instance;
    }

    /// <summary>
    /// Closes every session, whichever of them fails to close.
    /// </summary>
    /// <remarks>
    /// A close that throws is reported and the next one still runs. A cancelled close is reported the same
    /// way and then rethrown once every other session has had its turn: stopping at the first cancellation
    /// left the rest of the run's contexts open in the pooled browser, holding their cookies and storage
    /// until the process ended.
    /// </remarks>
    /// <typeparam name="TSession">What is closed.</typeparam>
    /// <param name="sessions">The sessions to close.</param>
    /// <param name="close">Closes one session.</param>
    /// <param name="reportFailure">Hears about a session that did not close.</param>
    /// <returns>A task that completes once every session has been tried.</returns>
    /// <exception cref="OperationCanceledException">A close was cancelled; every other one was still tried.</exception>
    internal static async Task CloseEachAsync<TSession>(
        IReadOnlyList<TSession> sessions,
        Func<TSession, Task> close,
        Action<TSession, Exception> reportFailure)
    {
        ArgumentNullException.ThrowIfNull(sessions);
        ArgumentNullException.ThrowIfNull(close);
        ArgumentNullException.ThrowIfNull(reportFailure);

        OperationCanceledException? cancellation = null;

        foreach (TSession session in sessions)
        {
            try
            {
                await close(session).ConfigureAwait(false);
            }
            catch (OperationCanceledException exception)
            {
                cancellation ??= exception;
                reportFailure(session, exception);
            }
            catch (Exception exception)
            {
                reportFailure(session, exception);
            }
        }

        if (cancellation is not null)
        {
            ExceptionDispatchInfo.Throw(cancellation);
        }
    }
}
