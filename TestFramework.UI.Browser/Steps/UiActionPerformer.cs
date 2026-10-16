using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Playwright;
using TestFramework.Core.Exceptions;
using TestFramework.Core.Logging;
using TestFramework.Core.Steps;
using TestFramework.Core.Variables;
using TestFramework.UI.Browser.Configuration;
using TestFramework.UI.Browser.Events;
using TestFramework.UI.Browser.Exceptions;
using TestFramework.UI.Browser.Identifier;
using TestFramework.UI.Browser.Reading;
using TestFramework.UI.Browser.Resolution;
using TestFramework.UI.Browser.Runtime;
using TestFramework.UI.Browser.Scripting;
using TestFramework.UI.Browser.Targeting;
using TestFramework.UI.Session;

namespace TestFramework.UI.Browser.Steps;

/// <summary>
/// Performs one recorded action of a <see cref="UiBrowserFlow"/> against its session's page, and says
/// what happened as a session entry.
/// </summary>
/// <remarks>
/// One per execution of a flow: everything an action needs besides itself - the page, the element
/// query, the configuration, the run - is fixed for the whole flow, so it is given once here rather
/// than threaded through every helper. The flow keeps the sequence, the gate and the failure account.
/// </remarks>
internal sealed class UiActionPerformer
{
    private readonly WebAppIdentifier app;
    private readonly UiSession session;
    private readonly PlaywrightElementQuery query;
    private readonly UiResolutionOptions resolutionOptions;
    private readonly WebAppConfig config;
    private readonly RunContext run;
    private readonly VariableStore variableStore;
    private readonly string label;
    private readonly ScopedLogger logger;

    internal UiActionPerformer(WebAppIdentifier app, UiSession session, WebAppConfig config, RunContext run, string label)
    {
        this.app = app;
        this.session = session;
        this.query = new PlaywrightElementQuery(session.Page, config.EffectiveTestIdAttribute);
        this.resolutionOptions = new UiResolutionOptions(config.EffectiveAmbiguityMode);
        this.config = config;
        this.run = run;
        this.variableStore = run.Variables;
        this.label = label;
        this.logger = run.Logger;
    }

    /// <summary>
    /// Performs one action and records it. The entry's duration is the caller's to fill in.
    /// </summary>
    internal async Task<UiSessionEntry> PerformAsync(UiActionSpec action, CancellationToken cancellationToken)
    {
        string? value = action.Value?.GetValue(this.variableStore);
        string? detail = action.Sensitive ? "•••" : value;

        (UiResolvedTarget? resolved, detail) = await this
            .DispatchAsync(action, value, detail, cancellationToken)
            .ConfigureAwait(false);

        if (resolved is { IsLoose: true })
        {
            this.LogLooseMatch(action, resolved);
        }

        return this.CreateEntry(action, resolved, detail);
    }

    private async Task<(UiResolvedTarget? Resolved, string? Detail)> DispatchAsync(
        UiActionSpec action,
        string? value,
        string? detail,
        CancellationToken cancellationToken)
    {
        switch (action.Kind)
        {
            case UiActionKind.Navigate:
                return (null, await this.NavigateAsync(value).ConfigureAwait(false));

            case UiActionKind.Press when action.Target is null:
                await this.session.Page.Keyboard.PressAsync(value ?? throw new ArgumentException("A key press needs keys.")).ConfigureAwait(false);
                return (null, detail);

            case UiActionKind.MouseAway:
                await this.session.Page.Mouse.MoveAsync(0, 0).ConfigureAwait(false);
                return (null, "to the corner");

            case UiActionKind.Drag:
                return await this.DragAsync(action, cancellationToken).ConfigureAwait(false);

            case UiActionKind.Screenshot:
                // The gate is already held by the step this runs inside, and it is not reentrant.
                return (null, await UiEvidence
                    .ScreenshotAsync(this.session, this.run, action.CaptureName ?? "screenshot", takeGate: false)
                    .ConfigureAwait(false));

            // Instant on purpose, both of them: a stylesheet's scroll-behavior:smooth would leave the
            // page mid-glide when the next action looks at it, and where the viewport ends up must not
            // depend on styling.
            case UiActionKind.ScrollToTop:
                await PageJson
                    .EvaluateAsync(this.session.Page, "() => window.scrollTo({ top: 0, left: 0, behavior: 'instant' })")
                    .ConfigureAwait(false);
                return (null, "top");

            case UiActionKind.ScrollToBottom:
                await PageJson
                    .EvaluateAsync(this.session.Page, "() => window.scrollTo({ top: document.documentElement.scrollHeight, left: 0, behavior: 'instant' })")
                    .ConfigureAwait(false);
                return (null, "bottom");

            case UiActionKind.ExpectNot:
                await this.ExpectGoneAsync(action, cancellationToken).ConfigureAwait(false);
                return (null, "gone");

            case UiActionKind.Read:
                return await this.ReadAsync(action, cancellationToken).ConfigureAwait(false);

            case UiActionKind.Execute:
            case UiActionKind.Evaluate:
                return await this.RunScriptAsync(action, cancellationToken).ConfigureAwait(false);

            case UiActionKind.Choose:
                return await this.ChooseAsync(action, value, cancellationToken).ConfigureAwait(false);

            default:
                UiResolvedTarget resolved = await this.ResolveAsync(action, cancellationToken).ConfigureAwait(false);

                return (resolved, await ActOnAsync(action, this.query.Locate(resolved), value, detail).ConfigureAwait(false));
        }
    }

    private async Task<string> NavigateAsync(string? value)
    {
        string target = UiUrls.Resolve(this.config.BaseUrl, value);
        await this.session.Page.GotoAsync(target).ConfigureAwait(false);

        return target;
    }

    private async Task<(UiResolvedTarget? Resolved, string? Detail)> DragAsync(UiActionSpec action, CancellationToken cancellationToken)
    {
        UiResolvedTarget resolved = await this.ResolveAsync(action, cancellationToken).ConfigureAwait(false);

        UiResolvedTarget destination = await this
            .ResolveTargetAsync(action.SecondTarget!, UiSmartContext.Text, cancellationToken)
            .ConfigureAwait(false);

        await this.query.Locate(resolved).DragToAsync(this.query.Locate(destination)).ConfigureAwait(false);

        // The destination's own match is said here, because the entry's audit fields carry the
        // dragged thing - a destination the framework had to reach for must not hide behind it.
        string detail = destination.IsLoose
            ? $"to {action.SecondTarget!.Describe()} (matched via {destination.DescribeMatch()})"
            : $"to {action.SecondTarget!.Describe()}";

        return (resolved, detail);
    }

    private async Task<(UiResolvedTarget? Resolved, string? Detail)> ChooseAsync(
        UiActionSpec action,
        string? value,
        CancellationToken cancellationToken)
    {
        UiResolvedTarget resolved = await this.ResolveAsync(action, cancellationToken).ConfigureAwait(false);

        string detail = await UiChooser.ChooseAsync(
            this.query.Locate(resolved),
            this.session.Page,
            value ?? throw new ArgumentException("Choosing needs an option to choose."),
            action.Target!.Describe(),
            this.config.EffectiveActionTimeout,
            cancellationToken).ConfigureAwait(false);

        return (resolved, detail);
    }

    private void LogLooseMatch(UiActionSpec action, UiResolvedTarget resolved)
    {
        // A match the framework had to reach for is said out loud. Tolerance that hides itself is how
        // a suite ends up trusting a test that only passes by luck.
        this.logger.LogInformation(
            "UI '{0}': {1} matched via {2}{3}.",
            this.app.ToString(),
            action.Target?.Describe() ?? action.Kind.ToString(),
            resolved.DescribeMatch(),
            resolved.CandidateCount > 1 ? $" ({resolved.CandidateCount} candidates)" : string.Empty);
    }

    private UiSessionEntry CreateEntry(UiActionSpec action, UiResolvedTarget? resolved, string? detail)
        => new UiSessionEntry
        {
            StepLabel = this.label,
            Action = action.Kind.ToString(),
            Target = action.Target?.Describe(),
            ResolvedVia = resolved?.DescribeMatch(),
            MatchRank = resolved?.Rank,
            MatchWasFuzzy = resolved is not null && !resolved.Spec.Exact,
            CandidateCount = resolved?.CandidateCount ?? 0,
            MatchedSnippet = resolved?.Snippet,
            Detail = detail,
            Url = this.session.Page.Url,
            ConsoleErrors = this.session.DrainConsoleErrors(),
            DurationMs = 0,
        };

    private static async Task<string?> ActOnAsync(
        UiActionSpec action,
        ILocator locator,
        string? value,
        string? detail)
    {
        switch (action.Kind)
        {
            case UiActionKind.Click:
                await locator.ClickAsync().ConfigureAwait(false);
                return null;

            case UiActionKind.DoubleClick:
                await locator.DblClickAsync().ConfigureAwait(false);
                return null;

            case UiActionKind.RightClick:
                await locator.ClickAsync(new LocatorClickOptions { Button = MouseButton.Right }).ConfigureAwait(false);
                return null;

            case UiActionKind.Fill:
                await locator.FillAsync(value ?? string.Empty).ConfigureAwait(false);
                return detail;

            case UiActionKind.Type:
                await locator.PressSequentiallyAsync(value ?? string.Empty).ConfigureAwait(false);
                return detail;

            case UiActionKind.Press:
                await locator.PressAsync(value ?? throw new ArgumentException("A key press needs keys.")).ConfigureAwait(false);
                return detail;

            case UiActionKind.Select:
                await SelectNativeAsync(action, locator, value).ConfigureAwait(false);
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

            case UiActionKind.ScrollTo:
                await locator.ScrollIntoViewIfNeededAsync().ConfigureAwait(false);
                return "into view";

            case UiActionKind.Expect:
                await locator.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible }).ConfigureAwait(false);
                return "visible";

            default:
                throw new InvalidOperationException($"Action '{action.Kind}' does not act on an element.");
        }
    }

    private static async Task SelectNativeAsync(UiActionSpec action, ILocator locator, string? value)
    {
        string tag = await locator.EvaluateAsync<string>("el => el.tagName.toLowerCase()").ConfigureAwait(false);

        if (tag != "select")
        {
            // The one wrong answer would be to click around and hope: a component library's
            // select is a different contract, and there is a verb for it.
            throw new FrameworkStateException(
                $"The {action.Target!.Describe()} is a <{tag}>, not a native <select>, so Select " +
                "cannot drive it. Use Choose(...), which drives native lists and ARIA comboboxes alike.");
        }

        await locator.SelectOptionAsync(value ?? string.Empty).ConfigureAwait(false);
    }

    /// <summary>
    /// Answers one read: resolves the element when the source names one, asks the reader, and writes the
    /// variable in the source's own type.
    /// </summary>
    private async Task<(UiResolvedTarget? Resolved, string? Detail)> ReadAsync(UiActionSpec action, CancellationToken cancellationToken)
    {
        UiValueSource source = action.Source!;

        if (source.Kind == UiValueKind.Count)
        {
            // A count is a question about the whole page, not about one element, so it goes to the
            // resolver's ladder rather than through single-element resolution - zero and many are both
            // answers here, not failures.
            (int count, UiQuerySpec? spec) = await TargetResolver.CountAsync(
                this.query,
                source.Target!,
                action.Context,
                this.resolutionOptions,
                this.app,
                this.session.Page.Url,
                cancellationToken).ConfigureAwait(false);

            this.variableStore.SetVariable(action.CaptureName!, count);

            return (null, spec is null ? "0 (no channel matched)" : $"{count} via {spec.DescribeMatch()}");
        }

        UiResolvedTarget? resolved = source.Target is null
            ? null
            : await this.ResolveAsync(action, cancellationToken).ConfigureAwait(false);

        UiReadResult result = await UiValueReader.ReadAsync(
            source,
            this.session.Page,
            resolved is null ? null : this.query.Locate(resolved),
            resolved,
            cancellationToken).ConfigureAwait(false);

        this.StoreRead(action, result);

        return (resolved, result.Detail);
    }

    private void StoreRead(UiActionSpec action, UiReadResult result)
    {
        switch (result.TypedValue)
        {
            case bool flag:
                this.variableStore.SetVariable(action.CaptureName!, flag);
                break;
            case int number:
                this.variableStore.SetVariable(action.CaptureName!, number);
                break;
            // A credential travels in the variable and nowhere else: the run and the test read it, and the
            // debugger, the value files and an assertion's message say "(secret)".
            case string credential when result.IsCredential:
                this.variableStore.SetSecretVariable(action.CaptureName!, credential);
                break;
            default:
                this.variableStore.SetVariable(action.CaptureName!, (string)result.TypedValue);
                break;
        }
    }

    /// <summary>
    /// Runs one script action: resolves the element when the action names one, runs the script, and for
    /// an evaluation writes the variable in the type the test asked for.
    /// </summary>
    private async Task<(UiResolvedTarget? Resolved, string? Detail)> RunScriptAsync(UiActionSpec action, CancellationToken cancellationToken)
    {
        JsScript script = action.Script!;

        UiResolvedTarget? resolved = action.Target is null
            ? null
            : await this.ResolveAsync(action, cancellationToken).ConfigureAwait(false);

        Newtonsoft.Json.Linq.JToken? result = await UiScriptRunner.RunAsync(
            script,
            this.session.Page,
            resolved is null ? null : this.query.Locate(resolved),
            this.variableStore,

            // A flow acts once rather than polling, so it keeps the page's action timeout. The budget is
            // for the waits, whose deadline has a grace window to protect.
            ProbeBudget.Unbounded,
            cancellationToken).ConfigureAwait(false);

        if (action.Kind == UiActionKind.Execute)
        {
            return (resolved, $"'{script.Name}'");
        }

        action.ResultBinder!.Bind(this.variableStore, action.CaptureName!, result, script.Name);

        // The raw result, shortened: enough to see what came back without a screen of JSON.
        return (resolved, $"'{script.Name}' -> {UiText.Truncate(PageJson.Describe(result!), 80)}");
    }

    private Task<UiResolvedTarget> ResolveAsync(UiActionSpec action, CancellationToken cancellationToken)
        => this.ResolveTargetAsync(
            action.Target ?? throw new InvalidOperationException($"Action '{action.Kind}' needs a target."),
            action.Context,
            cancellationToken);

    private async Task<UiResolvedTarget> ResolveTargetAsync(UiTarget target, UiSmartContext context, CancellationToken cancellationToken)
    {
        // Stopwatch, not wall clock: one clock per deadline, and this budget must not jump with the
        // system time. The loop stays bounded by the step token either way.
        Stopwatch budget = Stopwatch.StartNew();

        while (true)
        {
            try
            {
                return await TargetResolver
                    .ResolveAsync(this.query, target, context, this.resolutionOptions, this.app, this.session.Page.Url, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (UiTargetNotFoundException) when (budget.Elapsed < this.config.EffectiveActionTimeout)
            {
                // Not found may simply mean not yet: an application that renders after fetching data is
                // normal, and waiting for it is the framework's job rather than the test's. Ambiguity is
                // deliberately not retried - it is a real answer about the page, not a transient one.
                await Task.Delay(TimeSpan.FromMilliseconds(120), cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task ExpectGoneAsync(UiActionSpec action, CancellationToken cancellationToken)
    {
        UiTarget target = action.Target ?? throw new InvalidOperationException("An absence expectation needs a target.");

        // Stopwatch, not wall clock: one clock per deadline, and this budget must not jump with the
        // system time. The loop stays bounded by the step token either way.
        Stopwatch budget = Stopwatch.StartNew();

        while (true)
        {
            UiResolvedTarget? resolved = null;

            try
            {
                resolved = await TargetResolver
                    .ResolveAsync(this.query, target, UiSmartContext.Text, this.resolutionOptions, this.app, this.session.Page.Url, cancellationToken)
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

            if (resolved is not null && !await this.query.Locate(resolved).IsVisibleAsync().ConfigureAwait(false))
            {
                return;
            }

            if (budget.Elapsed >= this.config.EffectiveActionTimeout)
            {
                throw new TimeoutException(
                    $"The {target.Describe()} was still on the page after {this.config.EffectiveActionTimeout.TotalSeconds:F0}s, " +
                    $"at {this.session.Page.Url}. An absence expectation waits for something to go away; if it was never " +
                    "supposed to be there at all, expect the state that replaces it instead.");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(120), cancellationToken).ConfigureAwait(false);
        }
    }
}
