using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using TestFramework.UI.Structure;

namespace TestFramework.UI.Browser.Structure;

/// <summary>
/// Writes out what a page actually was, as the C# that would expect it.
/// </summary>
/// <remarks>
/// <para>
/// The point of a failing structure comparison is not to prove the test wrong; it is to get the reader to
/// a correct expectation. So the failure hands over the expectation that <em>would</em> have passed, in the
/// same form the test is written in, ready to read, edit and paste. The format a failure prints and the
/// format a test is written in are the same format - which is the one idea that made accessibility-tree
/// snapshots work where DOM snapshots did not.
/// </para>
/// <para>
/// It is offered rather than applied. Nothing writes into a test file, and there is no bulk update
/// command: an expectation that changed should be read by a person, because a tool that regenerates them
/// in bulk turns a suite into a rubber stamp.
/// </para>
/// </remarks>
internal static class StructureCodeRenderer
{
    /// <summary>How deep the rendered expectation goes before it stops.</summary>
    private const int MaxDepth = 4;

    /// <summary>
    /// Renders a snapshot as a <c>WebElementStructure</c> declaration.
    /// </summary>
    /// <param name="snapshot">What the page was.</param>
    /// <param name="name">The field name to give it.</param>
    /// <returns>The C# source.</returns>
    public static string Render(UiElementSnapshot snapshot, string name = "Expected")
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        StringBuilder builder = new StringBuilder();

        builder.AppendLine(CultureInfo.InvariantCulture, $"private static readonly WebElementStructure {name} = WebElementStructure");
        builder.Append(CultureInfo.InvariantCulture, $"    .OneElement({Quote(snapshot.Tag)})");

        AppendRules(snapshot, builder, indent: 8);
        AppendChildren(snapshot, builder, depth: 1, indent: 8);

        builder.Append(';');

        return builder.ToString();
    }

    /// <summary>
    /// Renders a table snapshot as an <c>ExpectedTable</c> declaration.
    /// </summary>
    /// <param name="snapshot">What the table held.</param>
    /// <returns>The C# source.</returns>
    public static string Render(UiTableSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        StringBuilder builder = new StringBuilder();

        builder.AppendLine(CultureInfo.InvariantCulture,
            $"ExpectedTable.WithHeader({string.Join(", ", snapshot.Columns.Select(Quote))})");

        foreach (IReadOnlyList<string> row in snapshot.Rows)
        {
            builder.AppendLine(CultureInfo.InvariantCulture,
                $"    .Row({string.Join(", ", row.Select(Quote))})");
        }

        return builder.ToString().TrimEnd();
    }

    private static void AppendRules(UiElementSnapshot snapshot, StringBuilder builder, int indent)
    {
        string pad = new string(' ', indent);

        // Only the attributes a test would plausibly pin. An identifier or a name says something about the
        // element; a generated width does not, and putting it in the suggestion would teach the reader to
        // write brittle expectations.
        foreach ((string attribute, string value) in Meaningful(snapshot.Attributes))
        {
            builder.AppendLine();
            builder.Append(CultureInfo.InvariantCulture, $"{pad}.WithAttribute({Quote(attribute)}, {Quote(value)})");
        }

        if (snapshot.Children.Count == 0 && !string.IsNullOrEmpty(snapshot.Text))
        {
            builder.AppendLine();
            builder.Append(CultureInfo.InvariantCulture, $"{pad}.WithText({Quote(snapshot.Text)})");
        }
    }

    private static void AppendChildren(UiElementSnapshot snapshot, StringBuilder builder, int depth, int indent)
    {
        if (snapshot.Children.Count == 0 || depth > MaxDepth)
        {
            return;
        }

        string pad = new string(' ', indent);

        // A distinct name per level, because C# will not let a nested lambda reuse the enclosing one's
        // parameter - and suggested code that does not compile is worse than none.
        string parameter = depth == 1 ? "x" : $"x{depth}";

        builder.AppendLine();
        builder.AppendLine(CultureInfo.InvariantCulture, $"{pad}.Containing({parameter} => {parameter}");

        // Repeated elements of the same kind become one expectation of how many there are, which is what a
        // test author would have written and a tenth of the noise.
        foreach (IGrouping<string, UiElementSnapshot> group in snapshot.Children.GroupBy(static child => child.Tag))
        {
            UiElementSnapshot first = group.First();
            int count = group.Count();

            string call = count == 1
                ? $".OneElement({Quote(group.Key)})"
                : string.Format(CultureInfo.InvariantCulture, ".Exactly({0}, {1})", count, Quote(group.Key));

            builder.Append(CultureInfo.InvariantCulture, $"{pad}    {call}");

            if (count == 1)
            {
                AppendRules(first, builder, indent + 8);
                AppendChildren(first, builder, depth + 1, indent + 8);
            }

            builder.AppendLine();
        }

        builder.Append(CultureInfo.InvariantCulture, $"{pad}    )");
    }

    private static IEnumerable<(string Attribute, string Value)> Meaningful(IReadOnlyDictionary<string, string> attributes)
        => attributes
            .Where(static pair => pair.Key is "id" or "name" or "role" or "aria-label" or "type" or "href"
                || pair.Key.StartsWith("data-", StringComparison.OrdinalIgnoreCase))
            .OrderBy(static pair => pair.Key, StringComparer.Ordinal)
            .Select(static pair => (pair.Key, pair.Value));

    private static string Quote(string? value)
        => value is null ? "null" : $"\"{value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal)}\"";
}
