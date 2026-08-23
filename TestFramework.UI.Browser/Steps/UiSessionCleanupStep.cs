using System;
using System.Collections.Generic;
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
    public override async Task<EmptyStepResultContext?> Execute(
        IServiceProvider serviceProvider,
        VariableStore variableStore,
        ArtifactStore artifactStore,
        ScopedLogger logger,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(variableStore);

        UiRunState runState = UiRunState.For(variableStore);
        IReadOnlyList<UiSession> sessions = runState.OpenSessions();

        foreach (UiSession session in sessions)
        {
            try
            {
                await session.DisposeAsync().ConfigureAwait(false);

                logger?.LogInformation("Closed the browser session for '{0}'.", session.App);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger?.LogWarning("Could not close the browser session for '{0}': {1}", session.App, exception.Message);
            }
        }

        runState.Clear();

        return EmptyStepResultContext.Instance;
    }
}
