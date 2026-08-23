using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace TestFramework.UI.Browser.Exceptions;

/// <summary>
/// Thrown when a page's structure is not what a test expected.
/// </summary>
/// <remarks>
/// The message is built to be acted on rather than merely read: every difference with the path it is at,
/// then what the page actually was, then the expectation that would have passed - in the same form the
/// test is written in.
/// </remarks>
public sealed class UiStructureMismatchException : Exception
{
    internal UiStructureMismatchException(
        string app,
        string url,
        string scope,
        IReadOnlyList<string> differences,
        string actual,
        string? suggestedCode,
        TimeSpan waited)
        : base(BuildMessage(app, url, scope, differences, actual, suggestedCode, waited))
    {
        this.App = app;
        this.Url = url;
        this.Scope = scope;
        this.Differences = differences;
        this.Actual = actual;
        this.SuggestedCode = suggestedCode;
    }

    /// <summary>The application whose page was compared.</summary>
    public string App { get; }

    /// <summary>The address the page was on.</summary>
    public string Url { get; }

    /// <summary>What was compared.</summary>
    public string Scope { get; }

    /// <summary>Every way the page differed.</summary>
    public IReadOnlyList<string> Differences { get; }

    /// <summary>What the page actually was.</summary>
    public string Actual { get; }

    /// <summary>The expectation that would have passed.</summary>
    public string? SuggestedCode { get; }

    private static string BuildMessage(
        string app,
        string url,
        string scope,
        IReadOnlyList<string> differences,
        string actual,
        string? suggestedCode,
        TimeSpan waited)
    {
        StringBuilder message = new StringBuilder();

        message.AppendLine(CultureInfo.InvariantCulture,
            $"{scope} on '{app}' is not what the test expects ({differences.Count} difference(s)), at {url}.");

        // Said explicitly, because "it did not match" reads very differently once you know the run kept
        // looking for five seconds while the page settled.
        message.AppendLine(CultureInfo.InvariantCulture,
            $"The comparison was retried until the page settled, for {waited.TotalSeconds:F1}s.");

        message.AppendLine();

        foreach (string difference in differences)
        {
            message.AppendLine(CultureInfo.InvariantCulture, $"  {difference}");
        }

        message.AppendLine();
        message.AppendLine("What the page actually is:");
        message.AppendLine(actual);

        if (suggestedCode is { Length: > 0 })
        {
            message.AppendLine();
            message.AppendLine("The expectation that would pass, if this is the intended structure:");
            message.AppendLine();
            message.AppendLine(suggestedCode);
        }

        return message.ToString().TrimEnd();
    }
}
