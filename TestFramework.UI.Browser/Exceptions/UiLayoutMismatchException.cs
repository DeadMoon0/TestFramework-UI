using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace TestFramework.UI.Browser.Exceptions;

/// <summary>
/// Thrown when a page is not laid out the way a test says it should be.
/// </summary>
/// <remarks>
/// Every violated relation with both actual rectangles, so the reader sees the page's answer without
/// opening it - and the note that the comparison kept looking while the page settled, so a slow render
/// is never mistaken for a broken layout.
/// </remarks>
public sealed class UiLayoutMismatchException : Exception
{
    internal UiLayoutMismatchException(
        string app,
        string url,
        IReadOnlyList<string> differences,
        string actual,
        TimeSpan waited)
        : base(BuildMessage(app, url, differences, actual, waited))
    {
        this.App = app;
        this.Url = url;
        this.Differences = differences;
    }

    /// <summary>The application whose layout was checked.</summary>
    public string App { get; }

    /// <summary>The address the page was on.</summary>
    public string Url { get; }

    /// <summary>Every relation the page violated.</summary>
    public IReadOnlyList<string> Differences { get; }

    private static string BuildMessage(
        string app,
        string url,
        IReadOnlyList<string> differences,
        string actual,
        TimeSpan waited)
    {
        StringBuilder message = new StringBuilder();

        message.AppendLine(CultureInfo.InvariantCulture,
            $"The layout of '{app}' is not what the test expects ({differences.Count} difference(s)), at {url}.");
        message.AppendLine(string.Create(CultureInfo.InvariantCulture,
            $"The check was retried until the page settled, for {waited.TotalSeconds:F1}s."));
        message.AppendLine();

        foreach (string difference in differences)
        {
            message.AppendLine(CultureInfo.InvariantCulture, $"  {difference}");
        }

        if (actual.Length > 0)
        {
            message.AppendLine();
            message.AppendLine("Where everything measured is:");
            message.AppendLine(actual);
        }

        return message.ToString().TrimEnd();
    }
}
