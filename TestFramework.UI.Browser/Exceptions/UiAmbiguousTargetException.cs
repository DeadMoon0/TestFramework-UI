using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using TestFramework.UI.Browser.Resolution;
using TestFramework.UI.Browser.Targeting;

namespace TestFramework.UI.Browser.Exceptions;

/// <summary>
/// Thrown when a target names more than one element and the test did not say which one it meant.
/// </summary>
/// <remarks>
/// The message lists every candidate and, for each dial that would work, the line to write. A reader
/// should be able to fix the test from the failure alone without opening the page - that is the whole
/// point of failing here rather than pressing something plausible.
/// </remarks>
public sealed class UiAmbiguousTargetException : Exception
{
    internal UiAmbiguousTargetException(
        string app,
        string url,
        UiTarget target,
        UiQuerySpec spec,
        int matchCount,
        IReadOnlyList<UiCandidate> candidates)
        : base(BuildMessage(app, url, target, spec, matchCount, candidates))
    {
        this.App = app;
        this.Url = url;
        this.TargetDescription = target.Describe();
        this.MatchedVia = spec.DescribeMatch();
        this.MatchCount = matchCount;
        this.Candidates = candidates;
    }

    /// <summary>The application identifier whose page was searched.</summary>
    public string App { get; }

    /// <summary>The address the page was on.</summary>
    public string Url { get; }

    /// <summary>How the test described the element.</summary>
    public string TargetDescription { get; }

    /// <summary>The channel that found several elements.</summary>
    public string MatchedVia { get; }

    /// <summary>How many elements matched.</summary>
    public int MatchCount { get; }

    /// <summary>The matching elements, as far as they were described.</summary>
    public IReadOnlyList<UiCandidate> Candidates { get; }

    private static string BuildMessage(
        string app,
        string url,
        UiTarget target,
        UiQuerySpec spec,
        int matchCount,
        IReadOnlyList<UiCandidate> candidates)
    {
        StringBuilder message = new StringBuilder();

        message.Append(CultureInfo.InvariantCulture, $"The {target.Describe()} matches {matchCount} elements");
        message.Append(CultureInfo.InvariantCulture, $" on '{app}' at {url}");
        message.AppendLine(", so the run will not guess which one it is.");
        message.AppendLine(CultureInfo.InvariantCulture, $"Found through: {spec.Describe()}.");

        if (candidates.Count > 0)
        {
            message.AppendLine();
            message.AppendLine("Candidates:");

            foreach (UiCandidate candidate in candidates)
            {
                message.AppendLine(CultureInfo.InvariantCulture, $"  {candidate.Index}. {candidate}");
            }
        }

        message.AppendLine();
        message.AppendLine("Name it more precisely, for example:");

        foreach (string suggestion in BuildSuggestions(target, candidates))
        {
            message.AppendLine(CultureInfo.InvariantCulture, $"  {suggestion}");
        }

        return message.ToString().TrimEnd();
    }

    private static IEnumerable<string> BuildSuggestions(UiTarget target, IReadOnlyList<UiCandidate> candidates)
    {
        // A section name tells identical controls apart far better than a position does, so lead with
        // the sections actually present rather than with an index the page may reorder tomorrow.
        IEnumerable<string> sections = candidates
            .Select(static candidate => candidate.SectionName)
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .Take(3);

        bool suggestedSomething = false;

        foreach (string section in sections)
        {
            suggestedSomething = true;
            yield return target.InSection(section).ToSourceCode();
        }

        if (candidates.FirstOrDefault(static candidate => candidate.TestId is not null) is { TestId: { } testId })
        {
            suggestedSomething = true;
            yield return Target.TestId(testId).ToSourceCode();
        }

        if (!suggestedSomething)
        {
            yield return target.Nth(0).ToSourceCode() + "   // by position";
        }

        yield return target.First().ToSourceCode() + "   // accept the first match";
    }
}
