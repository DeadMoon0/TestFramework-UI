using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Playwright;
using TestFramework.Core.Steps;
using TestFramework.Core.Variables;
using TestFramework.UI.Browser.Identifier;
using TestFramework.UI.Browser.Resolution;
using TestFramework.UI.Browser.Runtime;
using TestFramework.UI.Browser.Structure;
using TestFramework.UI.Browser.Targeting;
using TestFramework.UI.Structure;

namespace TestFramework.UI.Browser.Events;

/// <summary>
/// The shared shape of a wait on what a part of the page is built like.
/// </summary>
/// <remarks>
/// <para>
/// The comparison steps answer "is the structure right, once the page settles"; these events answer
/// "wait until it is" - for the shape another actor produces over time: rows arriving from a background
/// job, a panel assembling as data streams in. Satisfied means zero differences, by exactly the same
/// comparison the steps use, so a wait and a check can never disagree about what matching means.
/// </para>
/// <para>
/// A wait never guesses: while the scope is missing, or matches several elements, the wait simply is not
/// over. Whatever the last look found - not there, several of it, or differing in three ways - is what
/// the timeout reports, difference by difference, so a wait that never matched ends as readably as a
/// comparison that failed.
/// </para>
/// </remarks>
/// <typeparam name="TEvent">The concrete event type.</typeparam>
public abstract class UiShapeEvent<TEvent> : UiEvent<TEvent>
    where TEvent : UiShapeEvent<TEvent>
{
    private readonly UiTarget scope;

    private string lastLook = "the page was never looked at";
    private IReadOnlyList<string> lastDifferences = [];
    private string? suggestedCode;

    private protected UiShapeEvent(WebAppIdentifier app, UiTarget scope, VariableReference<TimeSpan>? pollDelay)
        : base(app, pollDelay)
    {
        ArgumentNullException.ThrowIfNull(scope);

        this.scope = scope;
    }

    /// <summary>The element whose shape is watched, for a clone.</summary>
    private protected UiTarget Scope => this.scope;

    /// <summary>What the scope is compared against, in one line.</summary>
    private protected abstract string DescribeExpected();

    /// <summary>
    /// Compares the resolved scope against the expectation.
    /// </summary>
    /// <param name="element">The scope element.</param>
    /// <param name="cancellationToken">Cancels the look.</param>
    /// <returns>The differences, and the expectation that would pass, for the timeout.</returns>
    private protected abstract Task<(IReadOnlyList<string> Differences, string SuggestedCode)> CompareAsync(
        ILocator element,
        CancellationToken cancellationToken);

    /// <inheritdoc />
    private protected sealed override string DescribeWaited(VariableStore variableStore)
        => $"{this.scope.Describe()} matching {this.DescribeExpected()}";

    /// <inheritdoc />
    private protected sealed override string TimeoutAdvice(VariableStore variableStore)
    {
        if (this.lastDifferences.Count == 0)
        {
            return $"On the last look, {this.lastLook}.";
        }

        // The last diff is the whole diagnosis: what the page held when time ran out, said the same way
        // a failed comparison step says it - and with the expectation that would have passed, ready to
        // read against the intended one.
        string differences = string.Join("\n", this.lastDifferences.Select(static difference => "  " + difference));

        return $"On the last look it differed in {this.lastDifferences.Count} way(s):\n{differences}"
            + (this.suggestedCode is null
                ? string.Empty
                : $"\nThe expectation the page did satisfy at that moment:\n\n{this.suggestedCode}\n");
    }

    /// <inheritdoc />
    private protected sealed override async Task<UiProbeOutcome> ProbeAsync(
        UiSession session,
        PlaywrightElementQuery query,
        UiResolutionOptions options,
        VariableStore variableStore,
        CancellationToken cancellationToken)
    {
        (int count, UiQuerySpec? spec) = await TargetResolver.CountAsync(
            query,
            this.scope,
            UiSmartContext.Section,
            options,
            this.App,
            session.Page.Url,
            cancellationToken).ConfigureAwait(false);

        if (count == 0 || spec is null)
        {
            this.lastLook = $"{this.scope.Describe()} was not on the page at all";
            this.lastDifferences = [];

            return new UiProbeOutcome(false);
        }

        if (count > 1)
        {
            // Comparing "the first of several" would be a guess; several may also be a page mid-render,
            // which the next poll sees settled. Either way the wait is simply not over.
            this.lastLook = $"{count} elements answered to {this.scope.Describe()}, so there was no single scope to compare";
            this.lastDifferences = [];

            return new UiProbeOutcome(false);
        }

        try
        {
            (IReadOnlyList<string> differences, string suggested) = await this
                .CompareAsync(query.Locate(new UiResolvedTarget(spec, 0, 0, count, null)), cancellationToken)
                .ConfigureAwait(false);

            this.lastDifferences = differences;
            this.suggestedCode = suggested;
            this.lastLook = differences.Count == 0 ? "it matched" : $"it differed in {differences.Count} way(s)";

            return new UiProbeOutcome(differences.Count == 0, spec.DescribeMatch());
        }
        catch (PlaywrightException)
        {
            // The element was there when counted and gone when read - a page mid-swap. That is "not yet",
            // not an error; the next poll sees whatever replaced it.
            this.lastLook = $"{this.scope.Describe()} changed while it was being read";
            this.lastDifferences = [];

            return new UiProbeOutcome(false);
        }
    }
}

/// <summary>
/// Completes when a part of the page is built the way a structure says.
/// </summary>
public sealed class UiStructureMatchesEvent : UiShapeEvent<UiStructureMatchesEvent>
{
    private readonly WebElementStructure structure;
    private readonly UiExpectedNode expected;

    internal UiStructureMatchesEvent(
        WebAppIdentifier app,
        UiTarget scope,
        WebElementStructure structure,
        VariableReference<TimeSpan>? pollDelay)
        : base(app, scope, pollDelay)
    {
        ArgumentNullException.ThrowIfNull(structure);

        this.structure = structure;

        // Compiled once, which also freezes the structure - a field shared with a comparison step cannot
        // be changed by either after the first of them has used it.
        this.expected = structure.Compile();
    }

    /// <inheritdoc />
    public override string Name => "UI Structure Matches Event";

    /// <inheritdoc />
    public override string Description => $"Completes when the scope on '{this.App}' is {this.structure.Describe()}.";

    /// <inheritdoc />
    private protected override string ActionName => "WaitStructure";

    /// <inheritdoc />
    public override Step<UiWaitResultContext> Clone()
        => new UiStructureMatchesEvent(this.App, this.Scope, this.structure, this.PollDelay).WithClonedOptions(this);

    /// <inheritdoc />
    private protected override string DescribeExpected() => this.structure.Describe();

    /// <inheritdoc />
    private protected override async Task<(IReadOnlyList<string> Differences, string SuggestedCode)> CompareAsync(
        ILocator element,
        CancellationToken cancellationToken)
    {
        UiElementSnapshot snapshot = await DomProjector.ProjectAsync(element, cancellationToken).ConfigureAwait(false);

        return (
            StructureDiffer.Compare(this.expected, snapshot).Select(static difference => difference.ToString()).ToList(),
            StructureCodeRenderer.Render(snapshot));
    }
}

/// <summary>
/// Completes when a table on the page holds what an expected table says.
/// </summary>
public sealed class UiTableMatchesEvent : UiShapeEvent<UiTableMatchesEvent>
{
    private readonly ExpectedTable expected;

    internal UiTableMatchesEvent(
        WebAppIdentifier app,
        UiTarget table,
        ExpectedTable expected,
        VariableReference<TimeSpan>? pollDelay)
        : base(app, table, pollDelay)
    {
        ArgumentNullException.ThrowIfNull(expected);

        this.expected = expected;
    }

    /// <inheritdoc />
    public override string Name => "UI Table Matches Event";

    /// <inheritdoc />
    public override string Description => $"Completes when the table on '{this.App}' holds the expected {this.expected.RowCount} row(s).";

    /// <inheritdoc />
    private protected override string ActionName => "WaitTable";

    /// <inheritdoc />
    public override Step<UiWaitResultContext> Clone()
        => new UiTableMatchesEvent(this.App, this.Scope, this.expected, this.PollDelay).WithClonedOptions(this);

    /// <inheritdoc />
    private protected override string DescribeExpected() => $"the expected {this.expected.RowCount} row(s)";

    /// <inheritdoc />
    private protected override async Task<(IReadOnlyList<string> Differences, string SuggestedCode)> CompareAsync(
        ILocator element,
        CancellationToken cancellationToken)
    {
        UiTableSnapshot snapshot = await DomTableReader.ReadAsync(element, cancellationToken).ConfigureAwait(false);

        return (
            this.expected.Compare(snapshot).Select(static difference => difference.ToString()).ToList(),
            StructureCodeRenderer.Render(snapshot));
    }
}
