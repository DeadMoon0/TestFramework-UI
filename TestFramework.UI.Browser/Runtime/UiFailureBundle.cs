using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Playwright;
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
/// Every part of it is best effort. A failing screenshot must never replace the failure it was meant to
/// explain, so nothing in here is allowed to throw.
/// </para>
/// </remarks>
internal static class UiFailureBundle
{
    /// <summary>
    /// Collects the evidence of a failure.
    /// </summary>
    /// <param name="session">The session that failed.</param>
    /// <param name="runState">The run, for where to write.</param>
    /// <param name="stepLabel">The step that failed, used to name the folder.</param>
    /// <param name="picture">The session so far, including the actions that did succeed.</param>
    /// <param name="logger">Where to mention what was written, or why it could not be.</param>
    /// <returns>The folder, or null when nothing could be written.</returns>
    public static async Task<string?> CaptureAsync(
        UiSession session,
        UiRunState runState,
        string stepLabel,
        UiSessionPicture picture,
        ScopedLogger logger)
    {
        try
        {
            string directory = Path.Combine(runState.RunDirectory, $"failure-{UiRunPaths.SafeName(stepLabel, 40)}");
            Directory.CreateDirectory(directory);

            await TryWriteScreenshotAsync(session, directory).ConfigureAwait(false);
            await TryWriteMarkupAsync(session, directory).ConfigureAwait(false);
            await TryWritePictureAsync(picture, directory).ConfigureAwait(false);
            await TryWriteConsoleAsync(picture, directory).ConfigureAwait(false);

            logger?.LogInformation("UI failure evidence written to {0}", directory);

            return directory;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or PlaywrightException)
        {
            logger?.LogWarning("Could not write the UI failure evidence: {0}", exception.Message);

            return null;
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
        string json = JsonSerializer.Serialize(picture, new JsonSerializerOptions { WriteIndented = true });

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
