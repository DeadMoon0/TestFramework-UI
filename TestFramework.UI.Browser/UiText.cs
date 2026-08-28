using System;
using System.Text;

namespace TestFramework.UI;

/// <summary>
/// Text normalization shared by everything that compares what a page says to what a test expected.
/// </summary>
/// <remarks>
/// Normalization happens even when a comparison is exact. A page that wraps a label across two lines,
/// indents it by a tab, or separates two words with a non-breaking space is saying the same thing as
/// the test, and the established UI test tools all agree on this: collapse runs of whitespace into a
/// single space and trim the ends. Without it, "exact" would mean "exact including the HTML author's
/// formatting", which no test author intends.
/// </remarks>
public static class UiText
{
    /// <summary>
    /// Collapses every run of whitespace - including line breaks, tabs and non-breaking spaces - into
    /// a single space and trims both ends.
    /// </summary>
    /// <param name="value">The text to normalize. May be null.</param>
    /// <returns>The normalized text, or null when <paramref name="value"/> is null.</returns>
    public static string? Normalize(string? value)
    {
        if (value is null)
        {
            return null;
        }

        StringBuilder builder = new StringBuilder(value.Length);
        bool pendingSpace = false;

        foreach (char character in value)
        {
            // char.IsWhiteSpace covers the non-breaking space too, which is the point: a page
            // separating words with &nbsp; is saying the same thing as one using a plain space.
            if (char.IsWhiteSpace(character))
            {
                // Only remember that a gap occurred; whether it becomes a space depends on what follows.
                pendingSpace = builder.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }

            builder.Append(character);
        }

        return builder.ToString();
    }

    /// <summary>
    /// Compares two texts for equality after normalizing both. Case-sensitive.
    /// </summary>
    /// <param name="actual">The text a page produced.</param>
    /// <param name="expected">The text a test expects.</param>
    /// <returns>True when both normalize to the same string.</returns>
    public static bool EqualsNormalized(string? actual, string? expected)
        => string.Equals(Normalize(actual), Normalize(expected), StringComparison.Ordinal);

    /// <summary>
    /// Determines whether the normalized <paramref name="actual"/> contains the normalized
    /// <paramref name="fragment"/>.
    /// </summary>
    /// <param name="actual">The text a page produced.</param>
    /// <param name="fragment">The fragment a test expects to find.</param>
    /// <param name="ignoreCase">True to compare without regard to case.</param>
    /// <returns>True when the fragment occurs in the text.</returns>
    public static bool ContainsNormalized(string? actual, string? fragment, bool ignoreCase = false)
    {
        string normalizedActual = Normalize(actual) ?? string.Empty;
        string normalizedFragment = Normalize(fragment) ?? string.Empty;

        return normalizedActual.Contains(
            normalizedFragment,
            ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    }

    /// <summary>
    /// Shortens text for a failure message or a trace entry, appending an ellipsis when it was cut.
    /// </summary>
    /// <param name="value">The text to shorten. May be null.</param>
    /// <param name="maxLength">The maximum number of characters to keep. Must be positive.</param>
    /// <returns>The shortened text, or null when <paramref name="value"/> is null.</returns>
    public static string? Truncate(string? value, int maxLength)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxLength);

        if (value is null || value.Length <= maxLength)
        {
            return value;
        }

        return string.Concat(value.AsSpan(0, maxLength), "...");
    }
}
