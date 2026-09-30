using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TestFramework.UI.Session;

namespace TestFramework.UI.Browser.Exceptions;

/// <summary>
/// Thrown when an action in a flow could not be carried out.
/// </summary>
/// <remarks>
/// Wraps whatever went wrong in the context a reader needs: which action of how many, what had already
/// worked, and whether the page itself was complaining; the evidence is recorded as run widgets. The inner
/// exception keeps the original diagnosis - a missing element, an ambiguous name, a browser timeout.
/// </remarks>
public sealed class UiActionFailedException : Exception
{
    internal UiActionFailedException(
        string app,
        string actionDescription,
        int actionNumber,
        int actionCount,
        UiSessionPicture picture,
        IReadOnlyList<string> consoleErrors,
        Exception inner)
        : base(BuildMessage(app, actionDescription, actionNumber, actionCount, picture, consoleErrors, inner), inner)
    {
        this.App = app;
        this.ActionDescription = actionDescription;
        this.ActionNumber = actionNumber;
        this.ActionCount = actionCount;
        this.ConsoleErrors = consoleErrors;
    }

    /// <summary>The application being driven.</summary>
    public string App { get; }

    /// <summary>The action that failed.</summary>
    public string ActionDescription { get; }

    /// <summary>Its position in the flow, counting from one.</summary>
    public int ActionNumber { get; }

    /// <summary>How many actions the flow had.</summary>
    public int ActionCount { get; }

    /// <summary>What the page complained about while this step ran.</summary>
    public IReadOnlyList<string> ConsoleErrors { get; }

    private static string BuildMessage(
        string app,
        string actionDescription,
        int actionNumber,
        int actionCount,
        UiSessionPicture picture,
        IReadOnlyList<string> consoleErrors,
        Exception inner)
    {
        StringBuilder message = new StringBuilder();

        message.AppendLine(CultureInfo.InvariantCulture,
            $"Action {actionNumber} of {actionCount} on '{app}' failed: {actionDescription}.");
        message.AppendLine(CultureInfo.InvariantCulture, $"The page was at {picture.Url}.");

        if (actionNumber > 1)
        {
            // What already worked narrows the search enormously: it says the flow got this far, so the
            // page was the expected one until exactly here.
            message.AppendLine();
            message.AppendLine("Everything before it worked:");

            foreach (UiSessionEntry entry in picture.Entries)
            {
                message.AppendLine(CultureInfo.InvariantCulture, $"  {entry}");
            }
        }

        if (consoleErrors.Count > 0)
        {
            // The difference between a test looking for the wrong thing and an application that broke. It
            // goes above the diagnosis because it usually replaces it.
            message.AppendLine();
            message.AppendLine(CultureInfo.InvariantCulture,
                $"The application reported {consoleErrors.Count} error(s) while this step ran, so the page may be broken rather than the test:");

            foreach (string error in consoleErrors)
            {
                message.AppendLine(CultureInfo.InvariantCulture, $"  {error}");
            }
        }

        message.AppendLine();
        message.AppendLine(inner.Message);

        message.AppendLine();
        message.AppendLine("A picture of the page, its markup, the session story and the console were recorded with the run. The run's summary lists them under Widgets.");

        return message.ToString().TrimEnd();
    }
}
