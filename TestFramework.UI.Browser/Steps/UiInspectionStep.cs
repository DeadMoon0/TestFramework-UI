using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Playwright;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Environment;
using TestFramework.Core.Logging;
using TestFramework.Core.Steps;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Variables;
using TestFramework.UI.Browser.Configuration;
using TestFramework.UI.Browser.Exceptions;
using TestFramework.UI.Browser.Identifier;
using TestFramework.UI.Browser.Resolution;
using TestFramework.UI.Browser.Runtime;
using TestFramework.UI.Browser.Targeting;
using TestFramework.UI.Session;

namespace TestFramework.UI.Browser.Steps;

/// <summary>
/// The shared shape of a step that looks at a page rather than acting on it.
/// </summary>
/// <remarks>
/// <para>
/// Looking is its own kind of step, and it is worth its own name in a timeline: "the order list is right"
/// is a claim a run either upheld or did not, and it should read that way in a report rather than being
/// buried inside the flow that produced the page.
/// </para>
/// <para>
/// Every inspection retries until the page settles. A page fetches, renders, and then fills in - so a
/// single look at a live application is a coin toss, and comparing once is the most reliable way to write
/// a flaky test. The last comparison is the one that is reported, so a failure describes the page as it
/// finally was rather than as it was mid-render.
/// </para>
/// </remarks>
/// <typeparam name="TResult">What this inspection produces.</typeparam>
internal abstract class UiInspectionStep<TResult> : Step<TResult>, IHasEnvironmentRequirements, IHasCleanupStep
    where TResult : StepResultContext
{
    private readonly WebAppIdentifier app;
    private readonly UiTarget? target;

    protected UiInspectionStep(WebAppIdentifier app, UiTarget target)
        : this(app)
        => this.target = target ?? throw new ArgumentNullException(nameof(target));

    /// <summary>
    /// Creates an inspection without a subject element - one that looks at the page as a whole, the way
    /// a layout check relates many elements rather than examining one.
    /// </summary>
    /// <param name="app">The application to look at.</param>
    protected UiInspectionStep(WebAppIdentifier app)
    {
        ArgumentNullException.ThrowIfNull(app);

        this.app = app;
    }

    /// <summary>The application being looked at.</summary>
    protected WebAppIdentifier App => this.app;

    /// <summary>What is being looked at.</summary>
    protected UiTarget Target => this.target ?? throw new InvalidOperationException("This inspection has no subject element.");

    /// <summary>The element being looked at, or null for an inspection of the page as a whole.</summary>
    private protected UiTarget? Subject => this.target;

    /// <summary>The lookup machinery of the running inspection, for the steps that resolve their own targets.</summary>
    private protected UiInspectionLens? Lens { get; private set; }

    /// <summary>How this inspection is named in the session story.</summary>
    protected abstract string ActionName { get; }

    /// <inheritdoc />
    public override bool DoesReturn => true;

    /// <inheritdoc />
    public IReadOnlyCollection<EnvironmentRequirement> GetEnvironmentRequirements(VariableStore variableStore)
        => [this.app.ExternalRequirement ?? new EnvironmentRequirement(BrowserEnvironmentResourceKinds.WebApp, this.app)];

    /// <inheritdoc />
    public StepGeneric? CreateCleanupStep(VariableStore variableStore)
    {
        ArgumentNullException.ThrowIfNull(variableStore);

        return UiRunState.For(variableStore).TryClaimCleanup() ? new UiSessionCleanupStep() : null;
    }

    /// <inheritdoc />
    public override void DeclareIO(StepIOContract contract)
    {
        ArgumentNullException.ThrowIfNull(contract);

        // Same session variable as the flow steps, which is what keeps an inspection ordered after the
        // flow that produced the page it is about.
        contract.Outputs.Add(new StepIOEntry(
            UiSessionVariable.For(this.app),
            StepIOKind.Variable,
            true,
            typeof(UiSessionPicture)));

        this.DeclareOwnIO(contract);
    }

    /// <inheritdoc />
    public override async Task<TResult?> Execute(RunContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        string label = this.LabelOptions.Label ?? this.Name;
        string sessionVariable = UiSessionVariable.For(this.app);
        CancellationToken cancellationToken = context.Deadline.Token;

        WebAppConfig config = UiConfigResolver.ResolveEffective(context, this.app);
        UiRunState runState = UiRunState.For(context.Variables);

        UiSession session = await context.Services
            .GetUIComponentFactory()
            .SessionAsync(this.app, config, runState, cancellationToken)
            .ConfigureAwait(false);

        PlaywrightElementQuery query = new PlaywrightElementQuery(session.Page, config.EffectiveTestIdAttribute);
        UiResolutionOptions resolutionOptions = new UiResolutionOptions(config.EffectiveAmbiguityMode);

        await session.Gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        Stopwatch stopwatch = Stopwatch.StartNew();

        try
        {
            this.Lens = new UiInspectionLens(session, query, resolutionOptions, config);

            UiResolvedTarget? resolved = this.target is null
                ? null
                : await this
                    .ResolveAsync(query, session, resolutionOptions, config, cancellationToken)
                    .ConfigureAwait(false);

            (TResult result, string? detail) = await this
                .InspectUntilSettledAsync(query, resolved, config, cancellationToken)
                .ConfigureAwait(false);

            this.Record(context.Variables, sessionVariable, session, label, resolved, detail, stopwatch);

            // Recorded first, then thrown: the observer that photographs this reads the session from the
            // run, so what it captures has to be the story including the look that failed.
            if (this.Verdict(result, stopwatch.Elapsed) is { } verdict)
                throw verdict;

            return result;
        }
        finally
        {
            session.Gate.Release();
        }
    }

    /// <summary>
    /// Declares whatever else this inspection reads or writes.
    /// </summary>
    /// <param name="contract">The contract to add to.</param>
    protected virtual void DeclareOwnIO(StepIOContract contract)
    {
    }

    /// <summary>
    /// Looks at the page once.
    /// </summary>
    /// <param name="locator">The element being looked at.</param>
    /// <param name="cancellationToken">Cancels the look.</param>
    /// <returns>What was found, and a line for the session story.</returns>
    protected abstract Task<(TResult Result, string? Detail)> InspectAsync(ILocator? locator, CancellationToken cancellationToken);

    /// <summary>
    /// Whether this result is worth looking again for, because the page may not have settled.
    /// </summary>
    /// <param name="result">What the last look found.</param>
    /// <returns>True to look again until the deadline.</returns>
    protected virtual bool ShouldRetry(TResult result) => false;

    /// <summary>
    /// The failure this result amounts to, or null when the inspection is satisfied.
    /// </summary>
    /// <param name="result">What the last look found.</param>
    /// <param name="waited">How long the page was given to settle.</param>
    /// <returns>The exception to fail the step with, or null.</returns>
    protected virtual Exception? Verdict(TResult result, TimeSpan waited) => null;

    /// <summary>
    /// Stores a value this inspection produced, so later steps and the assertions can use it.
    /// </summary>
    /// <param name="variableStore">The run's variables.</param>
    /// <param name="identifier">The variable to write.</param>
    /// <param name="value">The value.</param>
    protected static void Publish<T>(VariableStore variableStore, VariableIdentifier identifier, T value)
        => variableStore.SetVariable(identifier, value);

    private async Task<UiResolvedTarget> ResolveAsync(
        PlaywrightElementQuery query,
        UiSession session,
        UiResolutionOptions resolutionOptions,
        WebAppConfig config,
        CancellationToken cancellationToken)
    {
        // Stopwatch, not wall clock: one clock per deadline, and this budget must not jump with the
        // system time. The loop stays bounded by the step token either way.
        Stopwatch budget = Stopwatch.StartNew();

        while (true)
        {
            try
            {
                return await TargetResolver
                    .ResolveAsync(query, this.target!, UiSmartContext.Section, resolutionOptions, this.app, session.Page.Url, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (UiTargetNotFoundException) when (budget.Elapsed < config.EffectiveActionTimeout)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(120), cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task<(TResult Result, string? Detail)> InspectUntilSettledAsync(
        PlaywrightElementQuery query,
        UiResolvedTarget? resolved,
        WebAppConfig config,
        CancellationToken cancellationToken)
    {
        // Stopwatch, not wall clock: one clock per deadline, and this budget must not jump with the
        // system time. The loop stays bounded by the step token either way.
        Stopwatch budget = Stopwatch.StartNew();

        while (true)
        {
            (TResult result, string? detail) = await this
                .InspectAsync(resolved is null ? null : query.Locate(resolved), cancellationToken)
                .ConfigureAwait(false);

            if (!this.ShouldRetry(result) || budget.Elapsed >= config.EffectiveCompareTimeout)
            {
                return (result, detail);
            }

            await Task.Delay(TimeSpan.FromMilliseconds(150), cancellationToken).ConfigureAwait(false);
        }
    }

    private void Record(
        VariableStore variableStore,
        string sessionVariable,
        UiSession session,
        string label,
        UiResolvedTarget? resolved,
        string? detail,
        Stopwatch stopwatch)
    {
        UiSessionEntry entry = new UiSessionEntry(
            label,
            this.ActionName,
            this.target?.Describe() ?? "the page",
            resolved?.DescribeMatch(),
            resolved?.Rank,
            resolved is not null && !resolved.Spec.Exact,
            resolved?.CandidateCount ?? 0,
            resolved?.Snippet,
            detail,
            session.Page.Url,
            session.DrainConsoleErrors(),
            stopwatch.Elapsed.TotalMilliseconds);

        UiSessionPicture picture = ReadPicture(variableStore, sessionVariable, this.app);

        // The title carries over: an inspection reads the page rather than changing it, so claiming it
        // ended up on an untitled page would be a lie the debug view would happily show.
        variableStore.SetVariable(sessionVariable, picture.Add([entry], session.Page.Url, picture.Title));
    }

    private static UiSessionPicture ReadPicture(VariableStore variableStore, string identifier, string app)
        => variableStore.TryGetVariable(identifier, out UiSessionPicture? picture) && picture is not null
            ? picture
            : UiSessionPicture.Empty(app);
}

/// <summary>
/// The lookup machinery of one running inspection, for a step that resolves targets of its own.
/// </summary>
/// <param name="Session">The browser session.</param>
/// <param name="Query">The page's lookup surface.</param>
/// <param name="Options">The resolution dials of this session.</param>
/// <param name="Config">The application's configuration.</param>
internal sealed record UiInspectionLens(
    UiSession Session,
    PlaywrightElementQuery Query,
    UiResolutionOptions Options,
    WebAppConfig Config);

