using TestFramework.UI.Browser.Events;
using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Playwright;
using TestFramework.UI.Browser.Scripting;
using TestFramework.UI.Structure;

namespace TestFramework.UI.Browser.Structure;

/// <summary>
/// Reads a table off a page as columns and rows of text.
/// </summary>
/// <remarks>
/// A table is whatever presents itself as one, so this reads a real <c>&lt;table&gt;</c> and an ARIA grid
/// built from divs or components the same way. That is what lets an expectation about the data outlive a
/// rewrite of the widget showing it - the case that breaks every test written against
/// <c>tbody tr:nth-child(2) td:nth-child(3)</c>.
/// </remarks>
internal static class DomTableReader
{
    private const string ReadScript = """
        element => {
            const text = node => (node.innerText || node.textContent || '').trim();

            const headerCells = element.querySelectorAll('th, [role="columnheader"]');
            const columns = Array.from(headerCells).map(text);

            // A header row must not also be read as a data row, whichever way the table is built.
            const headerRow = headerCells.length > 0
                ? headerCells[0].closest('tr, [role="row"]')
                : null;

            const rowNodes = Array.from(element.querySelectorAll('tr, [role="row"]'))
                .filter(row => row !== headerRow);

            const rows = rowNodes
                .map(row => Array.from(row.querySelectorAll('td, [role="cell"], [role="gridcell"]')).map(text))
                .filter(cells => cells.length > 0);

            return { columns, rows };
        }
        """;

    /// <summary>
    /// Reads the table an element represents.
    /// </summary>
    /// <param name="locator">The table element.</param>
    /// <param name="budget">How long a browser call may take before the step needs the time back.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The table's columns and rows.</returns>
    public static async Task<UiTableSnapshot> ReadAsync(ILocator locator, ProbeBudget budget, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(locator);

        cancellationToken.ThrowIfCancellationRequested();

        JToken? read = await PageJson.EvaluateAsync(locator, ReadScript, argument: null, budget.Milliseconds).ConfigureAwait(false);

        if (read is not JObject table)
        {
            throw new PlaywrightException("The table could not be read from the page.");
        }

        return new UiTableSnapshot(Strings(table, "columns"), Rows(table));
    }

    private static List<string> Strings(JObject owner, string property)
    {
        List<string> values = new List<string>();

        if (owner[property] is JArray array)
        {
            foreach (JToken value in array)
            {
                values.Add(UiText.Normalize(value.Value<string>()) ?? string.Empty);
            }
        }

        return values;
    }

    private static List<IReadOnlyList<string>> Rows(JObject table)
    {
        List<IReadOnlyList<string>> rows = new List<IReadOnlyList<string>>();

        if (table["rows"] is not JArray array)
        {
            return rows;
        }

        foreach (JToken row in array)
        {
            List<string> cells = new List<string>();

            foreach (JToken cell in row)
            {
                cells.Add(UiText.Normalize(cell.Value<string>()) ?? string.Empty);
            }

            rows.Add(cells);
        }

        return rows;
    }
}
