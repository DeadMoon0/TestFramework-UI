using TestFramework.UI.Browser.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Playwright;
using TestFramework.Core.Steps;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Variables;
using TestFramework.UI.Browser.Identifier;
using TestFramework.UI.Browser.Structure;
using TestFramework.UI.Browser.Targeting;
using TestFramework.UI.Structure;

namespace TestFramework.UI.Browser.Steps.Inspection;

/// <summary>
/// Reads a table off the page as data, for the assertions or for a later step to use.
/// </summary>
/// <remarks>
/// The counterpart to comparing a table: sometimes a test is not about the table matching a fixed shape
/// but about something derived from it - that the totals add up, that nothing is sorted wrongly, that a
/// row exists at all. Reading gives it the rows and lets it say so in its own terms.
/// </remarks>
internal sealed class ReadTableStep : UiInspectionStep<UiTableResultContext>
{
    private readonly VariableIdentifier? into;

    public ReadTableStep(WebAppIdentifier app, UiTarget table, VariableIdentifier? into)
        : base(app, table)
        => this.into = into;

    /// <inheritdoc />
    public override string Name => "UI Read Table";

    /// <inheritdoc />
    public override string Description => $"Reads {this.Target.Describe()} on '{this.App}'";

    /// <inheritdoc />
    protected override string ActionName => "ReadTable";

    /// <inheritdoc />
    public override Step<UiTableResultContext> Clone()
        => new ReadTableStep(this.App, this.Target, this.into).WithClonedOptions(this);

    /// <inheritdoc />
    public override StepInstance<Step<UiTableResultContext>, UiTableResultContext> GetInstance()
        => new StepInstance<Step<UiTableResultContext>, UiTableResultContext>(this);

    /// <inheritdoc />
    protected override void DeclareOwnIO(StepIOContract contract)
    {
        if (this.into is { } identifier)
        {
            contract.Outputs.Add(new StepIOEntry(
                identifier.Identifier,
                StepIOKind.Variable,
                true,
                typeof(IReadOnlyList<IReadOnlyDictionary<string, string>>)));
        }
    }

    /// <inheritdoc />
    public override async Task<UiTableResultContext?> Execute(RunContext context)
    {
        UiTableResultContext? result = await base
            .Execute(context)
            .ConfigureAwait(false);

        if (result is not null && this.into is { } identifier)
        {
            // The rows become an ordinary variable, so a later step - an API call, a database check - can
            // use what the page showed without anything in between to carry it.
            Publish(context.Variables, identifier, result.Rows);
        }

        return result;
    }

    /// <inheritdoc />
    protected override async Task<(UiTableResultContext Result, string? Detail)> InspectAsync(
        ILocator? locator,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(locator);

        UiTableSnapshot snapshot = await DomTableReader.ReadAsync(locator, ProbeBudget.Unbounded, cancellationToken).ConfigureAwait(false);

        // Rows keyed by column name rather than by position, because an assertion that says
        // row["Price"] survives a column being inserted and row[3] does not.
        List<IReadOnlyDictionary<string, string>> rows = snapshot.Rows
            .Select(row => (IReadOnlyDictionary<string, string>)snapshot.Columns
                .Select((column, index) => (column, value: index < row.Count ? row[index] : string.Empty))
                .ToDictionary(pair => pair.column, pair => pair.value, StringComparer.OrdinalIgnoreCase))
            .ToList();

        UiTableResultContext result = new UiTableResultContext(
            this.App,
            locator.Page.Url,
            this.Target.Describe(),
            snapshot.Columns,
            rows);

        return (result, $"{rows.Count} row(s)");
    }

    /// <inheritdoc />
    protected override Exception? Verdict(UiTableResultContext result, TimeSpan waited) => null;
}
