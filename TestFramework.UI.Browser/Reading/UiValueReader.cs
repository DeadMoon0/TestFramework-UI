using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Playwright;
using TestFramework.UI.Browser.Resolution;
using TestFramework.UI.Structure;

namespace TestFramework.UI.Browser.Reading;

/// <summary>
/// What one read produced: the typed value, and how it reads in the trace.
/// </summary>
/// <param name="TypedValue">The value, in the source's own type.</param>
/// <param name="Detail">The value as a trace line.</param>
/// <param name="Resolved">Which lookup found the element, when the source reads from one.</param>
internal sealed record UiReadResult(object TypedValue, string Detail, UiResolvedTarget? Resolved);

/// <summary>
/// Answers a <see cref="UiValueSource"/> against a live page.
/// </summary>
/// <remarks>
/// The reader's contract is the same as the resolver's: nothing is guessed, and every failure names what
/// the page does offer instead. A missing attribute lists the attributes that are there; a missing query
/// parameter lists the parameters the address carries; a missing storage key lists the keys the page
/// holds. The fix should be a copy, not an expedition.
/// </remarks>
internal static class UiValueReader
{
    /// <summary>How many existing names a failure lists at most.</summary>
    private const int MaxNamed = 15;

    /// <summary>What the trace shows in place of a value that must not be echoed.</summary>
    private const string SensitiveDetail = "•••";

    /// <summary>
    /// Reads one value.
    /// </summary>
    /// <param name="source">What to read.</param>
    /// <param name="page">The page to read it from.</param>
    /// <param name="locator">The resolved element, for sources that read from one; null otherwise.</param>
    /// <param name="resolved">How the element was found, for the trace; null for page-level sources.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The typed value and its trace line.</returns>
    public static async Task<UiReadResult> ReadAsync(
        UiValueSource source,
        IPage page,
        ILocator? locator,
        UiResolvedTarget? resolved,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(page);

        cancellationToken.ThrowIfCancellationRequested();

        return source.Kind switch
        {
            UiValueKind.Text => Result(
                UiText.Normalize(await Element(source, locator).InnerTextAsync().ConfigureAwait(false)) ?? string.Empty,
                resolved),
            UiValueKind.FieldValue => await FieldValueAsync(source, Element(source, locator), resolved).ConfigureAwait(false),
            UiValueKind.Checked => Result(await Element(source, locator).IsCheckedAsync().ConfigureAwait(false), resolved),
            UiValueKind.Attribute => await AttributeAsync(source, Element(source, locator), resolved).ConfigureAwait(false),
            UiValueKind.SelectedOption => await SelectedOptionAsync(source, Element(source, locator), resolved).ConfigureAwait(false),
            UiValueKind.Style => await StyleAsync(source, Element(source, locator), resolved).ConfigureAwait(false),
            UiValueKind.Url => Result(page.Url, resolved: null),
            UiValueKind.QueryParam => QueryParam(source, page),
            UiValueKind.LocalStorage => await LocalStorageAsync(source, page).ConfigureAwait(false),
            UiValueKind.Cookie => await CookieAsync(source, page).ConfigureAwait(false),

            // Count never reaches this reader: it needs the resolver's ladder rather than one element,
            // so the flow answers it there.
            _ => throw new InvalidOperationException($"'{source.Kind}' is not a value this reader answers."),
        };
    }

    private static ILocator Element(UiValueSource source, ILocator? locator)
        => locator ?? throw new InvalidOperationException($"Reading {source.Describe()} needs an element.");

    private static UiReadResult Result(object value, UiResolvedTarget? resolved)
        => new UiReadResult(value, Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty, resolved);

    private static async Task<UiReadResult> FieldValueAsync(UiValueSource source, ILocator locator, UiResolvedTarget? resolved)
    {
        try
        {
            return Result(await locator.InputValueAsync().ConfigureAwait(false), resolved);
        }
        catch (PlaywrightException exception)
        {
            // Playwright's input_value is defined for the native form controls and nothing else. The
            // element was found, so the problem is the question, and the message should redirect it.
            throw new InvalidOperationException(
                $"The {source.Target!.Describe()} was found, but it is not a native form control, so it has " +
                $"no value to read. Read Value.Text(...) for what it shows, or Value.Attribute(...) for what " +
                $"it carries.",
                exception);
        }
    }

    private static async Task<UiReadResult> AttributeAsync(UiValueSource source, ILocator locator, UiResolvedTarget? resolved)
    {
        string? value = await locator.GetAttributeAsync(source.Argument!).ConfigureAwait(false);

        if (value is not null)
        {
            return Result(value, resolved);
        }

        // Which attributes the element does carry is the fastest way to spot a typo - the same reason a
        // not-found target lists the names the page offers.
        IReadOnlyList<string> present = await locator
            .EvaluateAsync<string[]>("el => Array.from(el.attributes, a => a.name)")
            .ConfigureAwait(false) ?? [];

        throw new InvalidOperationException(
            $"The {source.Target!.Describe()} carries no attribute '{source.Argument}'. " +
            Offer("It carries", present.Take(MaxNamed).ToList()));
    }

    private static async Task<UiReadResult> SelectedOptionAsync(UiValueSource source, ILocator locator, UiResolvedTarget? resolved)
    {
        string tag = await locator.EvaluateAsync<string>("el => el.tagName.toLowerCase()").ConfigureAwait(false);

        if (tag == "select")
        {
            string? label = await locator
                .EvaluateAsync<string?>("el => el.selectedOptions.length > 0 ? el.selectedOptions[0].label : null")
                .ConfigureAwait(false);

            if (label is not null)
            {
                return Result(UiText.Normalize(label) ?? string.Empty, resolved);
            }

            IReadOnlyList<string> options = await locator
                .EvaluateAsync<string[]>("el => Array.from(el.options, o => o.label)")
                .ConfigureAwait(false) ?? [];

            throw new InvalidOperationException(
                $"The {source.Target!.Describe()} has nothing selected. " +
                Offer("Its options are", options.Take(MaxNamed).ToList()));
        }

        if (await IsComboboxAsync(locator).ConfigureAwait(false))
        {
            // Per the ARIA pattern, a closed combobox displays its selected value as its text.
            return Result(
                UiText.Normalize(await locator.InnerTextAsync().ConfigureAwait(false)) ?? string.Empty,
                resolved);
        }

        throw new InvalidOperationException(
            $"The {source.Target!.Describe()} is a <{tag}> that is neither a native list nor declares the " +
            "combobox contract, so it has no selected option to read.");
    }

    /// <summary>
    /// Whether an element declares the ARIA combobox contract.
    /// </summary>
    /// <remarks>
    /// The contract, not any library's markup: an explicit <c>role="combobox"</c>, or a popup announced
    /// as a listbox. That is what makes this work for Angular Material today and whatever replaces it
    /// later.
    /// </remarks>
    /// <param name="locator">The element.</param>
    /// <returns>True when the element is a combobox.</returns>
    internal static async Task<bool> IsComboboxAsync(ILocator locator)
    {
        string? role = await locator.GetAttributeAsync("role").ConfigureAwait(false);

        if (string.Equals(role, "combobox", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        string? popup = await locator.GetAttributeAsync("aria-haspopup").ConfigureAwait(false);

        return string.Equals(popup, "listbox", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<UiReadResult> StyleAsync(UiValueSource source, ILocator locator, UiResolvedTarget? resolved)
    {
        string value = (await locator
            .EvaluateAsync<string>("(el, prop) => getComputedStyle(el).getPropertyValue(prop)", source.Argument)
            .ConfigureAwait(false) ?? string.Empty).Trim();

        if (value.Length == 0)
        {
            // Computed style answers "" for a property that does not exist, which is a typo nine times
            // out of ten - and a typo deserves a message, not an empty variable.
            throw new InvalidOperationException(
                $"The computed style of {source.Target!.Describe()} has no value for '{source.Argument}'. " +
                "Property names are CSS names, for example 'background-color'.");
        }

        return Result(value, resolved);
    }

    private static UiReadResult QueryParam(UiValueSource source, IPage page)
    {
        Uri url = new Uri(page.Url);
        List<(string Name, string Value)> parameters = Parse(url.Query);

        foreach ((string name, string value) in parameters)
        {
            if (string.Equals(name, source.Argument, StringComparison.Ordinal))
            {
                return Result(value, resolved: null);
            }
        }

        throw new InvalidOperationException(
            $"The address {url} carries no query parameter '{source.Argument}'. " +
            Offer("It carries", parameters.Select(static parameter => parameter.Name).Take(MaxNamed).ToList()));
    }

    private static async Task<UiReadResult> LocalStorageAsync(UiValueSource source, IPage page)
    {
        string? value = await page
            .EvaluateAsync<string?>("key => window.localStorage.getItem(key)", source.Argument)
            .ConfigureAwait(false);

        if (value is not null)
        {
            // The variable is the one channel the value travels; the trace shows a placeholder.
            // Local storage is where tokens live, and the trace detail reaches the run log, the
            // session picture, failure messages and the evidence bundle.
            return new UiReadResult(value, SensitiveDetail, Resolved: null);
        }

        IReadOnlyList<string> keys = await page
            .EvaluateAsync<string[]>("() => Object.keys(window.localStorage)")
            .ConfigureAwait(false) ?? [];

        throw new InvalidOperationException(
            $"The page holds no local storage entry '{source.Argument}'. " +
            Offer("It holds", keys.Take(MaxNamed).ToList()));
    }

    private static async Task<UiReadResult> CookieAsync(UiValueSource source, IPage page)
    {
        // The browser's own jar, asked for the current address - so what is read is what the page's
        // requests actually carry, HttpOnly cookies included, rather than the subset document.cookie
        // shows scripts.
        bool scoped = Uri.TryCreate(page.Url, UriKind.Absolute, out Uri? address)
            && (address.Scheme == Uri.UriSchemeHttp || address.Scheme == Uri.UriSchemeHttps);

        IReadOnlyList<BrowserContextCookiesResult> cookies = scoped
            ? await page.Context.CookiesAsync([page.Url]).ConfigureAwait(false)
            : await page.Context.CookiesAsync().ConfigureAwait(false);

        foreach (BrowserContextCookiesResult cookie in cookies)
        {
            if (string.Equals(cookie.Name, source.Argument, StringComparison.Ordinal))
            {
                // The variable is the one channel the value travels; the trace shows a placeholder.
                // This source's own doc names an HttpOnly cookie - a session token, typically - as
                // the primary use, so echoing the value into the trace printed the token into logs
                // and evidence by construction.
                return new UiReadResult(cookie.Value, SensitiveDetail, Resolved: null);
            }
        }

        throw new InvalidOperationException(
            $"The browser holds no cookie '{source.Argument}' for {page.Url}. " +
            Offer("It holds", cookies.Select(static cookie => cookie.Name).Take(MaxNamed).ToList()));
    }

    private static string Offer(string lead, IReadOnlyList<string> names)
        => names.Count == 0
            ? lead switch
            {
                "It carries" => "It carries none.",
                "Its options are" => "It has no options.",
                _ => "It holds none.",
            }
            : $"{lead}: {string.Join(", ", names.Select(static name => $"'{name}'"))}.";

    private static List<(string Name, string Value)> Parse(string query)
    {
        List<(string, string)> parameters = new List<(string, string)>();

        foreach (string pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            int split = pair.IndexOf('=', StringComparison.Ordinal);

            parameters.Add(split < 0
                ? (Uri.UnescapeDataString(pair), string.Empty)
                : (Uri.UnescapeDataString(pair[..split]), Uri.UnescapeDataString(pair[(split + 1)..])));
        }

        return parameters;
    }
}
