using System;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Core.Steps;
using TestFramework.Core.Variables;
using TestFramework.UI.Browser.Identifier;
using TestFramework.UI.Browser.Resolution;
using TestFramework.UI.Browser.Runtime;
using TestFramework.UI.Browser.Targeting;

namespace TestFramework.UI.Browser.Events;

/// <summary>
/// Completes when the number of elements answering to a target reaches an expected count.
/// </summary>
/// <remarks>
/// For lists that fill in - rows arriving over HTTP, results streaming into a feed - where "the third
/// one is there" says more than any single element could. Counted on the first lookup channel that
/// finds anything, in resolution order, so what gets counted is what the same target would act on.
/// The exact form is also satisfied by a page that briefly passes through the expected count on its way
/// past it; a list that overshoots is better waited on with its final shape, via a structure or table
/// event.
/// </remarks>
public sealed class UiElementCountEvent : UiEvent<UiElementCountEvent>
{
    private readonly UiTarget target;
    private readonly int expected;
    private readonly bool atLeast;

    private int? lastCount;

    internal UiElementCountEvent(
        WebAppIdentifier app,
        UiTarget target,
        int expected,
        bool atLeast,
        VariableReference<TimeSpan>? pollDelay)
        : base(app, pollDelay)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentOutOfRangeException.ThrowIfNegative(expected);

        this.target = target;
        this.expected = expected;
        this.atLeast = atLeast;
    }

    /// <inheritdoc />
    public override string Name => "UI Element Count Event";

    /// <inheritdoc />
    public override string Description
        => $"Completes when {this.target.Describe()} on '{this.App}' numbers {this.Wording} {this.expected}.";

    /// <inheritdoc />
    private protected override string ActionName => "WaitCount";

    private string Wording => this.atLeast ? "at least" : "exactly";

    /// <inheritdoc />
    public override Step<UiWaitResultContext> Clone()
        => new UiElementCountEvent(this.App, this.target, this.expected, this.atLeast, this.PollDelay).WithClonedOptions(this);

    /// <inheritdoc />
    private protected override string DescribeWaited(VariableStore variableStore)
        => $"the count of {this.target.Describe()} reaching {this.Wording} {this.expected}";

    /// <inheritdoc />
    private protected override string TimeoutAdvice(VariableStore variableStore)
        => this.lastCount is { } count
            ? $"The last look counted {count}. If the page is still producing them, the wait needs a longer "
                + "step timeout; if it has settled, the expectation names the wrong number."
            : "No lookup channel ever matched, so there was nothing to count. Check the target first.";

    /// <inheritdoc />
    private protected override void OnPollingStarting() => this.lastCount = null;

    /// <inheritdoc />
    private protected override async Task<UiProbeOutcome> ProbeAsync(
        UiSession session,
        PlaywrightElementQuery query,
        UiResolutionOptions options,
        VariableStore variableStore,
        CancellationToken cancellationToken)
    {
        (int count, UiQuerySpec? spec) = await TargetResolver.CountAsync(
            query,
            this.target,
            UiSmartContext.Text,
            options,
            this.App,
            session.Page.Url,
            cancellationToken).ConfigureAwait(false);

        if (spec is not null)
        {
            this.lastCount = count;
        }

        bool satisfied = this.atLeast ? count >= this.expected : count == this.expected;

        // A count of zero satisfies "exactly 0" even though no channel matched - absence is the answer
        // being waited for there, exactly like ElementHidden.
        return new UiProbeOutcome(satisfied, satisfied ? spec?.DescribeMatch() : null);
    }
}
