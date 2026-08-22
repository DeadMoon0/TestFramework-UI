using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace TestFramework.UI.Browser.Resolution;

/// <summary>
/// The questions the resolver may ask a page while looking for a target.
/// </summary>
/// <remarks>
/// Deliberately small. The resolver decides which channel wins, what counts as ambiguous, and what a
/// failure should tell the reader; the page only answers how many elements a lookup finds and what
/// they look like. Keeping it to three questions is what allows the matching ladder - the part most
/// likely to be wrong - to be tested exhaustively without starting a browser.
/// </remarks>
internal interface IUiElementQuery
{
    /// <summary>
    /// How many elements the lookup finds right now.
    /// </summary>
    /// <param name="spec">The lookup.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>The number of matching elements. Zero when none match; never throws for a lookup that
    /// simply finds nothing.</returns>
    Task<int> CountAsync(UiQuerySpec spec, CancellationToken cancellationToken);

    /// <summary>
    /// Describes the elements a lookup finds, for a failure message or a trace entry.
    /// </summary>
    /// <param name="spec">The lookup.</param>
    /// <param name="max">How many to describe at most.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>The candidates, in document order.</returns>
    Task<IReadOnlyList<UiCandidate>> DescribeAsync(UiQuerySpec spec, int max, CancellationToken cancellationToken);

    /// <summary>
    /// Lists the names the page does offer for a kind of element, so a failure can suggest what the
    /// test probably meant.
    /// </summary>
    /// <param name="spec">A lookup whose channel and role say what kind of names to collect. Its text
    /// is ignored.</param>
    /// <param name="max">How many names to collect at most.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>The available names, in document order.</returns>
    Task<IReadOnlyList<string>> AvailableNamesAsync(UiQuerySpec spec, int max, CancellationToken cancellationToken);
}
