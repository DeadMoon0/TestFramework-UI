using System;
using System.Collections.Generic;
using Microsoft.Playwright;
using TestFramework.Core.Exceptions;
using TestFramework.UI.Browser.Runtime;

namespace TestFramework.UI.Browser.Exceptions;

/// <summary>
/// Thrown when the configured browser is not on this machine.
/// </summary>
/// <remarks>
/// A first run on a fresh clone or a fresh pipeline agent hits this, so the message is written for somebody who
/// has not read any documentation yet: it names the three ways forward and puts the one needing no download first.
/// Thrown while the run is planned when the missing build can be told from files, and at the launch otherwise.
/// </remarks>
public sealed class UiBrowserNotInstalledException : TimelineFrameworkException
{
    internal UiBrowserNotInstalledException(string browser, string? channel, Exception inner)
        : base(BuildMessage(browser, channel, needed: null, found: []), [], [], inner)
    {
        this.Browser = browser;
        this.Channel = channel;
    }

    private UiBrowserNotInstalledException(string identifier, string browser, string needed, IReadOnlyList<string> found)
        : base($"The web application '{identifier}' cannot run here, so this run was refused before anything started.\n\n" + BuildMessage(browser, null, needed, found), [])
    {
        this.Browser = browser;
        this.Channel = null;
    }

    /// <summary>
    /// The refusal while a run is planned: the build a launch would look for is not on this machine, and the
    /// application did not ask the run to install it.
    /// </summary>
    /// <param name="identifier">The application.</param>
    /// <param name="browser">The engine.</param>
    /// <param name="check">What was found.</param>
    /// <returns>The exception.</returns>
    internal static UiBrowserNotInstalledException BeforeTheRun(string identifier, string browser, UiBuildCheck check)
        => new(identifier, browser, check.Needed ?? browser, check.Found);

    /// <summary>The browser that could not be started.</summary>
    public string Browser { get; }

    /// <summary>The branded build that was asked for, when one was.</summary>
    public string? Channel { get; }

    /// <summary>
    /// Whether a failed launch failed because the browser is not on this machine.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The file is the answer wherever there is one: Playwright reports the path of its own build, and a
    /// launch that failed while that path holds nothing failed for want of the browser, whatever language
    /// or wording its message used.
    /// </para>
    /// <para>
    /// Playwright offers nothing structural beyond that - no error code, no exception type of its own -
    /// so a branded build, whose path Playwright does not report, and a build present in part (the
    /// headless shell without the full browser, say) are recognised by its English message. That match is
    /// <see cref="MessageSaysBrowserIsMissing"/>, and nowhere else.
    /// </para>
    /// </remarks>
    /// <param name="exception">What the launch threw.</param>
    /// <param name="executableExists">Whether Playwright's own build is on disk, or null when the launch
    /// asked for a branded build, whose location Playwright does not report.</param>
    /// <returns>True when the browser is missing.</returns>
    internal static bool IndicatesMissingBrowser(PlaywrightException exception, bool? executableExists)
        => executableExists == false || MessageSaysBrowserIsMissing(exception.Message);

    /// <summary>
    /// Whether Playwright's message says the browser is missing, for the cases its path cannot answer.
    /// </summary>
    /// <remarks>
    /// The texts are Playwright's own, in English, and are pinned by a test so a Playwright upgrade that
    /// rewords them is noticed there rather than on a fresh clone.
    /// </remarks>
    /// <param name="message">The launch failure's message.</param>
    /// <returns>True when the message is one of Playwright's missing-browser texts.</returns>
    internal static bool MessageSaysBrowserIsMissing(string message)
        => message.Contains("Executable doesn't exist", StringComparison.OrdinalIgnoreCase)
        || message.Contains("looks like Playwright", StringComparison.OrdinalIgnoreCase)
        || message.Contains("please run the following command to download new browsers", StringComparison.OrdinalIgnoreCase);

    private static string BuildMessage(string browser, string? channel, string? needed, IReadOnlyList<string> found)
    {
        string what = channel is { Length: > 0 } ? $"the '{channel}' build of {browser}" : browser;
        string which = needed is null ? string.Empty : $" This version of Playwright launches the build '{needed}'.";
        string stale = found.Count == 0 ? string.Empty
            : $"\n\nThis machine has {string.Join(", ", found)} - another version of Playwright's build. A cached browser folder from before a\nPlaywright upgrade looks like this; key the cache on the Playwright package version.";

        return $"""
            The browser this run needs ({what}) is not available on this machine.{which}{stale}

            Any one of these fixes it:

              1. Use a browser that is already installed - no download at all. In the 'Ui' section of
                 your settings, set "Channel": "msedge" (or "chrome") for this application.

              2. Let the run install it: set "Install": true for this application. The run then installs
                 exactly the build it needs, as a step of its own, before the first browser step.

              3. Install it in the pipeline or from a terminal, in the test project's output folder:
                     pwsh bin/Debug/net8.0/playwright.ps1 install --with-deps {browser}

            The application's Browser and Channel settings decide which browser a run drives; nothing
            picks another one for it.
            """;
    }
}
