using TestFramework.UI.Browser.Events;
using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Playwright;
using TestFramework.UI.Browser.Scripting;
using TestFramework.UI.Structure;

namespace TestFramework.UI.Browser.Structure;

/// <summary>
/// Takes the picture of a page's structure that a comparison is made against.
/// </summary>
/// <remarks>
/// <para>
/// One round trip produces the whole subtree, so the comparison is made against a single consistent
/// moment. Comparing against the live page instead would mean racing it, and a structure that changed
/// halfway through a comparison would produce differences that describe neither state.
/// </para>
/// <para>
/// What is left out is as deliberate as what is kept. Presentation - <c>class</c>, <c>style</c> - and
/// framework bookkeeping - Angular's <c>_ngcontent-*</c> markers, <c>ng-reflect-*</c>, and the
/// <c>ng-pristine</c>/<c>ng-valid</c> state classes - never enter the snapshot. Two reasons: a test about
/// structure should not fail because somebody restyled a panel, and a snapshot captured for drift
/// detection would otherwise change on every rebuild and every keystroke in a form, which would make the
/// comparison worthless within a week. Elements a person cannot see are skipped for the same reason.
/// </para>
/// </remarks>
internal static class DomProjector
{
    /// <summary>How deep a snapshot goes before it stops descending.</summary>
    public const int MaxDepth = 8;

    /// <summary>How many elements a snapshot holds at most.</summary>
    public const int MaxNodes = 600;

    private const string ProjectScript = """
        (element, options) => {
            const skippedTags = new Set(['script', 'style', 'template', 'noscript', 'link', 'meta']);
            const skippedAttributes = ['class', 'style'];
            let budget = options.maxNodes;

            const isNoise = name =>
                name.startsWith('_ng') ||
                name.startsWith('ng-reflect-') ||
                name.startsWith('_nghost') ||
                skippedAttributes.includes(name);

            const isVisible = node => {
                const style = getComputedStyle(node);

                return style.display !== 'none' && style.visibility !== 'hidden';
            };

            const project = (node, depth) => {
                if (budget-- <= 0) {
                    return null;
                }

                const attributes = {};

                for (const attribute of node.attributes) {
                    if (!isNoise(attribute.name)) {
                        attributes[attribute.name] = attribute.value;
                    }
                }

                const children = [];

                if (depth < options.maxDepth) {
                    for (const child of node.children) {
                        if (skippedTags.has(child.tagName.toLowerCase()) || !isVisible(child)) {
                            continue;
                        }

                        const projected = project(child, depth + 1);

                        if (projected !== null) {
                            children.push(projected);
                        }
                    }
                }

                return {
                    tag: node.tagName.toLowerCase(),
                    attributes,
                    text: (node.innerText || node.textContent || '').trim(),
                    children
                };
            };

            return project(element, 0);
        }
        """;

    /// <summary>
    /// Takes a snapshot of one element and what is inside it.
    /// </summary>
    /// <param name="locator">The element to photograph.</param>
    /// <param name="budget">How long a browser call may take before the step needs the time back.</param>
    /// <param name="cancellationToken">Cancels the projection.</param>
    /// <returns>The snapshot.</returns>
    /// <exception cref="PlaywrightException">The element is not there.</exception>
    public static async Task<UiElementSnapshot> ProjectAsync(ILocator locator, ProbeBudget budget, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(locator);

        cancellationToken.ThrowIfCancellationRequested();

        JToken? projected = await PageJson
            .EvaluateAsync(locator, ProjectScript, new { maxDepth = MaxDepth, maxNodes = MaxNodes }, budget.Milliseconds)
            .ConfigureAwait(false);

        if (projected is not JObject root)
        {
            throw new PlaywrightException("The element could not be read from the page.");
        }

        return Read(root);
    }

    private static UiElementSnapshot Read(JObject node)
    {
        Dictionary<string, string> attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (node["attributes"] is JObject attributeObject)
        {
            foreach (KeyValuePair<string, JToken?> attribute in attributeObject)
            {
                attributes[attribute.Key] = attribute.Value?.Value<string>() ?? string.Empty;
            }
        }

        List<UiElementSnapshot> children = new List<UiElementSnapshot>();

        if (node["children"] is JArray childArray)
        {
            foreach (JToken child in childArray)
            {
                // The projection returns null for a node it ran out of budget for, and that null travels.
                // Reading it as a child would invent an element the page does not have.
                if (child is JObject childObject && childObject["tag"] is not null)
                {
                    children.Add(Read(childObject));
                }
            }
        }

        return new UiElementSnapshot(
            node["tag"]?.Value<string>() ?? "?",
            attributes,
            UiText.Normalize(node["text"]?.Value<string>()),
            children);
    }

    /// <summary>
    /// Renders a snapshot as indented text, for a captured value or a difference message.
    /// </summary>
    /// <param name="snapshot">The snapshot.</param>
    /// <returns>The rendered tree.</returns>
    public static string Render(UiElementSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        System.Text.StringBuilder builder = new System.Text.StringBuilder();

        Render(snapshot, 0, builder);

        return builder.ToString().TrimEnd();
    }

    private static void Render(UiElementSnapshot node, int depth, System.Text.StringBuilder builder)
    {
        builder.AppendLine(CultureInfo.InvariantCulture, $"{new string(' ', depth * 2)}{node}");

        foreach (UiElementSnapshot child in node.Children)
        {
            Render(child, depth + 1, builder);
        }
    }
}
