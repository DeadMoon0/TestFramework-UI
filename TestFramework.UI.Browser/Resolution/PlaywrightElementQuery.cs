using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Playwright;
using TestFramework.UI.Browser.Targeting;

namespace TestFramework.UI.Browser.Resolution;

/// <summary>
/// Answers the resolver's questions by building Playwright locators.
/// </summary>
/// <remarks>
/// <para>
/// This class composes Playwright locators and never re-implements matching. Whitespace handling,
/// case-insensitive substrings, what counts as an accessible name, which elements are in the
/// accessibility tree at all - Playwright decides all of it, and years of browser edge cases went into
/// those decisions. What this package adds is the order the channels are tried in and what to do when a
/// page answers with none or several, which is exactly what the resolver does and Playwright does not.
/// </para>
/// <para>
/// One thing is deliberately built by hand rather than delegated: the test id channel. Playwright's own
/// test id support is a process-wide setting, and two applications in the same run may configure
/// different attributes, so the attribute selector is composed here from each application's own
/// configuration.
/// </para>
/// </remarks>
internal sealed class PlaywrightElementQuery : IUiElementQuery
{
    private const int SnippetLength = 200;

    private readonly IPage page;
    private readonly string testIdAttribute;
    private readonly float probeTimeoutMs;

    public PlaywrightElementQuery(IPage page, string testIdAttribute, TimeSpan probeTimeout)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentException.ThrowIfNullOrWhiteSpace(testIdAttribute);

        this.page = page;
        this.testIdAttribute = testIdAttribute;
        this.probeTimeoutMs = (float)probeTimeout.TotalMilliseconds;
    }

    /// <inheritdoc />
    public async Task<int> CountAsync(UiQuerySpec spec, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(spec);

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            return await this.Build(spec).CountAsync().ConfigureAwait(false);
        }
        catch (PlaywrightException)
        {
            // A channel that cannot even be expressed against this page - an unknown role, a selector the
            // page's engine rejects - is a channel that found nothing, not a failed run. The resolver
            // reports what it tried, so nothing is hidden by returning zero here.
            return 0;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<UiCandidate>> DescribeAsync(UiQuerySpec spec, int max, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(spec);

        ILocator locator = this.Build(spec);
        List<UiCandidate> candidates = new List<UiCandidate>();

        int count;

        try
        {
            count = await locator.CountAsync().ConfigureAwait(false);
        }
        catch (PlaywrightException)
        {
            return candidates;
        }

        for (int index = 0; index < Math.Min(count, max); index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            candidates.Add(await this.DescribeOneAsync(locator.Nth(index), index).ConfigureAwait(false));
        }

        return candidates;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> AvailableNamesAsync(UiQuerySpec spec, int max, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(spec);

        if (max <= 0)
        {
            return Array.Empty<string>();
        }

        // Only a role can be enumerated without a name: "every button" is a real question, while
        // "every label" is the label lookup's own text with nothing filled in - building GetByLabel(null)
        // is how this method once crashed the not-found message it exists to improve. The role channels
        // still carry every suggestion worth making: an element found by its label or placeholder shows
        // that same text as its accessible name here.
        if (spec.Channel is not UiMatchChannel.Role)
        {
            return Array.Empty<string>();
        }

        try
        {
            ILocator locator = this.Build(spec with { Text = null, Exact = false });
            IReadOnlyList<ILocator> all = await locator.AllAsync().ConfigureAwait(false);
            List<string> names = new List<string>();

            foreach (ILocator element in all.Take(max))
            {
                cancellationToken.ThrowIfCancellationRequested();

                string? name = await this.AccessibleNameAsync(element).ConfigureAwait(false);

                if (!string.IsNullOrWhiteSpace(name) && !names.Contains(name, StringComparer.Ordinal))
                {
                    names.Add(name);
                }
            }

            return names;
        }
        catch (PlaywrightException)
        {
            return Array.Empty<string>();
        }
    }

    /// <summary>
    /// The locator a resolved target points at, for the step that is about to act on it.
    /// </summary>
    /// <param name="resolved">The resolution outcome.</param>
    /// <returns>The locator, narrowed to the one element to act on.</returns>
    public ILocator Locate(UiResolvedTarget resolved)
    {
        ArgumentNullException.ThrowIfNull(resolved);

        return this.Build(resolved.Spec).Nth(resolved.Index);
    }

    private ILocator Build(UiQuerySpec spec)
    {
        if (spec.Within is { } within)
        {
            // Scoping is Playwright's own chaining, so the inner search is genuinely restricted to the
            // container's subtree rather than filtered afterwards.
            ILocator scope = this.Build(within).Nth(spec.WithinIndex);

            return this.Apply(spec, scope);
        }

        return this.Apply(spec, scope: null);
    }

    private ILocator Apply(UiQuerySpec spec, ILocator? scope)
    {
        ILocator located = spec.Channel switch
        {
            UiMatchChannel.Role => this.ByRole(spec, scope),
            UiMatchChannel.Label => scope is null
                ? this.page.GetByLabel(spec.Text!, new PageGetByLabelOptions { Exact = spec.Exact })
                : scope.GetByLabel(spec.Text!, new LocatorGetByLabelOptions { Exact = spec.Exact }),
            UiMatchChannel.Placeholder => scope is null
                ? this.page.GetByPlaceholder(spec.Text!, new PageGetByPlaceholderOptions { Exact = spec.Exact })
                : scope.GetByPlaceholder(spec.Text!, new LocatorGetByPlaceholderOptions { Exact = spec.Exact }),
            UiMatchChannel.Text => scope is null
                ? this.page.GetByText(spec.Text!, new PageGetByTextOptions { Exact = spec.Exact })
                : scope.GetByText(spec.Text!, new LocatorGetByTextOptions { Exact = spec.Exact }),
            UiMatchChannel.TestId => this.ByAttribute(this.testIdAttribute, spec.Text, scope),
            UiMatchChannel.ElementId => this.ByAttribute("id", spec.Text, scope),
            UiMatchChannel.Css => scope is null ? this.page.Locator(spec.Css!) : scope.Locator(spec.Css!),
            _ => throw new InvalidOperationException($"Unknown channel '{spec.Channel}'."),
        };

        if (spec.NearText is { Length: > 0 } nearText)
        {
            // Nearness by containment rather than by geometry: the element must live in a part of the page
            // that carries the anchor text. Coarser than pixel distance, but it means what a reader thinks
            // it means, and it does not change when the layout does.
            located = located.Filter(new LocatorFilterOptions
            {
                Has = this.page.Locator("xpath=ancestor-or-self::*").Filter(new LocatorFilterOptions { HasText = nearText }),
            });
        }

        return located;
    }

    private ILocator ByRole(UiQuerySpec spec, ILocator? scope)
    {
        AriaRole role = ParseRole(spec.Role);

        if (spec.Text is not { Length: > 0 })
        {
            return scope is null
                ? this.page.GetByRole(role)
                : scope.GetByRole(role);
        }

        return scope is null
            ? this.page.GetByRole(role, new PageGetByRoleOptions { Name = spec.Text, Exact = spec.Exact })
            : scope.GetByRole(role, new LocatorGetByRoleOptions { Name = spec.Text, Exact = spec.Exact });
    }

    private ILocator ByAttribute(string attribute, string? value, ILocator? scope)
    {
        string selector = value is { Length: > 0 }
            ? string.Format(CultureInfo.InvariantCulture, "[{0}=\"{1}\"]", attribute, value.Replace("\"", "\\\"", StringComparison.Ordinal))
            : string.Format(CultureInfo.InvariantCulture, "[{0}]", attribute);

        return scope is null ? this.page.Locator(selector) : scope.Locator(selector);
    }

    private static AriaRole ParseRole(string? role)
        => Enum.TryParse(role, ignoreCase: true, out AriaRole parsed)
            ? parsed
            : throw new PlaywrightException($"'{role}' is not an ARIA role Playwright knows.");

    private async Task<UiCandidate> DescribeOneAsync(ILocator element, int index)
    {
        // One round trip per candidate rather than five: this runs while building a failure message, and a
        // failure message should not be slow enough to be noticed.
        //
        // Read as a JSON element rather than into a record: Playwright's own serializer maps to primitives
        // and JSON, not to arbitrary .NET types, and asking it for one fails at run time rather than at
        // compile time.
        JsonElement? described = await element
            .EvaluateAsync<JsonElement?>(
                """
                element => {
                    const attributeName = ['data-testid', 'data-test-id', 'data-test'].find(name => element.hasAttribute(name));
                    const container = element.closest('[role="region"],[role="group"],[role="form"],section,fieldset,form,article');
                    const heading = container ? container.querySelector('h1,h2,h3,h4,h5,h6,legend') : null;

                    return {
                        snippet: element.outerHTML,
                        accessibleName: element.getAttribute('aria-label') || (element.innerText || element.textContent || '').trim(),
                        sectionName: container
                            ? (container.getAttribute('aria-label') || (heading ? heading.textContent.trim() : null))
                            : null,
                        testId: attributeName ? element.getAttribute(attributeName) : null
                    };
                }
                """)
            .ConfigureAwait(false);

        if (described is not { ValueKind: JsonValueKind.Object } description)
        {
            return new UiCandidate(index, "<unknown>");
        }

        return new UiCandidate(
            index,
            UiText.Truncate(UiText.Normalize(Text(description, "snippet")), SnippetLength) ?? "<unknown>",
            Blank(Text(description, "accessibleName")),
            Blank(Text(description, "sectionName")),
            Blank(Text(description, "testId")));
    }

    private static string? Text(JsonElement element, string property)
        => element.TryGetProperty(property, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private async Task<string?> AccessibleNameAsync(ILocator element)
    {
        try
        {
            string? name = await element
                .EvaluateAsync<string?>(
                    """
                    element => {
                        // The name channels of the accessible-name computation, in its order - so the
                        // suggestion for a field is the label a person reads, however the page wired it.
                        const labelledBy = element.getAttribute('aria-labelledby');

                        if (labelledBy) {
                            const text = labelledBy
                                .split(/\s+/)
                                .map(id => document.getElementById(id)?.innerText || '')
                                .join(' ')
                                .trim();

                            if (text) {
                                return text;
                            }
                        }

                        const aria = element.getAttribute('aria-label');

                        if (aria) {
                            return aria;
                        }

                        if (element.labels && element.labels.length > 0) {
                            const text = Array.from(element.labels, label => label.innerText).join(' ').trim();

                            if (text) {
                                return text;
                            }
                        }

                        return element.getAttribute('placeholder')
                            || element.getAttribute('title')
                            || (element.innerText || element.textContent || '').trim();
                    }
                    """)
                .ConfigureAwait(false);

            return UiText.Truncate(UiText.Normalize(name), 60);
        }
        catch (PlaywrightException)
        {
            return null;
        }
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : UiText.Normalize(value);
}
