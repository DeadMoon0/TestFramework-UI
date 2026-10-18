using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Playwright;
using TestFramework.Core;
using TestFramework.Core.Environment;
using TestFramework.Core.Environment.Graph;
using TestFramework.Core.Exceptions;
using TestFramework.Core.Steps;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Variables;
using TestFramework.UI.Browser.Configuration;
using TestFramework.UI.Browser.Events;
using TestFramework.UI.Browser.Exceptions;
using TestFramework.UI.Browser.Identifier;
using TestFramework.UI.Browser.Reading;
using TestFramework.UI.Browser.Runtime;
using TestFramework.UI.Browser.Scripting;
using TestFramework.UI.Browser.Targeting;
using TestFramework.UI.Browser.Session;

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
public sealed class UiBrowserFlow : Step<UiFlowResultContext>, IHasEnvironmentRequirements, IHasCleanupStep, IHasPreStep
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

    /// <summary>Presses something twice, the way a person opens or renames.</summary>
    /// <param name="target">What to double-click. A plain string names it the way <see cref="Click"/> does.</param>
    /// <returns>The same flow, for chaining.</returns>
    public UiBrowserFlow DoubleClick(UiTarget target) => this.Add(new UiActionSpec(UiActionKind.DoubleClick, target));

    /// <summary>Presses something with the secondary button, the way a person asks for options.</summary>
    /// <remarks>
    /// What appears is the page's own answer - a context menu the application renders. The browser's
    /// native menu never opens under automation, so this only tests pages that handle the event.
    /// </remarks>
    /// <param name="target">What to right-click.</param>
    /// <returns>The same flow, for chaining.</returns>
    public UiBrowserFlow RightClick(UiTarget target) => this.Add(new UiActionSpec(UiActionKind.RightClick, target));

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

    /// <summary>
    /// Types into a field one keystroke at a time, after whatever it already holds.
    /// </summary>
    /// <remarks>
    /// <see cref="Fill"/> sets the value in one motion, which is right for a form and wrong for anything
    /// listening to individual keys - an autocomplete, an input mask, a shortcut recorder. This verb
    /// presses each character as a person would, key events and all. It is the slower one on purpose;
    /// reach for it when the keystrokes ARE the behaviour under test.
    /// </remarks>
    /// <param name="field">The field. A plain string names it by label, placeholder or accessible name.</param>
    /// <param name="text">What to type, character by character.</param>
    /// <returns>The same flow, for chaining.</returns>
    public UiBrowserFlow Type(UiTarget field, VariableReference<string> text)
        => this.Add(new UiActionSpec(UiActionKind.Type, field, text));

    /// <summary>
    /// Types keystroke-wise without the value ever appearing in a log, a trace or a failure message.
    /// </summary>
    /// <remarks>
    /// The sensitive twin of <see cref="Type"/>, for a password behind an input mask or an
    /// autocomplete - the cases <see cref="Type"/>'s own doc recommends it for, which are also the
    /// cases where the value is a credential. <see cref="FillSensitive"/> is the one-motion form.
    /// </remarks>
    /// <param name="field">The field.</param>
    /// <param name="text">What to type, character by character.</param>
    /// <returns>The same flow, for chaining.</returns>
    public UiBrowserFlow TypeSensitive(UiTarget field, VariableReference<string> text)
        => this.Add(new UiActionSpec(UiActionKind.Type, field, text, Sensitive: true));

    /// <summary>Chooses an option from a list.</summary>
    /// <param name="field">The list.</param>
    /// <param name="option">The option's value or visible text.</param>
    /// <returns>The same flow, for chaining.</returns>
    public UiBrowserFlow Select(UiTarget field, VariableReference<string> option)
        => this.Add(new UiActionSpec(UiActionKind.Select, field, option));

    /// <summary>
    /// Chooses an option from whatever kind of list control the page has.
    /// </summary>
    /// <remarks>
    /// Drives a native <c>&lt;select&gt;</c> and the ARIA combobox pattern alike - the component-library
    /// selects are the second kind - and matches the option by its label or value, exactly first and
    /// loosely only when nothing matches exactly. <see cref="Select"/> stays the native-only verb.
    /// </remarks>
    /// <param name="field">The list control.</param>
    /// <param name="option">The option, as a person would name it.</param>
    /// <returns>The same flow, for chaining.</returns>
    public UiBrowserFlow Choose(UiTarget field, VariableReference<string> option)
        => this.Add(new UiActionSpec(UiActionKind.Choose, field, option));

    /// <summary>Ticks a checkbox or selects a radio button.</summary>
    /// <param name="target">The checkbox or radio button.</param>
    /// <returns>The same flow, for chaining.</returns>
    public UiBrowserFlow Check(UiTarget target) => this.Add(new UiActionSpec(UiActionKind.Check, target));

    /// <summary>Unticks a checkbox.</summary>
    /// <param name="target">The checkbox.</param>
    /// <returns>The same flow, for chaining.</returns>
    public UiBrowserFlow Uncheck(UiTarget target) => this.Add(new UiActionSpec(UiActionKind.Uncheck, target));

    /// <summary>Presses keys, for example <c>Enter</c> or <c>Control+Enter</c>.</summary>
    /// <remarks>
    /// The keys go to whatever currently has focus, which makes this the verb for a page-wide shortcut.
    /// A shortcut that belongs to one control is better said with the targeted overload, which focuses
    /// first.
    /// </remarks>
    /// <param name="keys">The key or chord.</param>
    /// <returns>The same flow, for chaining.</returns>
    public UiBrowserFlow Press(VariableReference<string> keys) => this.Add(new UiActionSpec(UiActionKind.Press, Value: keys));

    /// <summary>Presses keys on one control - focused first, so the keys land where the test says.</summary>
    /// <param name="target">The control to focus and press the keys on.</param>
    /// <param name="keys">The key or chord, for example <c>Control+Enter</c>.</param>
    /// <returns>The same flow, for chaining.</returns>
    public UiBrowserFlow Press(UiTarget target, VariableReference<string> keys)
    {
        ArgumentNullException.ThrowIfNull(target);

        return this.Add(new UiActionSpec(UiActionKind.Press, target, keys));
    }

    /// <summary>Moves the pointer onto something, for whatever that reveals.</summary>
    /// <param name="target">What to hover.</param>
    /// <returns>The same flow, for chaining.</returns>
    public UiBrowserFlow Hover(UiTarget target) => this.Add(new UiActionSpec(UiActionKind.Hover, target));

    /// <summary>
    /// Moves the pointer off everything, to the viewport's top-left corner.
    /// </summary>
    /// <remarks>
    /// The other half of <see cref="Hover"/>: what a page shows must also go away when the pointer
    /// leaves, and that is a behaviour worth a test of its own. The corner is the one place with nothing
    /// of the page's own in it on most layouts; a page with a control there gets its pointer moved onto
    /// that control, so hover something known-inert instead.
    /// </remarks>
    /// <returns>The same flow, for chaining.</returns>
    public UiBrowserFlow MouseAway() => this.Add(new UiActionSpec(UiActionKind.MouseAway));

    /// <summary>
    /// Drags something onto something else - pressed, moved, released, the way a hand does it.
    /// </summary>
    /// <remarks>
    /// Real pointer events from source to destination, so both the HTML5 drag contract and the
    /// pointer-tracking kind of widget see what they would see from a person.
    /// </remarks>
    /// <param name="target">What to pick up. A plain string names it by its visible text.</param>
    /// <param name="destination">What to drop it on.</param>
    /// <returns>The same flow, for chaining.</returns>
    public UiBrowserFlow DragTo(UiTarget target, UiTarget destination)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(destination);

        return this.Add(new UiActionSpec(UiActionKind.Drag, target, SecondTarget: destination));
    }

    /// <summary>
    /// Brings something into view.
    /// </summary>
    /// <remarks>
    /// The acting verbs scroll on their own before they act, so this is not a prerequisite for a
    /// <see cref="Click"/>. It is for when the scrolling itself is the point: content that loads as it
    /// approaches the viewport, a sticky bar that appears past a threshold, or a layout check about to
    /// ask what is <c>InViewport</c>.
    /// </remarks>
    /// <param name="target">What to bring into view. A plain string names visible text.</param>
    /// <returns>The same flow, for chaining.</returns>
    public UiBrowserFlow ScrollTo(UiTarget target) => this.Add(new UiActionSpec(UiActionKind.ScrollTo, target));

    /// <summary>Scrolls the page back to its top.</summary>
    /// <returns>The same flow, for chaining.</returns>
    public UiBrowserFlow ScrollToTop() => this.Add(new UiActionSpec(UiActionKind.ScrollToTop));

    /// <summary>
    /// Scrolls the page to its bottom.
    /// </summary>
    /// <remarks>
    /// The bottom as the page has it at that moment. A list that grows while being scrolled - an
    /// infinite feed - has no bottom to arrive at; scroll to the thing being looked for instead, with
    /// <see cref="ScrollTo"/>, or wait for it with an event.
    /// </remarks>
    /// <returns>The same flow, for chaining.</returns>
    public UiBrowserFlow ScrollToBottom() => this.Add(new UiActionSpec(UiActionKind.ScrollToBottom));

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
    public UiBrowserFlow Read(UiTarget target, VariableIdentifier into) => this.Read(Value.Text(target), into);

    /// <summary>
    /// Reads a value off the page into a variable, for the rest of the timeline to use.
    /// </summary>
    /// <remarks>
    /// The variable receives the source's own type - a string for text, a bool for a tick, an int for a
    /// count. A read is a snapshot of what the page says now; waiting for the page to say something is
    /// what <see cref="Expect"/> is for.
    /// </remarks>
    /// <param name="source">What to read - see <see cref="Value"/>.</param>
    /// <param name="into">The variable to write.</param>
    /// <returns>The same flow, for chaining.</returns>
    public UiBrowserFlow Read(UiValueSource source, VariableIdentifier into)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(into);

        return this.Add(new UiActionSpec(UiActionKind.Read, source.Target, CaptureName: into.Identifier, Source: source));
    }

    /// <summary>Photographs the page into the run's output folder.</summary>
    /// <returns>The same flow, for chaining.</returns>
    public UiBrowserFlow Screenshot()
        => this.Screenshot("screenshot");

    /// <summary>Photographs the page into the run's output folder.</summary>
    /// <param name="name">What the picture is of, used in the file name.</param>
    /// <returns>The same flow, for chaining.</returns>
    public UiBrowserFlow Screenshot(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return this.Add(new UiActionSpec(UiActionKind.Screenshot, CaptureName: name));
    }

    /// <summary>
    /// Runs JavaScript in the page for its effect, ignoring what it returns.
    /// </summary>
    /// <remarks>
    /// The escape hatch, and honest about it: a script bypasses everything the verbs guarantee. Nothing
    /// checks that what it touches is visible, enabled or there at all, so '() =&gt; el.click()' will
    /// happily press a button a person could not - a green test over a broken page. Scripts are for
    /// reading state and seeding it, not for acting; every one is recorded in the session story, and
    /// <c>run.UiScripts(app)</c> lets a suite keep their number at zero.
    /// </remarks>
    /// <param name="script">The function, called as <c>() =&gt; ...</c>, or <c>args =&gt; ...</c> when
    /// arguments were declared.</param>
    /// <returns>The same flow, for chaining.</returns>
    public UiBrowserFlow Execute(JsScript script)
    {
        ArgumentNullException.ThrowIfNull(script);

        return this.Add(new UiActionSpec(UiActionKind.Execute, Script: script));
    }

    /// <summary>
    /// Runs JavaScript on one element, found the way every other verb finds it.
    /// </summary>
    /// <param name="target">The element. A plain string names it by its visible text.</param>
    /// <param name="script">The function, called as <c>el =&gt; ...</c>, or <c>(el, args) =&gt; ...</c>
    /// when arguments were declared.</param>
    /// <returns>The same flow, for chaining.</returns>
    public UiBrowserFlow Execute(UiTarget target, JsScript script)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(script);

        return this.Add(new UiActionSpec(UiActionKind.Execute, target, Script: script));
    }

    /// <summary>
    /// Runs JavaScript in the page and keeps what it returns in a variable.
    /// </summary>
    /// <remarks>
    /// For the value no verb reads yet. The result must be plain data the page can hand over - strings,
    /// numbers, booleans, arrays, objects - and it lands in the variable as the type this call names, so
    /// the rest of the timeline gets a value rather than JSON.
    /// </remarks>
    /// <typeparam name="T">The type the variable is declared and written as.</typeparam>
    /// <param name="script">The function, called as <c>() =&gt; ...</c>, or <c>args =&gt; ...</c> when
    /// arguments were declared.</param>
    /// <param name="into">The variable to write.</param>
    /// <returns>The same flow, for chaining.</returns>
    public UiBrowserFlow Evaluate<T>(JsScript script, VariableIdentifier into)
    {
        ArgumentNullException.ThrowIfNull(script);
        ArgumentNullException.ThrowIfNull(into);

        return this.Add(new UiActionSpec(
            UiActionKind.Evaluate,
            Script: script,
            CaptureName: into.Identifier,
            ResultBinder: new UiScriptResultBinder<T>()));
    }

    /// <summary>
    /// Runs JavaScript on one element and keeps what it returns in a variable.
    /// </summary>
    /// <typeparam name="T">The type the variable is declared and written as.</typeparam>
    /// <param name="target">The element. A plain string names it by its visible text.</param>
    /// <param name="script">The function, called as <c>el =&gt; ...</c>, or <c>(el, args) =&gt; ...</c>
    /// when arguments were declared.</param>
    /// <param name="into">The variable to write.</param>
    /// <returns>The same flow, for chaining.</returns>
    public UiBrowserFlow Evaluate<T>(UiTarget target, JsScript script, VariableIdentifier into)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(script);
        ArgumentNullException.ThrowIfNull(into);

        return this.Add(new UiActionSpec(
            UiActionKind.Evaluate,
            target,
            Script: script,
            CaptureName: into.Identifier,
            ResultBinder: new UiScriptResultBinder<T>()));
    }

    /// <inheritdoc />
    public IReadOnlyCollection<EnvironmentRequirement> GetEnvironmentRequirements(VariableStore variableStore)
        => UiRequirements.Without(this.app);

    /// <inheritdoc />
    /// <remarks>
    /// The application, and the resource its address comes from - bridged, or the same-name site or API -
    /// decided with the run's resources in view; see <see cref="UiRequirements"/>.
    /// </remarks>
    public IReadOnlyCollection<EnvironmentRequirement> GetEnvironmentRequirements(VariableStore variableStore, ResourceGraph resources)
        => UiRequirements.For(this.app, resources, UiRunState.For(variableStore));

    /// <inheritdoc />
    /// <remarks>
    /// Every browser step offers one, but a run needs at most one: it installs whatever the plan noted for the run's
    /// applications. The first step planned claims it.
    /// </remarks>
    public StepGeneric? CreatePreStep(VariableStore variableStore)
        => UiRunState.For(variableStore).TryClaimInstall() ? new UiBrowserInstallStep() : null;

    /// <inheritdoc />
    /// <remarks>
    /// Also where the retry rule is enforced, because this is the one moment at plan time a step sees
    /// both its final options and the run's variables - after the modifiers have been applied, before
    /// any browser exists.
    /// </remarks>
    public StepGeneric? CreateCleanupStep(VariableStore variableStore)
    {
        ArgumentNullException.ThrowIfNull(variableStore);

        // Retrying a flow replays its actions against whatever the failed attempt left behind: a second
        // "Place order" places a second order. A flow that begins with Navigate starts every attempt
        // from a known page, so that is the one shape a retry is allowed on - refused here, at plan
        // time, rather than discovered in production data.
        if (this.RetryOptions.MaxRetryCount.GetValue(variableStore) > 0
            && (this.actions.Count == 0 || this.actions[0].Kind != UiActionKind.Navigate))
        {
            throw new FrameworkConfigurationException(
                $"'{this.LabelOptions.Label ?? this.Name}' combines WithRetry with a browser flow that starts " +
                $"with {(this.actions.Count == 0 ? "no action" : this.actions[0].Kind.ToString())}. A retried " +
                "attempt replays its actions against whatever the failed one left behind, so only a flow whose " +
                "first action is Navigate may retry - each attempt then starts from a known page. Start the " +
                "flow with Navigate, or drop WithRetry.");
        }

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
                contract.Input(identifier, typeof(string));
            }

            foreach ((string _, VariableReference<string> argument) in action.Script?.Arguments ?? [])
            {
                if (argument.Identifier is { } argumentIdentifier)
                {
                    // A script's dependencies are step inputs like any other verb's, so the planner sees
                    // them and a typo fails before a browser starts.
                    contract.Input(argumentIdentifier, typeof(string));
                }
            }

            if (action.Kind == UiActionKind.Evaluate && action.CaptureName is { } evaluated)
            {
                contract.Output(new VariableIdentifier(evaluated), action.ResultBinder!.ResultType);
            }

            if (action.Kind == UiActionKind.Read && action.CaptureName is { } captureName)
            {
                // The variable's declared type is the source's own - so a count is an int to the step
                // that consumes it, not a string that happens to hold digits.
                contract.Output(new VariableIdentifier(captureName), action.Source?.ValueType ?? typeof(string));
            }
        }

        // The session picture is declared as an output rather than an input, and that is what keeps steps
        // on the same application in order: the planner sequences steps that write the same variable, and
        // the first step of a session legitimately has no picture to read yet.
        contract.Output(new VariableIdentifier(UiSessionVariable.For(this.app)), typeof(UiSessionPicture));
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
    public override async Task<UiFlowResultContext?> Execute(RunContext context)
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

        UiSessionPicture picture = context.Variables.TryGetVariable(sessionVariable, out UiSessionPicture? existing) && existing is not null
            ? existing
            : UiSessionPicture.Empty(this.app);

        UiActionPerformer performer = new UiActionPerformer(this.app, session, config, context, label);
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
                    UiSessionEntry entry = await performer.PerformAsync(action, cancellationToken).ConfigureAwait(false);

                    entries.Add(entry with { DurationMs = stopwatch.Elapsed.TotalMilliseconds });

                    // What the page looked like after the action, when the suite asked to keep it.
                    // Inside the loop rather than at the end of the step, because the point is the
                    // sequence: a failure three actions later is read backwards from here.
                    if (config.EffectiveWidgetCapture == UiWidgetCapture.EveryAction)
                    {
                        await UiEvidence
                            .ScreenshotAsync(session, context, $"{index + 1:00}-{action.Describe()}", takeGate: false)
                            .ConfigureAwait(false);
                    }
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    picture = picture.Add(entries, session.Page.Url, await SafeTitleAsync(session).ConfigureAwait(false));

                    throw this.Fail(action, index, entries, session, picture, sessionVariable, context, exception);
                }
                catch (OperationCanceledException exception) when (context.Deadline.HasExpired)
                {
                    // The story of what DID happen survives the timeout: these entries lived only in
                    // this local list, so the observer photographed the page against a pre-step story
                    // and the frozen run missed the step entirely. Same catch the wait events carry,
                    // and the account names which action was running when the time ran out.
                    picture = picture.Add(entries, session.Page.Url, await SafeTitleAsync(session).ConfigureAwait(false));
                    context.Variables.SetVariable(sessionVariable, picture);

                    throw new TimeoutException(
                        $"Action {index + 1} of {this.actions.Count} on '{this.app}' ({action.Describe()}) was still running when the step's time ran out.",
                        exception);
                }
            }

            picture = picture.Add(entries, session.Page.Url, await SafeTitleAsync(session).ConfigureAwait(false));
            context.Variables.SetVariable(sessionVariable, picture);

            return new UiFlowResultContext(this.app, picture.Url, picture.Title, entries);
        }
        finally
        {
            session.Gate.Release();
        }
    }

    /// <summary>
    /// The actions as written, for the tests that check what a verb records.
    /// </summary>
    internal IReadOnlyList<UiActionSpec> ActionsForTesting => this.actions;

    private UiBrowserFlow Add(UiActionSpec action)
    {
        ((IFreezable)this).EnsureNotFrozen();
        this.actions.Add(action);

        return this;
    }

    /// <summary>
    /// The step's own account of what went wrong.
    /// </summary>
    /// <remarks>
    /// It records the session; it does not gather the evidence. Photographing the page, writing its
    /// markup and holding the browser open used to happen here, and in the wait and the inspection too -
    /// three copies of a job that belongs to whoever is watching the run rather than to each step that
    /// can fail. Where the evidence ends up is no longer this step's business either: it goes to the
    /// run's own widgets, which is a place every surface already knows how to read.
    /// </remarks>
    private Exception Fail(
        UiActionSpec action,
        int index,
        IReadOnlyList<UiSessionEntry> stepEntries,
        UiSession session,
        UiSessionPicture picture,
        string sessionVariable,
        RunContext context,
        Exception inner)
    {
        // Everything the page complained about during this step, not only since the last action. A click
        // that throws is usually recorded against the click, while the failure lands on whatever came
        // after it and never appeared - and that later failure is exactly where a reader needs to be told
        // the application broke.
        List<string> consoleErrors = [.. stepEntries.SelectMany(static entry => entry.ConsoleErrors), .. session.DrainConsoleErrors()];

        // Recorded even on the failing path, and before this throws: the debugging UI, the next run's
        // comparison and the observer about to photograph the page all read the session from here.
        context.Variables.SetVariable(sessionVariable, picture);

        return new UiActionFailedException(
            this.app,
            action.Describe(),
            index + 1,
            this.actions.Count,
            picture,
            consoleErrors,
            inner);
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
