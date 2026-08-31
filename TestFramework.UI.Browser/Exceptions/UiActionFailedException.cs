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
/// worked, whether the page itself was complaining, and where the evidence was written. The inner
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
        string? failureBundlePath,
        Exception inner)
        : base(BuildMessage(app, actionDescription, actionNumber, actionCount, picture, consoleErrors, failureBundlePath, inner), inner)
    {
        this.App = app;
        this.ActionDescription = actionDescription;
        this.ActionNumber = actionNumber;
        this.ActionCount = actionCount;
        this.ConsoleErrors = consoleErrors;

#pragma warning disable CS0618 // Set once, here, so the obsolete member keeps its declared meaning of "nothing".
        this.FailureBundlePath = failureBundlePath;
#pragma warning restore CS0618
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

    /// <summary>
    /// Always null. The evidence is recorded with the run instead.
    /// </summary>
    /// <remarks>
    /// The screenshot, the markup, the session story and the console now go to the run's own widgets,
    /// where the debugging tool draws them, a build publishes them and the run's summary lists them.
    /// There is no folder to name here because there is nothing to name yet: the step throws, and
    /// whoever is watching the run photographs the page afterwards.
    /// </remarks>
    [Obsolete("The evidence is recorded as run widgets; this is always null. Read the run's widgets, or the Widgets section of its summary.")]
    public string? FailureBundlePath { get; }

    private static string BuildMessage(
        string app,
        string actionDescription,
        int actionNumber,
        int actionCount,
        UiSessionPicture picture,
        IReadOnlyList<string> consoleErrors,
        string? failureBundlePath,
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
