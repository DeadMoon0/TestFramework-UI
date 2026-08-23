using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace TestFramework.UI.Structure;

/// <summary>
/// The table a test expects to find, written as the table itself.
/// </summary>
/// <remarks>
/// <para>
/// Rows are matched as a <em>set</em>, keyed on the first column given unless another is named. Sorting a
/// table differently is not a defect in the data, and a test about the data should not fail for it - while
/// a test that does care about the order says <c>InOrder()</c>.
/// </para>
/// <para>
/// A row the page does not have is <em>missing</em>; a row the page has that the expectation did not is
/// <em>surplus</em>, unless the test allowed extras. Naming only some columns is normal: the ones nobody
/// mentioned are not compared, so a table gaining a column does not break every test that reads it.
/// </para>
/// </remarks>
public sealed class ExpectedTable
{
    private readonly List<string> columns;
    private readonly List<IReadOnlyList<CellRule>> rows = new List<IReadOnlyList<CellRule>>();
    private string? keyColumn;
    private bool allowExtraRows;
    private bool inOrder;

    private ExpectedTable(IEnumerable<string> columns)
    {
        this.columns = columns.ToList();

        if (this.columns.Count == 0)
        {
            throw new ArgumentException("An expected table needs at least one column.", nameof(columns));
        }
    }

    /// <summary>
    /// Starts an expected table by naming the columns it is about.
    /// </summary>
    /// <param name="columns">The header texts of the columns this test cares about.</param>
    /// <returns>The table, to add rows to.</returns>
    public static ExpectedTable WithHeader(params string[] columns)
    {
        ArgumentNullException.ThrowIfNull(columns);

        return new ExpectedTable(columns);
    }

    /// <summary>
    /// Adds an expected row.
    /// </summary>
    /// <param name="cells">One value or rule per named column. A plain string means the text, normalized.</param>
    /// <returns>The same table, for chaining.</returns>
    public ExpectedTable Row(params CellRule[] cells)
    {
        ArgumentNullException.ThrowIfNull(cells);

        if (cells.Length != this.columns.Count)
        {
            throw new ArgumentException(
                $"The row has {cells.Length} cell(s) but the table names {this.columns.Count} column(s): " +
                $"{string.Join(", ", this.columns)}. Use Cell.Any for a column this row does not care about.",
                nameof(cells));
        }

        this.rows.Add(cells);

        return this;
    }

    /// <summary>
    /// Matches rows on this column instead of the first one.
    /// </summary>
    /// <remarks>
    /// The key decides what a failure reads like. Keyed on something unique - an order number, an id - a
    /// difference points at the one cell that is wrong. Keyed on something repeated, the best it can say is
    /// that a whole row is missing.
    /// </remarks>
    /// <param name="column">The column to key on.</param>
    /// <returns>The same table, for chaining.</returns>
    public ExpectedTable KeyedBy(string column)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(column);

        if (!this.columns.Contains(column, StringComparer.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                $"'{column}' is not one of this table's columns: {string.Join(", ", this.columns)}.",
                nameof(column));
        }

        this.keyColumn = column;

        return this;
    }

    /// <summary>
    /// Allows the page to have rows this table did not mention.
    /// </summary>
    /// <returns>The same table, for chaining.</returns>
    public ExpectedTable AllowExtraRows()
    {
        this.allowExtraRows = true;

        return this;
    }

    /// <summary>
    /// Requires the rows to appear in the order given.
    /// </summary>
    /// <returns>The same table, for chaining.</returns>
    public ExpectedTable InOrder()
    {
        this.inOrder = true;

        return this;
    }

    /// <summary>
    /// Compares this expectation against what the page had.
    /// </summary>
    /// <param name="actual">The table the page had.</param>
    /// <returns>Every difference found, empty when the table matched.</returns>
    public IReadOnlyList<UiDifference> Compare(UiTableSnapshot actual)
    {
        ArgumentNullException.ThrowIfNull(actual);

        List<UiDifference> differences = new List<UiDifference>();

        foreach (string column in this.columns.Where(column => actual.IndexOf(column) < 0))
        {
            differences.Add(new UiDifference(
                UiDifferenceKind.Missing,
                $"column '{column}'",
                $"missing — the table has {string.Join(", ", actual.Columns.Select(static header => $"'{header}'"))}."));
        }

        if (differences.Count > 0)
        {
            // Comparing rows against columns that are not there would turn one real difference into one per
            // row, and bury it.
            return differences;
        }

        HashSet<int> claimed = new HashSet<int>();
        int lastClaimed = -1;

        for (int rowIndex = 0; rowIndex < this.rows.Count; rowIndex++)
        {
            IReadOnlyList<CellRule> expectedRow = this.rows[rowIndex];
            string rowPath = $"row {rowIndex + 1}";

            int match = this.FindByKey(expectedRow, actual, claimed);

            if (match < 0)
            {
                differences.Add(new UiDifference(
                    UiDifferenceKind.Missing,
                    rowPath,
                    $"missing — no row where {this.KeyDescription(expectedRow)}."));

                continue;
            }

            claimed.Add(match);

            if (this.inOrder && match < lastClaimed)
            {
                differences.Add(new UiDifference(
                    UiDifferenceKind.OutOfOrder,
                    rowPath,
                    $"the table was expected in order, and this row comes before the previous one on the page."));
            }

            lastClaimed = Math.Max(lastClaimed, match);

            // The key matched, so anything else that differs is a cell rather than a row - which is the
            // difference between "a row is missing" and "the price is wrong".
            this.CompareCells(expectedRow, actual, match, rowPath, differences);
        }

        if (!this.allowExtraRows)
        {
            foreach (int index in Enumerable.Range(0, actual.Rows.Count).Where(index => !claimed.Contains(index)))
            {
                differences.Add(new UiDifference(
                    UiDifferenceKind.Surplus,
                    $"page row {index + 1}",
                    $"surplus — the page has | {string.Join(" | ", actual.Rows[index])} | and the table did not expect it."));
            }
        }

        return differences;
    }

    /// <summary>
    /// The columns this table is about.
    /// </summary>
    public IReadOnlyList<string> Columns => this.columns;

    /// <summary>
    /// How many rows this table expects.
    /// </summary>
    public int RowCount => this.rows.Count;

    private void CompareCells(
        IReadOnlyList<CellRule> expectedRow,
        UiTableSnapshot actual,
        int actualRowIndex,
        string rowPath,
        List<UiDifference> differences)
    {
        for (int columnIndex = 0; columnIndex < this.columns.Count; columnIndex++)
        {
            string column = this.columns[columnIndex];
            string? value = actual.Cell(actual.Rows[actualRowIndex], column);

            if (!expectedRow[columnIndex].Matches(value))
            {
                differences.Add(new UiDifference(
                    UiDifferenceKind.RuleFailed,
                    $"{rowPath}, column '{column}'",
                    $"expected {expectedRow[columnIndex].Description}, found '{value}'"));
            }
        }
    }

    private int FindByKey(IReadOnlyList<CellRule> expectedRow, UiTableSnapshot actual, HashSet<int> claimed)
    {
        int keyIndex = this.KeyIndex();
        CellRule key = expectedRow[keyIndex];
        string keyColumnName = this.columns[keyIndex];

        for (int index = 0; index < actual.Rows.Count; index++)
        {
            if (!claimed.Contains(index) && key.Matches(actual.Cell(actual.Rows[index], keyColumnName)))
            {
                return index;
            }
        }

        return -1;
    }

    private int KeyIndex()
        => this.keyColumn is null
            ? 0
            : this.columns.FindIndex(column => string.Equals(column, this.keyColumn, StringComparison.OrdinalIgnoreCase));

    private string KeyDescription(IReadOnlyList<CellRule> expectedRow)
    {
        int keyIndex = this.KeyIndex();

        return string.Format(
            CultureInfo.InvariantCulture,
            "'{0}' {1}",
            this.columns[keyIndex],
            expectedRow[keyIndex].Description);
    }
}
