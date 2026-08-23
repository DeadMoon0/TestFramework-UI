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
/// Checks that a table on the page holds what a test says it should.
/// </summary>
internal sealed class CompareTableStep : UiInspectionStep<UiCompareResultContext>
{
    private readonly ExpectedTable expected;

    public CompareTableStep(WebAppIdentifier app, UiTarget table, ExpectedTable expected)
        : base(app, table)
    {
        ArgumentNullException.ThrowIfNull(expected);

        this.expected = expected;
    }

    /// <inheritdoc />
    public override string Name => "UI Compare Table";

    /// <inheritdoc />
    public override string Description
        => $"Checks {this.expected.RowCount} row(s) of {this.Target.Describe()} on '{this.App}'";

    /// <inheritdoc />
    protected override string ActionName => "CompareTable";

    /// <inheritdoc />
    public override Step<UiCompareResultContext> Clone()
        => new CompareTableStep(this.App, this.Target, this.expected).WithClonedOptions(this);

    /// <inheritdoc />
    public override StepInstance<Step<UiCompareResultContext>, UiCompareResultContext> GetInstance()
        => new StepInstance<Step<UiCompareResultContext>, UiCompareResultContext>(this);

    /// <inheritdoc />
    protected override async Task<(UiCompareResultContext Result, string? Detail)> InspectAsync(
        ILocator locator,
        CancellationToken cancellationToken)
    {
        UiTableSnapshot snapshot = await DomTableReader.ReadAsync(locator, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<UiDifference> differences = this.expected.Compare(snapshot);

        UiCompareResultContext result = new UiCompareResultContext(
            this.App,
            locator.Page.Url,
            this.Target.Describe(),
            differences.Count == 0,
            differences.Select(static difference => difference.ToString()).ToList(),
            snapshot.ToString(),
            differences.Count == 0 ? null : StructureCodeRenderer.Render(snapshot));

        string detail = differences.Count == 0
            ? $"matched ({snapshot.Rows.Count} rows)"
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
