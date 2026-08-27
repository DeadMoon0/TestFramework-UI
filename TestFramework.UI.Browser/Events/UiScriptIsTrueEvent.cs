using System;
using Newtonsoft.Json.Linq;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Core.Steps;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Variables;
using TestFramework.UI.Browser.Identifier;
using TestFramework.UI.Browser.Resolution;
using TestFramework.UI.Browser.Runtime;
using TestFramework.UI.Browser.Scripting;
using TestFramework.UI.Structure;

namespace TestFramework.UI.Browser.Events;

/// <summary>
/// Completes when a script in the page returns <c>true</c>.
/// </summary>
/// <remarks>
/// The wait for application state no element shows - a store flag, a queue length, something on
/// <c>window</c> the page maintains. The contract is strict on purpose: the script must return a
/// boolean. <c>true</c> ends the wait, <c>false</c> keeps polling, and anything else fails immediately -
/// a script returning <c>undefined</c> forever would otherwise read as a timeout, pointing the reader at
/// the page when the bug is the script. Write <c>() =&gt; window.myApp?.ready === true</c>, and the
/// question always has a boolean answer.
/// </remarks>
public sealed class UiScriptIsTrueEvent : UiEvent<UiScriptIsTrueEvent>
{
    private readonly JsScript script;

    internal UiScriptIsTrueEvent(WebAppIdentifier app, JsScript script, VariableReference<TimeSpan>? pollDelay)
        : base(app, pollDelay)
    {
        ArgumentNullException.ThrowIfNull(script);

        this.script = script;
    }

    /// <inheritdoc />
    public override string Name => "UI Script Is True Event";

    /// <inheritdoc />
    public override string Description => $"Completes when the script '{this.script.Name}' returns true on '{this.App}'.";

    /// <inheritdoc />
    private protected override string ActionName => "WaitScript";

    /// <inheritdoc />
    public override Step<UiWaitResultContext> Clone()
        => new UiScriptIsTrueEvent(this.App, this.script, this.PollDelay).WithClonedOptions(this);

    /// <inheritdoc />
    private protected override void DeclareOwnIO(StepIOContract contract)
    {
        foreach ((string _, VariableReference<string> argument) in this.script.Arguments)
        {
            if (argument.Identifier is { } identifier)
            {
                contract.Inputs.Add(new StepIOEntry(identifier.Identifier, StepIOKind.Variable, true, typeof(string)));
            }
        }
    }

    /// <inheritdoc />
    private protected override string DescribeWaited(VariableStore variableStore)
        => $"the script '{this.script.Name}' returning true";

    /// <inheritdoc />
    private protected override string TimeoutAdvice(VariableStore variableStore)
        => "The script kept returning false. If the state it reads is set by another step, check that the "
        + "step ran; if it is set by the page itself, the page never got there.";

    /// <inheritdoc />
    private protected override async Task<UiProbeOutcome> ProbeAsync(
        UiSession session,
        PlaywrightElementQuery query,
        UiResolutionOptions options,
        VariableStore variableStore,
        ProbeBudget budget,
        CancellationToken cancellationToken)
    {
        JToken? result = await UiScriptRunner
            .RunAsync(this.script, session.Page, element: null, variableStore, budget, cancellationToken)
            .ConfigureAwait(false);

        if (result is { Type: JTokenType.Boolean })
        {
            return new UiProbeOutcome(result.Value<bool>());
        }

        // Not a pending state but a broken question - said now, with the fix, rather than as a timeout
        // that blames the page.
        throw new InvalidOperationException(
            $"The script '{this.script.Name}' returned "
            + (result is null ? "nothing" : UiText.Truncate(PageJson.Describe(result), 80))
            + ", and a wait needs a boolean. Phrase the question so it always has one, for example "
            + "'() => window.myApp?.ready === true'.");
    }
}
