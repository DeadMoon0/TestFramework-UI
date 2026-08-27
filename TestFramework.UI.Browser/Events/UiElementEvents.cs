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
/// Completes when an element is on the page and visible.
/// </summary>
/// <remarks>
/// The question is "is it there", not "act on it", so several matches satisfy the wait where an action
/// would call them ambiguous: two error banners are certainly a visible error banner. The probe counts
/// with the same lookup ladder the verbs resolve with, so what satisfies the wait is what a verb would
/// find.
/// </remarks>
public sealed class UiElementVisibleEvent : UiEvent<UiElementVisibleEvent>
{
    private readonly UiTarget target;

    internal UiElementVisibleEvent(WebAppIdentifier app, UiTarget target, VariableReference<TimeSpan>? pollDelay)
        : base(app, pollDelay)
    {
        ArgumentNullException.ThrowIfNull(target);

        this.target = target;
    }

    /// <inheritdoc />
    public override string Name => "UI Element Visible Event";

    /// <inheritdoc />
    public override string Description => $"Completes when {this.target.Describe()} is visible on '{this.App}'.";

    /// <inheritdoc />
    private protected override string ActionName => "WaitVisible";

    /// <inheritdoc />
    public override Step<UiWaitResultContext> Clone()
        => new UiElementVisibleEvent(this.App, this.target, this.PollDelay).WithClonedOptions(this);

    /// <inheritdoc />
    private protected override string DescribeWaited(VariableStore variableStore) => this.target.Describe();

    /// <inheritdoc />
    private protected override string TimeoutAdvice(VariableStore variableStore)
        => "If the page renders it after data arrives, the wait may need a longer step timeout; if the "
        + "wording changed, the target needs the new words.";

    /// <inheritdoc />
    private protected override async Task<UiProbeOutcome> ProbeAsync(
        UiSession session,
        PlaywrightElementQuery query,
        UiResolutionOptions options,
        VariableStore variableStore,
        ProbeBudget budget,
        CancellationToken cancellationToken)
        => await ElementProbe.VisibleAsync(query, this.target, options, this.App, session, cancellationToken).ConfigureAwait(false);
}

/// <summary>
/// Completes when an element is gone from the page, or was never there.
/// </summary>
/// <remarks>
/// "Was never there" satisfies it on purpose: the wait asserts a state, not a transition, so it does not
/// fail when the thing disappeared faster than the first poll. A test that needs to see the transition
/// waits for the element first.
/// </remarks>
public sealed class UiElementHiddenEvent : UiEvent<UiElementHiddenEvent>
{
    private readonly UiTarget target;

    internal UiElementHiddenEvent(WebAppIdentifier app, UiTarget target, VariableReference<TimeSpan>? pollDelay)
        : base(app, pollDelay)
    {
        ArgumentNullException.ThrowIfNull(target);

        this.target = target;
    }

    /// <inheritdoc />
    public override string Name => "UI Element Hidden Event";

    /// <inheritdoc />
    public override string Description => $"Completes when {this.target.Describe()} is gone from '{this.App}'.";

    /// <inheritdoc />
    private protected override string ActionName => "WaitHidden";

    /// <inheritdoc />
    public override Step<UiWaitResultContext> Clone()
        => new UiElementHiddenEvent(this.App, this.target, this.PollDelay).WithClonedOptions(this);

    /// <inheritdoc />
    private protected override string DescribeWaited(VariableStore variableStore)
        => $"the disappearance of {this.target.Describe()}";

    /// <inheritdoc />
    private protected override string TimeoutAdvice(VariableStore variableStore)
        => "The element was still there on the last poll. If it is meant to go away in response to "
        + "something, check that the something actually happened.";

    /// <inheritdoc />
    private protected override async Task<UiProbeOutcome> ProbeAsync(
        UiSession session,
        PlaywrightElementQuery query,
        UiResolutionOptions options,
        VariableStore variableStore,
        ProbeBudget budget,
        CancellationToken cancellationToken)
    {
        UiProbeOutcome visible = await ElementProbe
            .VisibleAsync(query, this.target, options, this.App, session, cancellationToken)
            .ConfigureAwait(false);

        return new UiProbeOutcome(!visible.Satisfied);
    }
}

/// <summary>
/// The one look both element waits share: is the target on the page and visible right now.
/// </summary>
internal static class ElementProbe
{
    public static async Task<UiProbeOutcome> VisibleAsync(
        PlaywrightElementQuery query,
        UiTarget target,
        UiResolutionOptions options,
        string app,
        UiSession session,
        CancellationToken cancellationToken)
    {
        // The counting form of the ladder, because a wait has no act to be ambiguous about. Zero is
        // "not yet", never an error - that is the whole point of polling.
        (int count, UiQuerySpec? spec) = await TargetResolver.CountAsync(
            query,
            target,
            UiSmartContext.Text,
            options,
            app,
            session.Page.Url,
            cancellationToken).ConfigureAwait(false);

        if (count == 0 || spec is null)
        {
            return new UiProbeOutcome(false);
        }

        // Matched in the tree is not yet on the screen: display:none descendants of role lookups, or a
        // panel mid-close. Visibility of the first match answers immediately, without any waiting of its
        // own - the loop owns the waiting.
        bool visible = await query
            .Locate(new UiResolvedTarget(spec, 0, 0, count, null))
            .IsVisibleAsync()
            .ConfigureAwait(false);

        return new UiProbeOutcome(visible, visible ? spec.DescribeMatch() : null);
    }
}
