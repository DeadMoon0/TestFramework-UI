using System;
using System.Text.RegularExpressions;

namespace TestFramework.UI.Structure;

/// <summary>
/// One expectation about a single piece of text a page produced - a table cell, an element's text, an
/// attribute value.
/// </summary>
/// <remarks>
/// A rule carries its own description because that description is what a failure message shows and
/// what travels to the debugging UI. The delegate behind <see cref="Cell.Satisfies"/> stays where it
/// was written and never leaves the test process; only the description does.
/// </remarks>
public sealed class CellRule
{
    private readonly Func<string?, bool> predicate;

    internal CellRule(string description, Func<string?, bool> predicate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentNullException.ThrowIfNull(predicate);

        this.Description = description;
        this.predicate = predicate;
    }

    /// <summary>
    /// How this rule reads in a failure message, for example <c>matches '\d+'</c> or <c>is 'Anvil'</c>.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// Applies the rule to what the page actually produced.
    /// </summary>
    /// <param name="actual">The text found on the page. May be null when the element carried none.</param>
    /// <returns>True when the text satisfies this rule.</returns>
    public bool Matches(string? actual) => this.predicate(actual);

    /// <summary>
    /// Lets a plain string stand in for a rule, so an expected table reads as data rather than as
    /// calls. The string is compared after normalization.
    /// </summary>
    /// <param name="expected">The expected text.</param>
    public static implicit operator CellRule(string expected) => Cell.Exactly(expected);

    /// <summary>
    /// Returns <see cref="Description"/>.
    /// </summary>
    /// <returns>The rule's description.</returns>
    public override string ToString() => this.Description;
}

/// <summary>
/// The tolerance markers a test places inside expected data: ignore this value, match a fragment,
/// match a pattern, or judge it with a predicate.
/// </summary>
/// <remarks>
/// Markers sit inline in the expected structure rather than in a parallel argument, because a reader
/// reviewing the expectation should see which parts are pinned and which are deliberately loose
/// without cross-referencing anything.
/// </remarks>
public static class Cell
{
    /// <summary>
    /// Accepts anything, including a missing value. Use it to show that a column exists but its
    /// content is not this test's subject.
    /// </summary>
    public static CellRule Any { get; } = new CellRule("is anything", static _ => true);

    /// <summary>
    /// Accepts only a value that is null or normalizes to the empty string.
    /// </summary>
    public static CellRule Empty { get; } = new CellRule(
        "is empty",
        static actual => string.IsNullOrEmpty(UiText.Normalize(actual)));

    /// <summary>
    /// Accepts any value that is not null and does not normalize to the empty string.
    /// </summary>
    public static CellRule NotEmpty { get; } = new CellRule(
        "is not empty",
        static actual => !string.IsNullOrEmpty(UiText.Normalize(actual)));

    /// <summary>
    /// Accepts a value equal to <paramref name="expected"/> after normalizing both. Case-sensitive.
    /// </summary>
    /// <param name="expected">The expected text.</param>
    /// <returns>The rule.</returns>
    public static CellRule Exactly(string expected)
    {
        ArgumentNullException.ThrowIfNull(expected);

        return new CellRule(
            $"is '{UiText.Normalize(expected)}'",
            actual => UiText.EqualsNormalized(actual, expected));
    }

    /// <summary>
    /// Accepts a value containing <paramref name="fragment"/> after normalizing both.
    /// </summary>
    /// <param name="fragment">The fragment to look for.</param>
    /// <param name="ignoreCase">True to compare without regard to case.</param>
    /// <returns>The rule.</returns>
    public static CellRule Contains(string fragment, bool ignoreCase = false)
    {
        ArgumentNullException.ThrowIfNull(fragment);

        return new CellRule(
            $"contains '{UiText.Normalize(fragment)}'",
            actual => UiText.ContainsNormalized(actual, fragment, ignoreCase));
    }

    /// <summary>
    /// Accepts a value the regular expression finds a match in. Use it for text a run cannot pin
    /// down, such as a generated identifier or a formatted amount.
    /// </summary>
    /// <param name="pattern">The regular expression.</param>
    /// <returns>The rule.</returns>
    public static CellRule Matches(string pattern)
    {
        ArgumentException.ThrowIfNullOrEmpty(pattern);

        // Compiled once per rule rather than per comparison: a rule is reused for every row of a
        // table and every retry of the comparison against a page that has not settled yet.
        Regex regex = new Regex(pattern, RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

        return new CellRule(
            $"matches '{pattern}'",
            actual => regex.IsMatch(UiText.Normalize(actual) ?? string.Empty));
    }

    /// <summary>
    /// Accepts a value the predicate approves. The description is what a failure message shows, so
    /// write it as the rule reads: <c>"is a positive number"</c>.
    /// </summary>
    /// <param name="rule">The predicate, receiving the raw (un-normalized) text.</param>
    /// <param name="description">How the rule reads in a failure message.</param>
    /// <returns>The rule.</returns>
    public static CellRule Satisfies(Func<string?, bool> rule, string description)
        => new CellRule(description, rule);
}
