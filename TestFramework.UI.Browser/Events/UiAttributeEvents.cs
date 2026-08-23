using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Playwright;
using TestFramework.Core.Steps;
using TestFramework.Core.Variables;
using TestFramework.UI.Browser.Identifier;
using TestFramework.UI.Browser.Resolution;
using TestFramework.UI.Browser.Runtime;
using TestFramework.UI.Browser.Targeting;
using TestFramework.UI.Structure;

namespace TestFramework.UI.Browser.Events;

/// <summary>
/// Completes when an attribute of an element reads a value a rule accepts.
/// </summary>
/// <remarks>
/// Attributes are where components say what they are up to - <c>data-state</c>, <c>aria-busy</c>,
/// <c>aria-expanded</c> - often without a visible word changing. This wait watches that channel the way
/// <c>TextAppears</c> watches the visible one. The element does not have to exist yet when the wait
/// starts; an element that is not there simply is not "reading" anything.
/// </remarks>
public sealed class UiAttributeEqualsEvent : UiEvent<UiAttributeEqualsEvent>
{
    private readonly UiTarget target;
    private readonly string attribute;
    private readonly CellRule expected;

    private bool sawElement;
    private string? lastSeen;

    internal UiAttributeEqualsEvent(
        WebAppIdentifier app,
        UiTarget target,
        string attribute,
        CellRule expected,
        VariableReference<TimeSpan>? pollDelay)
        : base(app, pollDelay)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentException.ThrowIfNullOrWhiteSpace(attribute);
        ArgumentNullException.ThrowIfNull(expected);

        this.target = target;
        this.attribute = attribute;
        this.expected = expected;
    }

    /// <inheritdoc />
    public override string Name => "UI Attribute Equals Event";

    /// <inheritdoc />
    public override string Description
        => $"Completes when attribute '{this.attribute}' of {this.target.Describe()} on '{this.App}' reads a value that {this.expected.Description}.";

    /// <inheritdoc />
    private protected override string ActionName => "WaitAttribute";

    /// <inheritdoc />
    public override Step<UiWaitResultContext> Clone()
        => new UiAttributeEqualsEvent(this.App, this.target, this.attribute, this.expected, this.PollDelay).WithClonedOptions(this);

    /// <inheritdoc />
    private protected override string DescribeWaited(VariableStore variableStore)
        => $"attribute '{this.attribute}' of {this.target.Describe()} reading a value that {this.expected.Description}";

    /// <inheritdoc />
    private protected override string TimeoutAdvice(VariableStore variableStore)
        => this.sawElement
            ? $"On the last look the attribute read '{this.lastSeen ?? "(absent)"}'. If that is the value the page "
                + "settles on, the expectation names the wrong value; if the page moves on from it, the wait needs "
                + "a longer step timeout."
            : "The element itself never appeared, so there was no attribute to watch. Check the target first.";

    /// <inheritdoc />
    private protected override void OnPollingStarting()
    {
        this.sawElement = false;
        this.lastSeen = null;
    }

    /// <inheritdoc />
    private protected override async Task<UiProbeOutcome> ProbeAsync(
        UiSession session,
        PlaywrightElementQuery query,
        UiResolutionOptions options,
        VariableStore variableStore,
        CancellationToken cancellationToken)
    {
        AttributeLook look = await AttributeProbe
            .LookAsync(query, this.target, this.attribute, options, this.App, session, cancellationToken)
            .ConfigureAwait(false);

        if (!look.Found)
        {
            return new UiProbeOutcome(false);
        }

        this.sawElement = true;
        this.lastSeen = look.Value;

        bool satisfied = this.expected.Matches(look.Value);

        return new UiProbeOutcome(satisfied, satisfied ? look.ResolvedVia : null);
    }
}

/// <summary>
/// Completes when an attribute of an element no longer reads what this wait first saw.
/// </summary>
/// <remarks>
/// For the transitions that have no destination value a test could name - a version stamp, a refresh
/// counter, an id that rotates. The baseline is the first value the wait sees, taken on its first look
/// at the element; a change that happens before the wait starts is therefore invisible to it. When the
/// destination IS known, <see cref="UiAttributeEqualsEvent"/> says it better - a wait for "changed away
/// from X" passes on any change, including a change to "broken".
/// </remarks>
public sealed class UiAttributeChangedEvent : UiEvent<UiAttributeChangedEvent>
{
    private readonly UiTarget target;
    private readonly string attribute;

    private bool baselineTaken;
    private string? baseline;

    internal UiAttributeChangedEvent(
        WebAppIdentifier app,
        UiTarget target,
        string attribute,
        VariableReference<TimeSpan>? pollDelay)
        : base(app, pollDelay)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentException.ThrowIfNullOrWhiteSpace(attribute);

        this.target = target;
        this.attribute = attribute;
    }

    /// <inheritdoc />
    public override string Name => "UI Attribute Changed Event";

    /// <inheritdoc />
    public override string Description
        => $"Completes when attribute '{this.attribute}' of {this.target.Describe()} on '{this.App}' changes from what the wait first saw.";

    /// <inheritdoc />
    private protected override string ActionName => "WaitAttributeChange";

    /// <inheritdoc />
    public override Step<UiWaitResultContext> Clone()
        => new UiAttributeChangedEvent(this.App, this.target, this.attribute, this.PollDelay).WithClonedOptions(this);

    /// <inheritdoc />
    private protected override string DescribeWaited(VariableStore variableStore)
        => $"attribute '{this.attribute}' of {this.target.Describe()} changing from what the wait first saw";

    /// <inheritdoc />
    private protected override string TimeoutAdvice(VariableStore variableStore)
        => this.baselineTaken
            ? $"It still read '{this.baseline ?? "(absent)"}', the value the wait's first look saw. A change that "
                + "happens before the wait starts is invisible to it - if the page changes the attribute in "
                + "response to a step, put this wait after that step and nothing else in between."
            : "The element itself never appeared, so there was no value to take as the baseline. Check the target first.";

    /// <inheritdoc />
    private protected override void OnPollingStarting()
    {
        this.baselineTaken = false;
        this.baseline = null;
    }

    /// <inheritdoc />
    private protected override async Task<UiProbeOutcome> ProbeAsync(
        UiSession session,
        PlaywrightElementQuery query,
        UiResolutionOptions options,
        VariableStore variableStore,
        CancellationToken cancellationToken)
    {
        AttributeLook look = await AttributeProbe
            .LookAsync(query, this.target, this.attribute, options, this.App, session, cancellationToken)
            .ConfigureAwait(false);

        if (!look.Found)
        {
            return new UiProbeOutcome(false);
        }

        if (!this.baselineTaken)
        {
            this.baselineTaken = true;
            this.baseline = look.Value;

            return new UiProbeOutcome(false);
        }

        bool changed = !string.Equals(look.Value, this.baseline, StringComparison.Ordinal);

        return new UiProbeOutcome(changed, changed ? look.ResolvedVia : null);
    }
}

/// <summary>
/// What one look at an attribute found.
/// </summary>
/// <param name="Found">True when the element is on the page.</param>
/// <param name="Value">The attribute's value, or null for an attribute the element does not carry.</param>
/// <param name="ResolvedVia">Which lookup channel answered.</param>
internal sealed record AttributeLook(bool Found, string? Value, string? ResolvedVia);

/// <summary>
/// The one look the attribute waits share: find the element, read the attribute, judge nothing.
/// </summary>
internal static class AttributeProbe
{
    public static async Task<AttributeLook> LookAsync(
        PlaywrightElementQuery query,
        UiTarget target,
        string attribute,
        UiResolutionOptions options,
        string app,
        UiSession session,
        CancellationToken cancellationToken)
    {
        // The counting form of the ladder, like the other waits: several matches are not an ambiguity
        // here, and zero is "not yet". The first match is the one watched - the same one a verb would
        // act on.
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
            return new AttributeLook(false, null, null);
        }

        ILocator element = query.Locate(new UiResolvedTarget(spec, 0, 0, count, null));
        string? value = await element.GetAttributeAsync(attribute).ConfigureAwait(false);

        return new AttributeLook(true, value, spec.DescribeMatch());
    }
}
