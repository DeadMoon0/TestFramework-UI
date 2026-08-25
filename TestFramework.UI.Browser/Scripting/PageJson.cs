using System;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.Playwright;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TestFramework.Core.Json;

namespace TestFramework.UI.Browser.Scripting;

/// <summary>
/// Asks the page a question and reads the answer as JSON text.
/// </summary>
/// <remarks>
/// <para>
/// Everything this package learns from a page - a projected structure, a table, an element's description,
/// a test's own script - comes back through here, and it comes back as text the page serialised itself.
/// Letting the browser driver deserialise instead looked simpler and cost three things.
/// </para>
/// <para>
/// It was a second JSON library. The driver deserialises with <c>System.Text.Json</c>, so a test's own
/// record was bound by a different set of attributes and a different idea of null than every other package
/// in this family uses - the exact seam the one-library rule exists to close.
/// </para>
/// <para>
/// It was the driver's transport showing through. Its protocol stamps a <c>$id</c> into every object it
/// serialises so it can express back-references, and those arrived looking like attributes the page had.
/// The structure projector carried two filters for them, one of which dropped anything without a tag - and
/// a number that changes on every render is precisely what makes captured structure worthless to compare.
/// Text has no back-references, so both filters went.
/// </para>
/// <para>
/// And it was a promise quietly swallowed: this wraps the caller's function and awaits it, because an
/// asynchronous script serialised without awaiting would hand back an empty object.
/// </para>
/// </remarks>
internal static class PageJson
{
    /// <summary>
    /// Runs a script on the page and parses what it returned.
    /// </summary>
    /// <param name="page">The page.</param>
    /// <param name="source">The function, called with the argument object when one is given.</param>
    /// <param name="argument">The argument object, or null to call it without one.</param>
    /// <returns>The parsed answer, or null when the script returned nothing.</returns>
    public static async Task<JToken?> EvaluateAsync(IPage page, string source, object? argument = null)
    {
        ArgumentNullException.ThrowIfNull(page);

        return Read(await page.EvaluateAsync<string?>(Wrap(source, "args", "args"), argument).ConfigureAwait(false));
    }

    /// <summary>
    /// Runs a script on one element and parses what it returned.
    /// </summary>
    /// <param name="element">The element, which the function receives first.</param>
    /// <param name="source">The function, called as <c>(el, args)</c>.</param>
    /// <param name="argument">The argument object, or null to call it without one.</param>
    /// <returns>The parsed answer, or null when the script returned nothing.</returns>
    public static async Task<JToken?> EvaluateAsync(ILocator element, string source, object? argument = null)
    {
        ArgumentNullException.ThrowIfNull(element);

        return Read(await element.EvaluateAsync<string?>(Wrap(source, "el, args", "el, args"), argument).ConfigureAwait(false));
    }

    /// <summary>
    /// Renders a parsed answer back to compact JSON, for a message that quotes what came back.
    /// </summary>
    /// <param name="token">The answer.</param>
    /// <returns>The JSON text.</returns>
    public static string Describe(JToken token)
    {
        ArgumentNullException.ThrowIfNull(token);

        return token.ToString(Formatting.None);
    }

    /// <summary>
    /// Wraps a caller's function so the page serialises the result instead of the driver.
    /// </summary>
    /// <remarks>
    /// A non-function expression is accepted as well, because the driver accepts one and a caller who
    /// passes <c>document.title</c> should not be told off by a wrapper rather than by the page. The
    /// <c>await</c> is what makes an asynchronous script work: awaiting a value that is not a promise is
    /// free, and not awaiting one that is hands back an empty object.
    /// </remarks>
    /// <param name="source">The caller's function or expression.</param>
    /// <param name="parameters">The wrapper's parameter list.</param>
    /// <param name="arguments">What the caller's function is called with.</param>
    /// <returns>The wrapped source.</returns>
    private static string Wrap(string source, string parameters, string arguments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);

        return string.Create(
            CultureInfo.InvariantCulture,
            $"async ({parameters}) => {{ const answer = ({source}); return JSON.stringify(typeof answer === 'function' ? await answer({arguments}) : await answer); }}");
    }

    /// <summary>
    /// Parses what the page sent, exactly as it sent it.
    /// </summary>
    /// <remarks>
    /// Null text and the text <c>null</c> are different answers and stay different: a script with no
    /// return statement produced nothing, while one that returned null answered the question. Parsed
    /// through the engine's reader, so a string that happens to look like a date is still the string the
    /// page sent.
    /// </remarks>
    private static JToken? Read(string? json) => json is null ? null : WireJson.Parse(json);
}
