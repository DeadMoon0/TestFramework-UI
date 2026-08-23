using System;
using Microsoft.Playwright;

namespace TestFramework.UI.Browser.Exceptions;

/// <summary>
/// Thrown when the configured browser is not on this machine.
/// </summary>
/// <remarks>
/// A first run on a fresh clone hits this, so the message is written for somebody who has not read any
/// documentation yet: it names the three ways forward and puts the one needing no download first.
/// </remarks>
public sealed class UiBrowserNotInstalledException : Exception
{
    internal UiBrowserNotInstalledException(string browser, string? channel, Exception inner)
        : base(BuildMessage(browser, channel), inner)
    {
        this.Browser = browser;
        this.Channel = channel;
    }

    /// <summary>The browser that could not be started.</summary>
    public string Browser { get; }

    /// <summary>The branded build that was asked for, when one was.</summary>
    public string? Channel { get; }

    internal static bool LooksLikeMissingBrowser(PlaywrightException exception)
        => exception.Message.Contains("Executable doesn't exist", StringComparison.OrdinalIgnoreCase)
        || exception.Message.Contains("looks like Playwright", StringComparison.OrdinalIgnoreCase)
        || exception.Message.Contains("please run the following command to download new browsers", StringComparison.OrdinalIgnoreCase);

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

            Tests that drive a browser are meant to opt in through the TESTFRAMEWORK_UI_BROWSER
            environment variable, so a clone with no browser installed still goes green by skipping them.
            """;
    }
}
