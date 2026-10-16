using Microsoft.Playwright;
using TestFramework.UI.Browser.Exceptions;

namespace TestFramework.UI.Browser.Tests.Runtime;

/// <summary>
/// A launch that failed for want of the browser is told apart from one that failed for any other reason,
/// by the file where there is one and by Playwright's wording only where there is not.
/// </summary>
public class MissingBrowserDetectionTests
{
    [Fact]
    public void ALaunchFailingWhileTheBuildIsNotOnDisk_IsAMissingBrowser_WhateverItsMessageSays()
        => Assert.True(UiBrowserNotInstalledException.IndicatesMissingBrowser(
            new PlaywrightException("Programmdatei nicht gefunden."),
            executableExists: false));

    [Fact]
    public void ALaunchFailingWhileTheBuildIsOnDisk_IsNotAMissingBrowser()
        => Assert.False(UiBrowserNotInstalledException.IndicatesMissingBrowser(
            new PlaywrightException("Browser closed unexpectedly: sandbox could not start."),
            executableExists: true));

    [Fact]
    public void ABrandedBuildThatFailedForAnotherReason_IsNotAMissingBrowser()
        => Assert.False(UiBrowserNotInstalledException.IndicatesMissingBrowser(
            new PlaywrightException("Browser closed unexpectedly: sandbox could not start."),
            executableExists: null));

    /// <summary>
    /// Playwright's own missing-browser texts, the only signal for a branded build. A Playwright upgrade
    /// that rewords one fails here.
    /// </summary>
    [Theory]
    [InlineData("Executable doesn't exist at C:\\ms-playwright\\chromium-1234\\chrome-win64\\chrome.exe")]
    [InlineData("Looks like Playwright Test or Playwright was just installed or updated.")]
    [InlineData("Please run the following command to download new browsers: playwright install")]
    public void PlaywrightsMissingBrowserTexts_AreRecognisedWhereTheFileCannotAnswer(string message)
        => Assert.True(UiBrowserNotInstalledException.IndicatesMissingBrowser(new PlaywrightException(message), executableExists: null));
}
