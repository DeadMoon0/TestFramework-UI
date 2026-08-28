using System;
using System.Collections.Generic;
using System.Linq;

namespace TestFramework.UI.Structure;

/// <summary>
/// What a table on a page actually held.
/// </summary>
/// <remarks>
/// Columns by their header text and rows as plain strings, because that is what a table means to the
/// person reading it. Nothing here knows how the table was built - a real <c>&lt;table&gt;</c>, a grid of
/// divs, or a list of components - which is what lets one expectation outlive a rewrite of the widget.
/// </remarks>
/// <param name="Columns">The header texts, in the order the page had them.</param>
/// <param name="Rows">The rows, each as one value per column.</param>
public sealed record UiTableSnapshot(
    IReadOnlyList<string> Columns,
    IReadOnlyList<IReadOnlyList<string>> Rows)
{
    /// <summary>
    /// The value of one cell, by column name.
    /// </summary>
    /// <param name="row">The row.</param>
    /// <param name="column">The column's header text, matched normalized and without regard to case.</param>
    /// <returns>The value, or null when the table has no such column.</returns>
    public string? Cell(IReadOnlyList<string> row, string column)
    {
        int index = this.IndexOf(column);

        return index >= 0 && index < row.Count ? row[index] : null;
    }

    /// <summary>
    /// Where a column sits, or -1 when the table does not have it.
    /// </summary>
    /// <param name="column">The column's header text.</param>
    /// <returns>The index, or -1.</returns>
    public int IndexOf(string column)
    {
        string? wanted = UiText.Normalize(column);

        for (int index = 0; index < this.Columns.Count; index++)
        {
            if (string.Equals(UiText.Normalize(this.Columns[index]), wanted, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return -1;
    }

    /// <summary>
    /// Renders the table as text, for a failure message or a captured value.
    /// </summary>
    /// <returns>The rendered table.</returns>
    public override string ToString()
    {
        IEnumerable<string> lines = this.Rows.Select(static row => "  | " + string.Join(" | ", row) + " |");

        return string.Join(
            Environment.NewLine,
            new[] { "  | " + string.Join(" | ", this.Columns) + " |" }.Concat(lines));
    }
}
