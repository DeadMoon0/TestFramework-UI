using System;
using System.Globalization;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Environment;
using TestFramework.Core.Events;
using TestFramework.Core.Logging;
using TestFramework.Core.Steps;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Variables;
using TestFramework.UI.Browser.Configuration;
using TestFramework.UI.Browser.Identifier;
using TestFramework.UI.Browser.Resolution;
using TestFramework.UI.Browser.Runtime;
using TestFramework.UI.Session;

namespace TestFramework.UI.Browser.Events;

/// <summary>
/// What one poll of the page found.
/// </summary>
/// <param name="Satisfied">True when the wait is over.</param>
/// <param name="ResolvedVia">Which lookup channel answered, when the wait is for an element.</param>
internal sealed record UiProbeOutcome(bool Satisfied, string? ResolvedVia = null);

/// <summary>
/// The shared shape of a wait on a page: something a timeline's <c>WaitForEvent</c> watches for.
/// </summary>
/// <remarks>
/// <para>
/// A wait event is for the state another actor produces - a payment provider redirecting back, a
/// background job's result appearing, another step of the timeline writing what the page then shows. A
/// flow's own <c>Expect</c> covers "the page caught up with what I just did"; the event form exists so a
/// timeline can put the waiting BETWEEN steps, where the actor being waited on is visible in the plan.
/// </para>
/// <para>
/// Each poll is one short, non-throwing look; the loop owns the waiting, and the step's timeout bounds
/// it. The event gives up slightly before that timeout on purpose, so its own message - what was watched,
/// where the page was, and the evidence bundle - is the one the reader gets rather than the runner's
/// generic one.
/// </para>
/// </remarks>
/// <typeparam name="TEvent">The concrete event type.</typeparam>
public abstract class UiEvent<TEvent> : SequentialEvent<TEvent, UiWaitResultContext>, IHasEnvironmentRequirements, IHasCleanupStep
    where TEvent : UiEvent<TEvent>
{
    private readonly WebAppIdentifier app;
    private readonly VariableReference<TimeSpan> pollDelay;

    private UiSession? session;
    private PlaywrightElementQuery? query;
    private UiResolutionOptions? resolutionOptions;
    private Stopwatch? clock;
    private int polls;

    private protected UiEvent(WebAppIdentifier app, VariableReference<TimeSpan>? pollDelay)
    {
        ArgumentNullException.ThrowIfNull(app);

        this.app = app;
        this.pollDelay = pollDelay ?? TimeSpan.FromMilliseconds(500);
    }

    /// <inheritdoc />
    public override bool DoesReturn => true;

    /// <summary>The application being watched.</summary>
    protected WebAppIdentifier App => this.app;

    /// <summary>The delay between polls, for a clone.</summary>
    private protected VariableReference<TimeSpan> PollDelay => this.pollDelay;

    /// <summary>How this wait is named in the session story.</summary>
    private protected abstract string ActionName { get; }

    /// <summary>What is being waited for, in the words the test used.</summary>
    private protected abstract string DescribeWaited(VariableStore variableStore);

    /// <summary>What a reader should check when this wait never ends.</summary>
    private protected abstract string TimeoutAdvice(VariableStore variableStore);

    /// <summary>
    /// Called once before the first poll of an execution, for a wait that keeps state between polls -
    /// a baseline, a last-seen value - so a rerun starts clean.
    /// </summary>
    private protected virtual void OnPollingStarting()
    {
    }

    /// <summary>
    /// Looks at the page once. Must not throw for a page that simply is not there yet.
    /// </summary>
    private protected abstract Task<UiProbeOutcome> ProbeAsync(
        UiSession session,
        PlaywrightElementQuery query,
        UiResolutionOptions options,
        VariableStore variableStore,
        CancellationToken cancellationToken);

    /// <inheritdoc />
    public IReadOnlyCollection<EnvironmentRequirement> GetEnvironmentRequirements(VariableStore variableStore)
        => [this.app.ExternalRequirement ?? new EnvironmentRequirement(BrowserEnvironmentResourceKinds.WebApp, this.app)];

    /// <inheritdoc />
    public StepGeneric? CreateCleanupStep(VariableStore variableStore)
    {
        ArgumentNullException.ThrowIfNull(variableStore);

        return UiRunState.For(variableStore).TryClaimCleanup() ? new Steps.UiSessionCleanupStep() : null;
    }

    /// <inheritdoc />
    public override void DeclareIO(StepIOContract contract)
    {
        ArgumentNullException.ThrowIfNull(contract);

        if (this.pollDelay.Identifier is { } delayIdentifier)
        {
            contract.Inputs.Add(new StepIOEntry(delayIdentifier.Identifier, StepIOKind.Variable, true, typeof(TimeSpan)));
        }

        this.DeclareOwnIO(contract);

        // The same variable the flows write, which is what keeps a wait ordered between the steps of its
        // application's session instead of floating free in the plan.
        contract.Outputs.Add(new StepIOEntry(
            UiSessionVariable.For(this.app),
            StepIOKind.Variable,
            true,
            typeof(UiSessionPicture)));
    }

    /// <summary>
    /// Declares whatever else this wait reads.
    /// </summary>
    /// <param name="contract">The contract to add to.</param>
    private protected virtual void DeclareOwnIO(StepIOContract contract)
    {
    }

    /// <inheritdoc />
    public override async Task<UiWaitResultContext?> DoEventPolling(
        IServiceProvider serviceProvider,
        VariableStore variableStore,
        ArtifactStore artifactStore,
        ScopedLogger logger,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ArgumentNullException.ThrowIfNull(variableStore);
        ArgumentNullException.ThrowIfNull(logger);

        WebAppConfig config = UiEnvironmentOverrides.Apply(UiConfigResolver.Resolve(serviceProvider, this.app));
        UiRunState runState = UiRunState.For(variableStore);

        this.session = await serviceProvider
            .GetUIComponentFactory()
            .SessionAsync(this.app, config, runState, cancellationToken)
            .ConfigureAwait(false);

        this.query = new PlaywrightElementQuery(this.session.Page, config.TestIdAttribute, config.DefaultActionTimeout);
        this.resolutionOptions = new UiResolutionOptions(config.AmbiguityMode);
        this.clock = Stopwatch.StartNew();
        this.polls = 0;
        this.OnPollingStarting();

        string waited = this.DescribeWaited(variableStore);
        logger.LogInformation("Waiting for {0} on '{1}'.", waited, this.app.ToString());

        // The step has to give up marginally before its own timeout to say anything useful - the runner
        // abandons the task the instant the timeout token fires, and an exception raised at that same
        // moment is never observed.
        using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        TimeSpan ownDeadline = UiEventDeadline.For(this.TimeOutOptions.TimeOut.GetValue(variableStore));

        if (ownDeadline > TimeSpan.Zero)
        {
            deadline.CancelAfter(ownDeadline);
        }

        try
        {
            UiWaitResultContext? result = await base
                .DoEventPolling(serviceProvider, variableStore, artifactStore, logger, deadline.Token)
                .ConfigureAwait(false);

            if (result is not null)
            {
                this.Record(variableStore, result);
            }

            return result;
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            UiSessionPicture picture = this.Picture(variableStore);
            variableStore.SetVariable(UiSessionVariable.For(this.app), picture);

            string? bundle = await UiFailureBundle
                .CaptureAsync(this.session, runState, this.LabelOptions.Label ?? this.Name, picture, logger)
                .ConfigureAwait(false);

            // Invariant formatting, because a failure message must read the same on every machine that
            // reproduces it - a German runner printing "2,6s" is a diff in every comparison of two logs.
            throw new TimeoutException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{Capitalize(waited)} never happened on '{this.app}'. The page was at {this.session.Page.Url} ") +
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"after {this.clock.Elapsed.TotalSeconds:F1}s and {this.polls} poll(s). {this.TimeoutAdvice(variableStore)}") +
                (bundle is null ? string.Empty : $"\nScreenshot, markup and session story: {bundle}"),
                exception);
        }
    }

    /// <inheritdoc />
    public override async Task<SequentialPollingResult<UiWaitResultContext>> OnSequentialPolling(
        IServiceProvider serviceProvider,
        VariableStore variableStore,
        ArtifactStore artifactStore,
        ScopedLogger logger,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(variableStore);

        this.polls++;

        // The page must not be probed while a flow is driving it - the runner is free to reach a wait and
        // a flow of another application at the same time, and the gate is per session.
        await this.session!.Gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        UiProbeOutcome outcome;

        try
        {
            outcome = await this
                .ProbeAsync(this.session, this.query!, this.resolutionOptions!, variableStore, cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            this.session.Gate.Release();
        }

        if (!outcome.Satisfied)
        {
            return new SequentialPollingResult<UiWaitResultContext>(false, null, this.pollDelay.GetValue(variableStore));
        }

        return new SequentialPollingResult<UiWaitResultContext>(
            true,
            new UiWaitResultContext(
                this.app,
                this.session.Page.Url,
                this.DescribeWaited(variableStore),
                outcome.ResolvedVia,
                this.clock!.Elapsed.TotalMilliseconds,
                this.polls),
            TimeSpan.Zero);
    }

    private void Record(VariableStore variableStore, UiWaitResultContext result)
    {
        UiSessionEntry entry = new UiSessionEntry(
            this.LabelOptions.Label ?? this.Name,
            this.ActionName,
            result.Waited,
            result.ResolvedVia,
            null,
            false,
            0,
            null,
            string.Create(CultureInfo.InvariantCulture, $"after {result.WaitedForMs / 1000:F1}s ({result.Polls} poll(s))"),
            result.Url,
            this.session!.DrainConsoleErrors(),
            result.WaitedForMs);

        UiSessionPicture picture = this.Picture(variableStore);

        // The title carries over: a wait watches the page rather than changing it.
        variableStore.SetVariable(
            UiSessionVariable.For(this.app),
            picture.Add([entry], result.Url, picture.Title));
    }

    private UiSessionPicture Picture(VariableStore variableStore)
        => variableStore.TryGetVariable(UiSessionVariable.For(this.app), out UiSessionPicture? picture) && picture is not null
            ? picture
            : UiSessionPicture.Empty(this.app);

    private static string Capitalize(string text)
        => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];
}
