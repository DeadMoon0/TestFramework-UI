using System;
using Microsoft.Playwright;

using TestFramework.Core.Exceptions;
namespace TestFramework.UI.Browser.Exceptions;

/// <summary>
/// Thrown when the configured browser is not on this machine.
/// </summary>
/// <remarks>
/// A first run on a fresh clone hits this, so the message is written for somebody who has not read any
/// documentation yet: it names the three ways forward and puts the one needing no download first.
/// </remarks>
public sealed class UiBrowserNotInstalledException : TimelineFrameworkException
{
    internal UiBrowserNotInstalledException(string browser, string? channel, Exception inner)
        : base(BuildMessage(browser, channel), [], [], inner)
    {
        this.Browser = browser;
        this.Channel = channel;
    }

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

    private static string BuildMessage(string browser, string? channel)
    {
        string what = channel is { Length: > 0 } ? $"the '{channel}' build of {browser}" : browser;

        return $"""
            The browser this run needs ({what}) is not available on this machine.

            Any one of these fixes it:

              1. Use a browser that is already installed - no download at all. In the 'Ui' section of
                 your settings, set "Channel": "msedge" (or "chrome") for this application.

              2. Install the browsers once from a test fixture:
                     BrowserExt.Tooling.InstallBrowsers("chromium");

              3. Install them from a terminal, in the test project's output folder:
                     pwsh bin/Debug/net8.0/playwright.ps1 install chromium

            The application's Browser and Channel settings decide which browser a run drives; nothing
            picks another one for it.
            """;
    }
}
