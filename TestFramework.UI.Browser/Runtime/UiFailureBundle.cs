using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Playwright;
using Newtonsoft.Json;
using TestFramework.Core.Logging;
using TestFramework.UI.Session;

namespace TestFramework.UI.Browser.Runtime;

/// <summary>
/// Everything a person needs to understand a browser failure, written into one folder.
/// </summary>
/// <remarks>
/// <para>
/// A failure on a build server is only as useful as what it left behind. This writes the picture, the
/// markup, the story of the session and the page's own complaints - enough to attach to a ticket and
/// enough to tell "the test looked for the wrong thing" apart from "the application was broken".
/// </para>
/// <para>
/// Where it goes is a separate question from writing it, and deliberately so. The step that failed names
/// the folder in its own message, because that is the line a reader actually sees; the observer the engine
/// drives is what puts the evidence there. One convention, asked twice, so the two can never disagree
/// about the path.
/// </para>
/// <para>
/// Every part of it is best effort. A failing screenshot must never replace the failure it was meant to
/// explain, so nothing in here is allowed to throw.
/// </para>
/// </remarks>
internal static class UiFailureBundle
{
    /// <summary>
    /// Where the evidence of one failure belongs.
    /// </summary>
    /// <remarks>
    /// Derived rather than chosen, so the step naming the folder and the observer filling it arrive at the
    /// same string. The application is in the name because the evidence is of that application's page and a
    /// run may have two open; the attempt is in it only from the second one on, so a retry cannot
    /// photograph over the failure that is usually the interesting one.
    /// </remarks>
    /// <param name="runState">The run, for the folder it writes into.</param>
    /// <param name="stepLabel">The step that failed.</param>
    /// <param name="app">The application whose page this is.</param>
    /// <param name="attempt">Which attempt at the step, counting from one.</param>
    /// <returns>The folder path, which may not exist yet.</returns>
    public static string DirectoryFor(UiRunState runState, string stepLabel, string app, int attempt)
    {
        ArgumentNullException.ThrowIfNull(runState);

        string name = $"failure-{UiRunPaths.SafeName(stepLabel, 40)}-{UiRunPaths.SafeName(app, 24)}";

        return Path.Combine(runState.RunDirectory, attempt > 1 ? $"{name}-attempt{attempt.ToString(CultureInfo.InvariantCulture)}" : name);
    }

    /// <summary>
    /// Collects the evidence of a failure.
    /// </summary>
    /// <param name="session">The session that failed.</param>
    /// <param name="directory">Where to write it, from <see cref="DirectoryFor"/>.</param>
    /// <param name="picture">The session so far, including the actions that did succeed.</param>
    /// <param name="logger">Where to mention what was written, or why it could not be.</param>
    /// <returns>True when the folder was written.</returns>
    public static async Task<bool> CaptureAsync(
        UiSession session,
        string directory,
        UiSessionPicture picture,
        ScopedLogger logger)
    {
        try
        {
            Directory.CreateDirectory(directory);

            await TryWriteScreenshotAsync(session, directory).ConfigureAwait(false);
            await TryWriteMarkupAsync(session, directory).ConfigureAwait(false);
            await TryWritePictureAsync(picture, directory).ConfigureAwait(false);
            await TryWriteConsoleAsync(picture, directory).ConfigureAwait(false);

            logger?.LogInformation("UI failure evidence written to {0}", directory);

            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or PlaywrightException)
        {
            // Said out loud, because the failure that caused this names the folder in its own message and a
            // reader opening an absent one deserves to find out why here.
            logger?.LogWarning("Could not write the UI failure evidence to {0}: {1}", directory, exception.Message);

            return false;
        }
    }

    /// <summary>
    /// Photographs the page into the run's folder.
    /// </summary>
    /// <param name="session">The session to photograph.</param>
    /// <param name="runState">The run, for where to write.</param>
    /// <param name="name">What the screenshot is of.</param>
    /// <returns>The file's path, or null when it could not be taken.</returns>
    public static async Task<string?> ScreenshotAsync(UiSession session, UiRunState runState, string name)
    {
        try
        {
            Directory.CreateDirectory(runState.RunDirectory);

            string path = Path.Combine(runState.RunDirectory, runState.NextScreenshotFileName(name));

            await session.Page.ScreenshotAsync(new PageScreenshotOptions { Path = path, FullPage = true }).ConfigureAwait(false);

            return path;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or PlaywrightException)
        {
            return null;
        }
    }

    private static async Task TryWriteScreenshotAsync(UiSession session, string directory)
    {
        try
        {
            await session.Page
                .ScreenshotAsync(new PageScreenshotOptions
                {
                    Path = Path.Combine(directory, "screenshot.png"),
                    FullPage = true,
                })
                .ConfigureAwait(false);
        }
        catch (PlaywrightException)
        {
            // A page that has already gone cannot be photographed, and that is not worth failing over.
        }
    }

    private static async Task TryWriteMarkupAsync(UiSession session, string directory)
    {
        try
        {
            string html = await session.Page.ContentAsync().ConfigureAwait(false);

            await File.WriteAllTextAsync(Path.Combine(directory, "page.html"), html).ConfigureAwait(false);
        }
        catch (PlaywrightException)
        {
        }
    }

    private static async Task TryWritePictureAsync(UiSessionPicture picture, string directory)
    {
        string json = JsonConvert.SerializeObject(picture, Formatting.Indented);

        await File.WriteAllTextAsync(Path.Combine(directory, "session-picture.json"), json).ConfigureAwait(false);
        await File.WriteAllTextAsync(Path.Combine(directory, "session.txt"), picture.ToString()).ConfigureAwait(false);
    }

    private static async Task TryWriteConsoleAsync(UiSessionPicture picture, string directory)
    {
        IReadOnlyList<string> errors = picture.ConsoleErrors();

        if (errors.Count == 0)
        {
            return;
        }

        StringBuilder builder = new StringBuilder();

        foreach (string error in errors)
        {
            builder.AppendLine(CultureInfo.InvariantCulture, $"{error}");
        }

        await File.WriteAllTextAsync(Path.Combine(directory, "console.log"), builder.ToString()).ConfigureAwait(false);
    }
}
