using TestFramework.UI.Browser.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Playwright;
using TestFramework.Core.Steps;
using TestFramework.UI.Browser.Exceptions;
using TestFramework.UI.Browser.Identifier;
using TestFramework.UI.Browser.Structure;
using TestFramework.UI.Browser.Targeting;
using TestFramework.UI.Structure;

namespace TestFramework.UI.Browser.Steps.Inspection;

/// <summary>
/// Checks that a part of the page is built the way a test says it should be.
/// </summary>
internal sealed class CompareStructureStep : UiInspectionStep<UiCompareResultContext>
{
    private readonly WebElementStructure structure;
    private readonly UiExpectedNode expected;

    public CompareStructureStep(WebAppIdentifier app, UiTarget scope, WebElementStructure structure)
        : base(app, scope)
    {
        ArgumentNullException.ThrowIfNull(structure);

        this.structure = structure;

        // Compiled once, here, which also freezes the structure - so a field shared by several timelines
        // cannot be changed by one of them after another has started using it.
        this.expected = structure.Compile();
    }

    /// <inheritdoc />
    public override string Name => "UI Compare Structure";

    /// <inheritdoc />
    public override string Description => $"Checks that {this.Target.Describe()} on '{this.App}' is {this.structure.Describe()}";

    /// <inheritdoc />
    protected override string ActionName => "CompareStructure";

    /// <inheritdoc />
    public override Step<UiCompareResultContext> Clone()
        => new CompareStructureStep(this.App, this.Target, this.structure).WithClonedOptions(this);

    /// <inheritdoc />
    public override StepInstance<Step<UiCompareResultContext>, UiCompareResultContext> GetInstance()
        => new StepInstance<Step<UiCompareResultContext>, UiCompareResultContext>(this);

    /// <inheritdoc />
    protected override async Task<(UiCompareResultContext Result, string? Detail)> InspectAsync(
        ILocator? locator,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(locator);

        UiElementSnapshot snapshot = await DomProjector.ProjectAsync(locator, ProbeBudget.Unbounded, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<UiDifference> differences = StructureDiffer.Compare(this.expected, snapshot);

        UiCompareResultContext result = new UiCompareResultContext(
            this.App,
            locator.Page.Url,
            this.Target.Describe(),
            differences.Count == 0,
            differences.Select(static difference => difference.ToString()).ToList(),
            DomProjector.Render(snapshot),
            differences.Count == 0 ? null : StructureCodeRenderer.Render(snapshot));

        string detail = differences.Count == 0
            ? $"matched ({snapshot.Size()} elements)"
            : $"{differences.Count} difference(s)";

        return (result, detail);
    }

    /// <inheritdoc />
    protected override bool ShouldRetry(UiCompareResultContext result) => !result.IsMatch;

    /// <inheritdoc />
    protected override Exception? Verdict(UiCompareResultContext result, TimeSpan waited)
        => result.IsMatch
            ? null
            : new UiStructureMismatchException(
                result.App,
                result.Url,
                result.Scope,
                result.Differences,
                result.Actual,
                result.SuggestedCode,
                waited);
}
