using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Core;
using TestFramework.Core.Steps;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Variables;
using TestFramework.UI.Browser.Identifier;
using TestFramework.UI.Browser.Resolution;
using TestFramework.UI.Browser.Runtime;

namespace TestFramework.UI.Browser.Events;

/// <summary>
/// Completes when the page's address contains a text, or matches an expression.
/// </summary>
/// <remarks>
/// For the navigations another actor causes - a payment provider redirecting back, a login flow landing
/// where it should. Plain text is a case-sensitive substring of the address; <see cref="AsRegex"/> makes
/// it a regular expression instead, for the address whose interesting part is a shape rather than a
/// wording.
/// </remarks>
public sealed class UiUrlMatchesEvent : UiEvent<UiUrlMatchesEvent>
{
    private readonly VariableReference<string> pattern;
    private bool asRegex;

    internal UiUrlMatchesEvent(WebAppIdentifier app, VariableReference<string> pattern, VariableReference<TimeSpan>? pollDelay)
        : base(app, pollDelay)
    {
        ArgumentNullException.ThrowIfNull(pattern);

        this.pattern = pattern;
    }

    /// <inheritdoc />
    public override string Name => "UI Url Matches Event";

    /// <inheritdoc />
    public override string Description => $"Completes when the address of '{this.App}' matches the pattern.";

    /// <inheritdoc />
    private protected override string ActionName => "WaitUrl";

    /// <summary>
    /// Treats the pattern as a regular expression instead of as a substring.
    /// </summary>
    /// <returns>The same event, for chaining.</returns>
    public UiUrlMatchesEvent AsRegex()
    {
        ((IFreezable)this).EnsureNotFrozen();
        this.asRegex = true;

        return this;
    }

    /// <inheritdoc />
    public override Step<UiWaitResultContext> Clone()
    {
        UiUrlMatchesEvent clone = new UiUrlMatchesEvent(this.App, this.pattern, this.PollDelay)
        {
            asRegex = this.asRegex,
        };

        return clone.WithClonedOptions(this);
    }

    /// <inheritdoc />
    private protected override void DeclareOwnIO(StepIOContract contract)
    {
        if (this.pattern.Identifier is { } identifier)
        {
            contract.Inputs.Add(new StepIOEntry(identifier.Identifier, StepIOKind.Variable, true, typeof(string)));
        }
    }

    /// <inheritdoc />
    private protected override string DescribeWaited(VariableStore variableStore)
        => this.asRegex
            ? $"an address matching /{this.pattern.GetValue(variableStore)}/"
            : $"an address containing '{this.pattern.GetValue(variableStore)}'";

    /// <inheritdoc />
    private protected override string TimeoutAdvice(VariableStore variableStore)
        => "The address never changed to match. If another step is supposed to cause the navigation, "
        + "check that it ran before this wait in the timeline.";

    /// <inheritdoc />
    private protected override Task<UiProbeOutcome> ProbeAsync(
        UiSession session,
        PlaywrightElementQuery query,
        UiResolutionOptions options,
        VariableStore variableStore,
        CancellationToken cancellationToken)
    {
        string wanted = this.pattern.GetValue(variableStore)
            ?? throw new InvalidOperationException("The address pattern resolved to nothing.");

        bool matches = this.asRegex
            ? Regex.IsMatch(session.Page.Url, wanted)
            : session.Page.Url.Contains(wanted, StringComparison.Ordinal);

        return Task.FromResult(new UiProbeOutcome(matches));
    }
}
