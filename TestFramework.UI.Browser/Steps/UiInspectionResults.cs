using System.Collections.Generic;
using TestFramework.Core.Steps;
using TestFramework.UI.Structure;

namespace TestFramework.UI.Browser.Steps;

/// <summary>
/// What a structure or table comparison found.
/// </summary>
public sealed record UiCompareResultContext : StepResultContext
{
    internal UiCompareResultContext(string app, string url, string scope, bool isMatch, IReadOnlyList<string> differences, string actual, string? suggestedCode)
    {
        this.App = app;
        this.Url = url;
        this.Scope = scope;
        this.IsMatch = isMatch;
        this.Differences = differences;
        this.Actual = actual;
        this.SuggestedCode = suggestedCode;
    }

    /// <summary>
    /// The application whose page was compared.
    /// </summary>
    public string App { get; }

    /// <summary>
    /// The address the page was on.
    /// </summary>
    public string Url { get; }

    /// <summary>
    /// What was compared, as the test described it.
    /// </summary>
    public string Scope { get; }

    /// <summary>
    /// True when the page matched the expectation.
    /// </summary>
    public bool IsMatch { get; }

    /// <summary>
    /// Every way it differed, as readable lines.
    /// </summary>
    public IReadOnlyList<string> Differences { get; }

    /// <summary>
    /// What the page actually was, rendered - so a reader can see it without the page.
    /// </summary>
    public string Actual { get; }

    /// <summary>
    /// The expectation that would have passed, as C# to read and paste.
    /// </summary>
    public string? SuggestedCode { get; }

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
public sealed record UiTableResultContext : StepResultContext
{
    internal UiTableResultContext(string app, string url, string table, IReadOnlyList<string> columns, IReadOnlyList<IReadOnlyDictionary<string, string>> rows)
    {
        this.App = app;
        this.Url = url;
        this.Table = table;
        this.Columns = columns;
        this.Rows = rows;
    }

    /// <summary>
    /// The application.
    /// </summary>
    public string App { get; }

    /// <summary>
    /// The address the page was on.
    /// </summary>
    public string Url { get; }

    /// <summary>
    /// What the test called the table.
    /// </summary>
    public string Table { get; }

    /// <summary>
    /// The header texts.
    /// </summary>
    public IReadOnlyList<string> Columns { get; }

    /// <summary>
    /// The rows, each keyed by column name so an assertion reads as data rather than as indexes.
    /// </summary>
    public IReadOnlyList<IReadOnlyDictionary<string, string>> Rows { get; }

    /// <summary>
    /// Returns a readable summary.
    /// </summary>
    /// <returns>The summary.</returns>
    public override string ToString() => $"{this.Rows.Count} row(s) of {this.Columns.Count} column(s) from {this.Table}";
}

/// <summary>
/// A captured structure, kept for comparison against later runs.
/// </summary>
public sealed record UiCaptureResultContext : StepResultContext
{
    internal UiCaptureResultContext(string app, string url, string name, string structure, int elementCount)
    {
        this.App = app;
        this.Url = url;
        this.Name = name;
        this.Structure = structure;
        this.ElementCount = elementCount;
    }

    /// <summary>
    /// The application.
    /// </summary>
    public string App { get; }

    /// <summary>
    /// The address the page was on.
    /// </summary>
    public string Url { get; }

    /// <summary>
    /// What the capture was called.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// The rendered structure, as it was stored in the variable.
    /// </summary>
    public string Structure { get; }

    /// <summary>
    /// How many elements it covers.
    /// </summary>
    public int ElementCount { get; }

    /// <summary>
    /// Returns a readable summary.
    /// </summary>
    /// <returns>The summary.</returns>
    public override string ToString() => $"captured '{this.Name}': {this.ElementCount} element(s) on '{this.App}'";
}
