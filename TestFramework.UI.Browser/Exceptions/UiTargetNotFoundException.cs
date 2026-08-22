using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using TestFramework.UI.Browser.Resolution;
using TestFramework.UI.Browser.Targeting;

namespace TestFramework.UI.Browser.Exceptions;

/// <summary>
/// Thrown when nothing on the page answers to the target.
/// </summary>
/// <remarks>
/// The message says what was tried, what the page does offer instead, and - when one of those is close
/// enough to be the obvious intent - the exact line that would have worked. A control renamed from
/// <c>Save</c> to <c>Save changes</c> should cost a reader one glance, not a debugging session.
/// </remarks>
public sealed class UiTargetNotFoundException : Exception
{
    internal UiTargetNotFoundException(
        string app,
        string url,
        UiTarget target,
        IReadOnlyList<UiQuerySpec> triedSpecs,
        IReadOnlyList<string> availableNames)
        : base(BuildMessage(app, url, target, triedSpecs, availableNames))
    {
        this.App = app;
        this.Url = url;
        this.TargetDescription = target.Describe();
        this.Tried = triedSpecs.Select(static spec => spec.Describe()).ToList();
        this.AvailableNames = availableNames;
        this.ClosestName = FindClosest(target.Name, availableNames);
    }

    /// <summary>The application identifier whose page was searched.</summary>
    public string App { get; }

    /// <summary>The address the page was on.</summary>
    public string Url { get; }

    /// <summary>How the test described the element.</summary>
    public string TargetDescription { get; }

    /// <summary>Every lookup that was attempted, in order.</summary>
    public IReadOnlyList<string> Tried { get; }

    /// <summary>The names the page does offer for this kind of element.</summary>
    public IReadOnlyList<string> AvailableNames { get; }

    /// <summary>The available name closest to the one asked for, when there is a plausible one.</summary>
    public string? ClosestName { get; }

    private static string BuildMessage(
        string app,
        string url,
        UiTarget target,
        IReadOnlyList<UiQuerySpec> triedSpecs,
        IReadOnlyList<string> availableNames)
    {
        StringBuilder message = new StringBuilder();

        message.AppendLine(CultureInfo.InvariantCulture,
            $"No element matching {target.Describe()} was found on '{app}' at {url}.");

        if (triedSpecs.Count > 0)
        {
            message.AppendLine(CultureInfo.InvariantCulture,
                $"Tried, in order: {string.Join(", ", triedSpecs.Select(static spec => spec.Describe()))}.");
        }

        if (availableNames.Count > 0)
        {
            message.AppendLine();
            message.AppendLine(CultureInfo.InvariantCulture,
                $"The page does offer: {string.Join(", ", availableNames.Select(static name => $"'{name}'"))}.");

            if (FindClosest(target.Name, availableNames) is { } closest)
            {
                UiTarget corrected = target with { Name = closest };
                message.AppendLine(CultureInfo.InvariantCulture, $"Did you mean: {corrected.ToSourceCode()}");
            }
        }
        else
        {
            message.AppendLine();
            message.AppendLine(
                "The page offers no element of that kind at all - it may not have finished rendering, " +
                "or the run may be on a different page than the test assumes. Wait for the element " +
                "with a WaitForEvent step, or check the address above.");
        }

        return message.ToString().TrimEnd();
    }

    private static string? FindClosest(string? wanted, IReadOnlyList<string> availableNames)
    {
        if (string.IsNullOrWhiteSpace(wanted) || availableNames.Count == 0)
        {
            return null;
        }

        string normalizedWanted = UiText.Normalize(wanted)!;

        // A name that contains what was asked for is almost always the renamed control: "Save" ->
        // "Save changes". Prefer the shortest such name, as it added the least.
        string? containing = availableNames
            .Where(name => UiText.ContainsNormalized(name, normalizedWanted, ignoreCase: true))
            .OrderBy(static name => name.Length)
            .FirstOrDefault();

        if (containing is not null)
        {
            return containing;
        }

        // Otherwise fall back to edit distance, but only report a genuinely near miss - a wrong
        // suggestion is worse than none, because it sends the reader looking in the wrong place.
        int budget = Math.Max(2, normalizedWanted.Length / 3);

        return availableNames
            .Select(name => (Name: name, Distance: Distance(normalizedWanted, UiText.Normalize(name)!)))
            .Where(candidate => candidate.Distance <= budget)
            .OrderBy(static candidate => candidate.Distance)
            .Select(static candidate => candidate.Name)
            .FirstOrDefault();
    }

    private static int Distance(string left, string right)
    {
        // Levenshtein with two rolling rows: the strings compared here are element names, so this
        // never runs on anything long enough to warrant more.
        int[] previous = new int[right.Length + 1];
        int[] current = new int[right.Length + 1];

        for (int index = 0; index <= right.Length; index++)
        {
            previous[index] = index;
        }

        for (int leftIndex = 1; leftIndex <= left.Length; leftIndex++)
        {
            current[0] = leftIndex;

            for (int rightIndex = 1; rightIndex <= right.Length; rightIndex++)
            {
                int substitution = char.ToUpperInvariant(left[leftIndex - 1]) == char.ToUpperInvariant(right[rightIndex - 1])
                    ? 0
                    : 1;

                current[rightIndex] = Math.Min(
                    Math.Min(current[rightIndex - 1] + 1, previous[rightIndex] + 1),
                    previous[rightIndex - 1] + substitution);
            }

            (previous, current) = (current, previous);
        }

        return previous[right.Length];
    }
}
