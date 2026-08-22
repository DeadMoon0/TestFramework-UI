using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.UI;
using TestFramework.UI.Browser.Resolution;
using TestFramework.UI.Browser.Targeting;

namespace TestFramework.UI.Browser.Tests.Fakes;

/// <summary>
/// One element a fake page offers, described by everything a lookup could match it on.
/// </summary>
internal sealed record FakeElement(
    string Tag,
    string? Role = null,
    string? Name = null,
    string? Label = null,
    string? Placeholder = null,
    string? TestId = null,
    string? Text = null,
    string? Section = null,
    string? Id = null,
    IReadOnlyList<string>? Selectors = null)
{
    public string Snippet => $"<{this.Tag}>{this.Text ?? this.Name ?? this.Label ?? string.Empty}</{this.Tag}>";
}

/// <summary>
/// A page that answers lookups from a declared list of elements.
/// </summary>
/// <remarks>
/// The whole matching ladder is decided by counting answers, so a page made of records is enough to
/// test it - which channel wins, what counts as ambiguous, what a failure says. A test here declares
/// the page it means and reads like the HTML it stands for, and the suite stays runnable on a machine
/// with no browser installed.
/// </remarks>
internal sealed class FakePage : IUiElementQuery
{
    private readonly List<FakeElement> elements;

    public FakePage(params FakeElement[] elements) => this.elements = elements.ToList();

    /// <summary>Every lookup the page was asked, in order - so a test can assert on the ladder itself.</summary>
    public List<UiQuerySpec> Queries { get; } = new List<UiQuerySpec>();

    public Task<int> CountAsync(UiQuerySpec spec, CancellationToken cancellationToken)
    {
        this.Queries.Add(spec);

        return Task.FromResult(this.Match(spec).Count);
    }

    public Task<IReadOnlyList<UiCandidate>> DescribeAsync(UiQuerySpec spec, int max, CancellationToken cancellationToken)
    {
        IReadOnlyList<UiCandidate> candidates = this.Match(spec)
            .Take(max)
            .Select((element, index) => new UiCandidate(
                index,
                element.Snippet,
                element.Name,
                element.Section,
                element.TestId))
            .ToList();

        return Task.FromResult(candidates);
    }

    public Task<IReadOnlyList<string>> AvailableNamesAsync(UiQuerySpec spec, int max, CancellationToken cancellationToken)
    {
        // The text is ignored on purpose: this asks what the page offers, not whether it offers what
        // was wanted.
        IReadOnlyList<string> names = this.Match(spec with { Text = null, Exact = false })
            .Select(element => NameFor(element, spec.Channel))
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .Take(max)
            .ToList();

        return Task.FromResult(names);
    }

    private static string? NameFor(FakeElement element, UiMatchChannel channel)
        => channel switch
        {
            UiMatchChannel.Role => element.Name,
            UiMatchChannel.Label => element.Label,
            UiMatchChannel.Placeholder => element.Placeholder,
            UiMatchChannel.TestId => element.TestId,
            UiMatchChannel.Text => element.Text,
            UiMatchChannel.ElementId => element.Id,
            _ => null,
        };

    private static bool Matches(string? actual, string? wanted, bool exact)
    {
        if (wanted is null)
        {
            return actual is not null;
        }

        return exact
            ? UiText.EqualsNormalized(actual, wanted)
            : UiText.ContainsNormalized(actual, wanted, ignoreCase: true);
    }

    private List<FakeElement> Match(UiQuerySpec spec)
    {
        IEnumerable<FakeElement> scoped = this.elements;

        if (spec.Within is { } within)
        {
            List<FakeElement> scopes = this.Match(within);

            if (spec.WithinIndex >= scopes.Count)
            {
                return new List<FakeElement>();
            }

            string? sectionName = scopes[spec.WithinIndex].Name;
            scoped = scoped.Where(element => string.Equals(element.Section, sectionName, StringComparison.Ordinal));
        }

        if (spec.NearText is { } nearText)
        {
            // Proximity stands in for geometry here: an element qualifies when its own section carries
            // the anchor text. Enough to prove the dial reaches the lookup.
            scoped = scoped.Where(element => UiText.ContainsNormalized(element.Section, nearText, ignoreCase: true));
        }

        return scoped.Where(element => spec.Channel switch
        {
            UiMatchChannel.Role => string.Equals(element.Role, spec.Role, StringComparison.Ordinal)
                                   && Matches(element.Name, spec.Text, spec.Exact),
            UiMatchChannel.Label => Matches(element.Label, spec.Text, spec.Exact),
            UiMatchChannel.Placeholder => Matches(element.Placeholder, spec.Text, spec.Exact),
            UiMatchChannel.TestId => spec.Text is null
                ? element.TestId is not null
                : string.Equals(element.TestId, spec.Text, StringComparison.Ordinal),
            UiMatchChannel.Text => Matches(element.Text, spec.Text, spec.Exact),
            UiMatchChannel.ElementId => string.Equals(element.Id, spec.Text, StringComparison.Ordinal),
            UiMatchChannel.Css => element.Selectors?.Contains(spec.Css ?? string.Empty, StringComparer.Ordinal) == true,
            _ => false,
        }).ToList();
    }
}
