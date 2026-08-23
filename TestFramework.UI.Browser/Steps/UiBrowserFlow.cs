using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Playwright;
using TestFramework.Core;
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
/// A sequence of things a person does to an application, and the step that does them.
/// </summary>
/// <remarks>
/// <para>
/// The flow is the step. There is no terminal verb to remember and nothing to build: a timeline reads
/// <c>.Trigger(BrowserExt.Session("shop").Navigate("/products").Click("Anvil"))</c> and the object that
/// sentence produced is what the runner executes. Verbs may be added until the timeline is built, which
/// is the same mutate-until-frozen contract the framework's other fluent steps use.
/// </para>
/// <para>
/// One step, not one per interaction, because a step is the unit a timeline names, times out and
/// retries - and "log in", "fill the form", "place the order" are the units a person thinks in. Each
/// individual action is still recorded, so nothing is hidden by grouping them.
/// </para>
/// </remarks>
public sealed class UiBrowserFlow : Step<UiFlowResultContext>, IHasEnvironmentRequirements, IHasCleanupStep
{
    private readonly WebAppIdentifier app;
    private readonly List<UiActionSpec> actions = new List<UiActionSpec>();

    internal UiBrowserFlow(WebAppIdentifier app)
    {
        ArgumentNullException.ThrowIfNull(app);

        this.app = app;
    }

    /// <inheritdoc />
    public override string Name => this.actions.Count == 1 ? $"UI {this.actions[0].Kind}" : "UI Flow";

    /// <inheritdoc />
    public override string Description
        => this.actions.Count == 1
            ? $"{this.actions[0].Describe()} on '{this.app}'"
            : $"Performs {this.actions.Count} action(s) on '{this.app}'";

    /// <inheritdoc />
    public override bool DoesReturn => true;

    /// <summary>Goes to an address, relative to the application's configured base address.</summary>
    /// <param name="path">The path, for example <c>/products</c>.</param>
    /// <returns>The same flow, for chaining.</returns>
    public UiBrowserFlow Navigate(VariableReference<string> path) => this.Add(new UiActionSpec(UiActionKind.Navigate, Value: path));

    /// <summary>Presses something a person could press.</summary>
    /// <param name="target">What to press. A plain string names anything clickable.</param>
    /// <returns>The same flow, for chaining.</returns>
    public UiBrowserFlow Click(UiTarget target) => this.Add(new UiActionSpec(UiActionKind.Click, target));

    /// <summary>Types into a field, replacing whatever it held.</summary>
    /// <param name="field">The field. A plain string names it by label, placeholder or accessible name.</param>
    /// <param name="value">What to type.</param>
    /// <returns>The same flow, for chaining.</returns>
    public UiBrowserFlow Fill(UiTarget field, VariableReference<string> value)
        => this.Add(new UiActionSpec(UiActionKind.Fill, field, value));

    /// <summary>
    /// Types into a field without the value ever appearing in a log, a trace or a failure message.
    /// </summary>
    /// <param name="field">The field.</param>
    /// <param name="value">What to type.</param>
    /// <returns>The same flow, for chaining.</returns>
    public UiBrowserFlow FillSensitive(UiTarget field, VariableReference<string> value)
        => this.Add(new UiActionSpec(UiActionKind.Fill, field, value, Sensitive: true));

    /// <summary>Chooses an option from a list.</summary>
    /// <param name="field">The list.</param>
    /// <param name="option">The option's value or visible text.</param>
    /// <returns>The same flow, for chaining.</returns>
    public UiBrowserFlow Select(UiTarget field, VariableReference<string> option)
        => this.Add(new UiActionSpec(UiActionKind.Select, field, option));

    /// <summary>Ticks a checkbox or selects a radio button.</summary>
    /// <param name="target">The checkbox or radio button.</param>
    /// <returns>The same flow, for chaining.</returns>
    public UiBrowserFlow Check(UiTarget target) => this.Add(new UiActionSpec(UiActionKind.Check, target));

    /// <summary>Unticks a checkbox.</summary>
    /// <param name="target">The checkbox.</param>
    /// <returns>The same flow, for chaining.</returns>
    public UiBrowserFlow Uncheck(UiTarget target) => this.Add(new UiActionSpec(UiActionKind.Uncheck, target));

    /// <summary>Presses keys, for example <c>Enter</c> or <c>Control+Enter</c>.</summary>
    /// <param name="keys">The key or chord.</param>
    /// <returns>The same flow, for chaining.</returns>
    public UiBrowserFlow Press(VariableReference<string> keys) => this.Add(new UiActionSpec(UiActionKind.Press, Value: keys));

    /// <summary>Moves the pointer onto something, for whatever that reveals.</summary>
    /// <param name="target">What to hover.</param>
    /// <returns>The same flow, for chaining.</returns>
    public UiBrowserFlow Hover(UiTarget target) => this.Add(new UiActionSpec(UiActionKind.Hover, target));

    /// <summary>
    /// Waits until something is there, and fails if it never is.
    /// </summary>
    /// <remarks>
    /// Both an expectation and a synchronisation point: it is how a flow says "the page has caught up"
    /// without a sleep, and it fails the step when the page never does.
    /// </remarks>
    /// <param name="target">What must appear. A plain string names visible text.</param>
    /// <returns>The same flow, for chaining.</returns>
    public UiBrowserFlow Expect(UiTarget target) => this.Add(new UiActionSpec(UiActionKind.Expect, target));

    /// <summary>
    /// Waits until something is gone, and fails if it never goes.
    /// </summary>
    /// <remarks>
    /// It waits rather than checking once, which is the whole difference between an expectation and a
    /// coincidence: a spinner that has not appeared yet is absent too.
    /// </remarks>
    /// <param name="target">What must disappear.</param>
    /// <returns>The same flow, for chaining.</returns>
    public UiBrowserFlow ExpectNot(UiTarget target) => this.Add(new UiActionSpec(UiActionKind.ExpectNot, target));

    /// <summary>
    /// Reads text off the page into a variable, for the rest of the timeline to use.
    /// </summary>
    /// <remarks>
    /// A real variable, not a private corner of the step's result: an order number read here can be a
    /// route value of an API call two steps later, with nothing in between to connect them.
    /// </remarks>
    /// <param name="target">What to read.</param>
    /// <param name="into">The variable to write.</param>
    /// <returns>The same flow, for chaining.</returns>
    public UiBrowserFlow Read(UiTarget target, VariableIdentifier into)
    {
        ArgumentNullException.ThrowIfNull(into);

        return this.Add(new UiActionSpec(UiActionKind.Read, target, CaptureName: into.Identifier));
    }

    /// <summary>Photographs the page into the run's output folder.</summary>
    /// <param name="name">What the picture is of, used in the file name.</param>
    /// <returns>The same flow, for chaining.</returns>
    public UiBrowserFlow Screenshot(string name = "screenshot")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return this.Add(new UiActionSpec(UiActionKind.Screenshot, CaptureName: name));
    }

    /// <inheritdoc />
    public IReadOnlyCollection<EnvironmentRequirement> GetEnvironmentRequirements(VariableStore variableStore)
        // A bridged identifier declares the requirement of the package that provisions it, so one
        // provisioned application serves both the browser steps and that package's own.
        => [this.app.ExternalRequirement ?? new EnvironmentRequirement(BrowserEnvironmentResourceKinds.WebApp, this.app)];

    /// <inheritdoc />
    public StepGeneric? CreateCleanupStep(VariableStore variableStore)
    {
        ArgumentNullException.ThrowIfNull(variableStore);

        // Every browser step offers one, but a run needs exactly one: it closes every session the run
        // opened. The first step planned claims it.
        return UiRunState.For(variableStore).TryClaimCleanup() ? new UiSessionCleanupStep() : null;
    }

    /// <inheritdoc />
    public override void DeclareIO(StepIOContract contract)
    {
        ArgumentNullException.ThrowIfNull(contract);

        foreach (UiActionSpec action in this.actions)
        {
            if (action.Value?.Identifier is { } identifier)
            {
                contract.Inputs.Add(new StepIOEntry(identifier.Identifier, StepIOKind.Variable, true, typeof(string)));
            }

            if (action.Kind == UiActionKind.Read && action.CaptureName is { } captureName)
            {
                contract.Outputs.Add(new StepIOEntry(captureName, StepIOKind.Variable, true, typeof(string)));
            }
        }

        // The session picture is declared as an output rather than an input, and that is what keeps steps
        // on the same application in order: the planner sequences steps that write the same variable, and
        // the first step of a session legitimately has no picture to read yet.
        contract.Outputs.Add(new StepIOEntry(
            UiSessionVariable.For(this.app),
            StepIOKind.Variable,
            true,
            typeof(UiSessionPicture)));
    }

    /// <inheritdoc />
    public override Step<UiFlowResultContext> Clone()
    {
        UiBrowserFlow clone = new UiBrowserFlow(this.app);
        clone.actions.AddRange(this.actions);

        return clone.WithClonedOptions(this);
    }

    /// <inheritdoc />
    public override StepInstance<Step<UiFlowResultContext>, UiFlowResultContext> GetInstance()
        => new StepInstance<Step<UiFlowResultContext>, UiFlowResultContext>(this);

    /// <inheritdoc />
    public override async Task<UiFlowResultContext?> Execute(
        IServiceProvider serviceProvider,
        VariableStore variableStore,
        ArtifactStore artifactStore,
        ScopedLogger logger,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ArgumentNullException.ThrowIfNull(variableStore);
        ArgumentNullException.ThrowIfNull(logger);

        string label = this.LabelOptions.Label ?? this.Name;
        string sessionVariable = UiSessionVariable.For(this.app);

        WebAppConfig config = UiEnvironmentOverrides.Apply(UiConfigResolver.Resolve(serviceProvider, this.app));
        UiRunState runState = UiRunState.For(variableStore);

        UiSession session = await serviceProvider
            .GetUIComponentFactory()
            .SessionAsync(this.app, config, runState, cancellationToken)
            .ConfigureAwait(false);

        UiSessionPicture picture = variableStore.TryGetVariable(sessionVariable, out UiSessionPicture? existing) && existing is not null
            ? existing
            : UiSessionPicture.Empty(this.app);

        PlaywrightElementQuery query = new PlaywrightElementQuery(session.Page, config.TestIdAttribute, config.DefaultActionTimeout);
        UiResolutionOptions resolutionOptions = new UiResolutionOptions(config.AmbiguityMode);
        List<UiSessionEntry> entries = new List<UiSessionEntry>();

        // The runner is free to reach two steps at once; a page driven from both is not a race to leave
        // to chance.
        await session.Gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            for (int index = 0; index < this.actions.Count; index++)
            {
                UiActionSpec action = this.actions[index];
                Stopwatch stopwatch = Stopwatch.StartNew();

                try
                {
                    UiSessionEntry entry = await this
                        .PerformAsync(action, session, query, resolutionOptions, config, runState, variableStore, label, logger, cancellationToken)
                        .ConfigureAwait(false);

                    entries.Add(entry with { DurationMs = stopwatch.Elapsed.TotalMilliseconds });
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    picture = picture.Add(entries, session.Page.Url, await SafeTitleAsync(session).ConfigureAwait(false));

                    throw await this
                        .FailAsync(action, index, entries, session, runState, picture, sessionVariable, variableStore, logger, exception)
                        .ConfigureAwait(false);
                }
            }

            picture = picture.Add(entries, session.Page.Url, await SafeTitleAsync(session).ConfigureAwait(false));
            variableStore.SetVariable(sessionVariable, picture);

            return new UiFlowResultContext(this.app, picture.Url, picture.Title, entries);
        }
        finally
        {
            session.Gate.Release();
        }
    }

    private UiBrowserFlow Add(UiActionSpec action)
    {
        ((IFreezable)this).EnsureNotFrozen();
        this.actions.Add(action);

        return this;
    }

    private async Task<Exception> FailAsync(
        UiActionSpec action,
        int index,
        IReadOnlyList<UiSessionEntry> stepEntries,
        UiSession session,
        UiRunState runState,
        UiSessionPicture picture,
        string sessionVariable,
        VariableStore variableStore,
        ScopedLogger logger,
        Exception inner)
    {
        // Everything the page complained about during this step, not only since the last action. A click
        // that throws is usually recorded against the click, while the failure lands on whatever came
        // after it and never appeared - and that later failure is exactly where a reader needs to be told
        // the application broke.
        List<string> consoleErrors = [.. stepEntries.SelectMany(static entry => entry.ConsoleErrors), .. session.DrainConsoleErrors()];

        // Recorded even on the failing path, so the debugging UI and the next run's comparison both see
        // how far the session actually got.
        picture = picture with { Entries = [.. picture.Entries] };
        variableStore.SetVariable(sessionVariable, picture);

        string? bundle = await UiFailureBundle
            .CaptureAsync(session, runState, this.LabelOptions.Label ?? this.Name, picture, logger)
            .ConfigureAwait(false);

        if (UiEnvironmentOverrides.PauseOnFailure)
        {
            logger.LogWarning(
                "The browser is being held open on the failure because {0} is set. Inspect the page, then let the run continue.",
                UiEnvironmentOverrides.PauseOnFailureVariable);

            await session.Page.PauseAsync().ConfigureAwait(false);
        }

        return new UiActionFailedException(
            this.app,
            action.Describe(),
            index + 1,
            this.actions.Count,
            picture,
            consoleErrors,
            bundle,
            inner);
    }

    private async Task<UiSessionEntry> PerformAsync(
        UiActionSpec action,
        UiSession session,
        PlaywrightElementQuery query,
        UiResolutionOptions resolutionOptions,
        WebAppConfig config,
        UiRunState runState,
        VariableStore variableStore,
        string label,
        ScopedLogger logger,
        CancellationToken cancellationToken)
    {
        string? value = action.Value?.GetValue(variableStore);
        string? detail = action.Sensitive ? "•••" : value;
        UiResolvedTarget? resolved = null;

        switch (action.Kind)
        {
            case UiActionKind.Navigate:
                string target = UiUrls.Resolve(config.BaseUrl, value);
                await session.Page.GotoAsync(target).ConfigureAwait(false);
                detail = target;
                break;

            case UiActionKind.Press:
                await session.Page.Keyboard.PressAsync(value ?? throw new ArgumentException("A key press needs keys.")).ConfigureAwait(false);
                break;

            case UiActionKind.Screenshot:
                detail = await UiFailureBundle
                    .ScreenshotAsync(session, runState, action.CaptureName ?? "screenshot")
                    .ConfigureAwait(false);
                break;

            case UiActionKind.ExpectNot:
                await this.ExpectGoneAsync(action, session, query, resolutionOptions, config, cancellationToken).ConfigureAwait(false);
                detail = "gone";
                break;

            default:
                resolved = await this
                    .ResolveAsync(action, session, query, resolutionOptions, config, cancellationToken)
                    .ConfigureAwait(false);

                detail = await ActOnAsync(action, query.Locate(resolved), value, detail, variableStore).ConfigureAwait(false);
                break;
        }

        if (resolved is { IsLoose: true })
        {
            // A match the framework had to reach for is said out loud. Tolerance that hides itself is how
            // a suite ends up trusting a test that only passes by luck.
            logger.LogInformation(
                "UI '{0}': {1} matched via {2}{3}.",
                this.app.ToString(),
                action.Target?.Describe() ?? action.Kind.ToString(),
                resolved.DescribeMatch(),
                resolved.CandidateCount > 1 ? $" ({resolved.CandidateCount} candidates)" : string.Empty);
        }

        return new UiSessionEntry(
            label,
            action.Kind.ToString(),
            action.Target?.Describe(),
            resolved?.DescribeMatch(),
            resolved?.Rank,
            resolved is not null && !resolved.Spec.Exact,
            resolved?.CandidateCount ?? 0,
            resolved?.Snippet,
            detail,
            session.Page.Url,
            session.DrainConsoleErrors(),
            0);
    }

    private static async Task<string?> ActOnAsync(
        UiActionSpec action,
        ILocator locator,
        string? value,
        string? detail,
        VariableStore variableStore)
    {
        switch (action.Kind)
        {
            case UiActionKind.Click:
                await locator.ClickAsync().ConfigureAwait(false);
                return null;

            case UiActionKind.Fill:
                await locator.FillAsync(value ?? string.Empty).ConfigureAwait(false);
                return detail;

            case UiActionKind.Select:
                await locator.SelectOptionAsync(value ?? string.Empty).ConfigureAwait(false);
                return detail;

            case UiActionKind.Check:
                await locator.CheckAsync().ConfigureAwait(false);
                return null;

            case UiActionKind.Uncheck:
                await locator.UncheckAsync().ConfigureAwait(false);
                return null;

            case UiActionKind.Hover:
                await locator.HoverAsync().ConfigureAwait(false);
                return null;

            case UiActionKind.Expect:
                await locator.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible }).ConfigureAwait(false);
                return "visible";

            case UiActionKind.Read:
                string text = UiText.Normalize(await locator.InnerTextAsync().ConfigureAwait(false)) ?? string.Empty;
                variableStore.SetVariable(action.CaptureName!, text);
                return text;

            default:
                throw new InvalidOperationException($"Action '{action.Kind}' does not act on an element.");
        }
    }

    private async Task<UiResolvedTarget> ResolveAsync(
        UiActionSpec action,
        UiSession session,
        PlaywrightElementQuery query,
        UiResolutionOptions resolutionOptions,
        WebAppConfig config,
        CancellationToken cancellationToken)
    {
        UiTarget target = action.Target ?? throw new InvalidOperationException($"Action '{action.Kind}' needs a target.");
        DateTimeOffset deadline = DateTimeOffset.UtcNow + config.DefaultActionTimeout;

        while (true)
        {
            try
            {
                return await TargetResolver
                    .ResolveAsync(query, target, action.Context, resolutionOptions, this.app, session.Page.Url, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (UiTargetNotFoundException) when (DateTimeOffset.UtcNow < deadline)
            {
                // Not found may simply mean not yet: an application that renders after fetching data is
                // normal, and waiting for it is the framework's job rather than the test's. Ambiguity is
                // deliberately not retried - it is a real answer about the page, not a transient one.
                await Task.Delay(TimeSpan.FromMilliseconds(120), cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task ExpectGoneAsync(
        UiActionSpec action,
        UiSession session,
        PlaywrightElementQuery query,
        UiResolutionOptions resolutionOptions,
        WebAppConfig config,
        CancellationToken cancellationToken)
    {
        UiTarget target = action.Target ?? throw new InvalidOperationException("An absence expectation needs a target.");
        DateTimeOffset deadline = DateTimeOffset.UtcNow + config.DefaultActionTimeout;

        while (true)
        {
            UiResolvedTarget? resolved = null;

            try
            {
                resolved = await TargetResolver
                    .ResolveAsync(query, target, UiSmartContext.Text, resolutionOptions, this.app, session.Page.Url, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (UiTargetNotFoundException)
            {
                // Gone, which is what was asked for.
                return;
            }
            catch (UiAmbiguousTargetException)
            {
                // Several of it are still there, so it is certainly not gone.
            }

            if (resolved is not null && !await query.Locate(resolved).IsVisibleAsync().ConfigureAwait(false))
            {
                return;
            }

            if (DateTimeOffset.UtcNow >= deadline)
            {
                throw new TimeoutException(
                    $"The {target.Describe()} was still on the page after {config.DefaultActionTimeout.TotalSeconds:F0}s, " +
                    $"at {session.Page.Url}. An absence expectation waits for something to go away; if it was never " +
                    "supposed to be there at all, expect the state that replaces it instead.");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(120), cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task<string> SafeTitleAsync(UiSession session)
    {
        try
        {
            return await session.Page.TitleAsync().ConfigureAwait(false);
        }
        catch (PlaywrightException)
        {
            return string.Empty;
        }
    }
}
