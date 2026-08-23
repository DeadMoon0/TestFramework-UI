using System.Collections.Generic;
using TestFramework.Core.Steps;
using TestFramework.UI.Structure;

namespace TestFramework.UI.Browser.Steps;

/// <summary>
/// What a structure or table comparison found.
/// </summary>
/// <param name="App">The application whose page was compared.</param>
/// <param name="Url">The address the page was on.</param>
/// <param name="Scope">What was compared, as the test described it.</param>
/// <param name="IsMatch">True when the page matched the expectation.</param>
/// <param name="Differences">Every way it differed, as readable lines.</param>
/// <param name="Actual">What the page actually was, rendered - so a reader can see it without the page.</param>
/// <param name="SuggestedCode">The expectation that would have passed, as C# to read and paste.</param>
public sealed record UiCompareResultContext(
    string App,
    string Url,
    string Scope,
    bool IsMatch,
    IReadOnlyList<string> Differences,
    string Actual,
    string? SuggestedCode) : StepResultContext
{
    /// <summary>
    /// Returns a readable summary.
    /// </summary>
    /// <returns>The summary.</returns>
    public override string ToString()
        => this.IsMatch
            ? $"{this.Scope} on '{this.App}' matched"
            : $"{this.Scope} on '{this.App}' differed in {this.Differences.Count} way(s)";
}

/// <summary>
/// A table as it was read off the page.
/// </summary>
/// <param name="App">The application.</param>
/// <param name="Url">The address the page was on.</param>
/// <param name="Table">What the test called the table.</param>
/// <param name="Columns">The header texts.</param>
/// <param name="Rows">The rows, each keyed by column name so an assertion reads as data rather than as
/// indexes.</param>
public sealed record UiTableResultContext(
    string App,
    string Url,
    string Table,
    IReadOnlyList<string> Columns,
    IReadOnlyList<IReadOnlyDictionary<string, string>> Rows) : StepResultContext
{
    /// <summary>
    /// Returns a readable summary.
    /// </summary>
    /// <returns>The summary.</returns>
    public override string ToString() => $"{this.Rows.Count} row(s) of {this.Columns.Count} column(s) from {this.Table}";
}

/// <summary>
/// A captured structure, kept for comparison against later runs.
/// </summary>
/// <param name="App">The application.</param>
/// <param name="Url">The address the page was on.</param>
/// <param name="Name">What the capture was called.</param>
/// <param name="Structure">The rendered structure, as it was stored in the variable.</param>
/// <param name="ElementCount">How many elements it covers.</param>
public sealed record UiCaptureResultContext(
    string App,
    string Url,
    string Name,
    string Structure,
    int ElementCount) : StepResultContext
{
    /// <summary>
    /// Returns a readable summary.
    /// </summary>
    /// <returns>The summary.</returns>
    public override string ToString() => $"captured '{this.Name}': {this.ElementCount} element(s) on '{this.App}'";
}
