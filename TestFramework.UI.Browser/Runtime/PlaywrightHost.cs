using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Playwright;
using TestFramework.UI.Browser.Configuration;
using TestFramework.UI.Browser.Exceptions;

namespace TestFramework.UI.Browser.Runtime;

/// <summary>
/// Owns the browsers for the whole test process.
/// </summary>
/// <remarks>
/// <para>
/// Starting a browser costs the better part of a second; a browser context costs milliseconds. So
/// browsers are pooled for the life of the process and every run gets a fresh context out of one - the
/// same trade the HTTP packages make with pooled clients, for the same reason, and with the same
/// consequence: isolation between runs is the context's job, not the browser's.
/// </para>
/// <para>
/// Nothing disposes the pool. A test host exits and takes the browser processes with it, and a pool
/// that closed a browser when one run finished would make the next run pay for it again.
/// </para>
/// </remarks>
internal static class PlaywrightHost
{
    private static readonly SemaphoreSlim BrowserGate = new SemaphoreSlim(1, 1);
    private static readonly Dictionary<string, IBrowser> Browsers = new Dictionary<string, IBrowser>(StringComparer.Ordinal);

    /// <summary>
    /// Started once per process, and deliberately not behind the same gate the browsers use.
    /// </summary>
    /// <remarks>
    /// A semaphore is not reentrant, so guarding both the driver and the browsers with one would deadlock
    /// the moment a browser launch needed the driver - which is every first launch. A lazily awaited task
    /// gives the same "exactly once" guarantee without anything to acquire.
    /// </remarks>
    private static readonly Lazy<Task<IPlaywright>> Driver = new Lazy<Task<IPlaywright>>(
        static () => Microsoft.Playwright.Playwright.CreateAsync(),
        LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>
    /// The Playwright driver, started on first use.
    /// </summary>
    /// <param name="cancellationToken">Cancels the wait for the driver, not the driver's own start.</param>
    /// <returns>The driver.</returns>
    public static async Task<IPlaywright> DriverAsync(CancellationToken cancellationToken)
        => await Driver.Value.WaitAsync(cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// A browser matching the configuration, launched on first use and shared afterwards.
    /// </summary>
    /// <param name="config">The configuration deciding which browser and how it is launched.</param>
    /// <param name="cancellationToken">Cancels the launch.</param>
    /// <returns>The browser.</returns>
    /// <exception cref="UiBrowserNotInstalledException">The browser is not on this machine.</exception>
    public static async Task<IBrowser> BrowserAsync(WebAppConfig config, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(config);

        // Only what actually changes a browser process belongs in the key. Anything a context can carry -
        // viewport, locale, device - must not, or every device variant would start its own browser.
        string key = string.Format(
            CultureInfo.InvariantCulture,
            "{0}|{1}|{2}|{3}",
            config.StatedBrowser.ToLowerInvariant(),
            config.Channel ?? string.Empty,
            config.Headless,
            config.SlowMo.TotalMilliseconds);

        if (Browsers.TryGetValue(key, out IBrowser? cached))
        {
            return cached;
        }

        await BrowserGate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            if (Browsers.TryGetValue(key, out cached))
            {
                return cached;
            }

            IPlaywright driver = await DriverAsync(cancellationToken).ConfigureAwait(false);
            IBrowser browser = await LaunchAsync(driver, config).ConfigureAwait(false);

            Browsers[key] = browser;

            return browser;
        }
        finally
        {
            BrowserGate.Release();
        }
    }

    private static async Task<IBrowser> LaunchAsync(IPlaywright driver, WebAppConfig config)
    {
        IBrowserType browserType = config.StatedBrowser.ToLowerInvariant() switch
        {
            "chromium" or "chrome" or "edge" or "msedge" => driver.Chromium,
            "firefox" => driver.Firefox,
            "webkit" or "safari" => driver.Webkit,
            _ => throw new ArgumentException(
                $"Unknown browser '{config.StatedBrowser}'. Use 'chromium', 'firefox' or 'webkit', and name a " +
                "branded build such as 'msedge' with 'Channel' instead.",
                nameof(config)),
        };

        BrowserTypeLaunchOptions options = new BrowserTypeLaunchOptions
        {
            Headless = config.Headless,
            Channel = config.Channel,
            SlowMo = config.SlowMo > TimeSpan.Zero ? (float)config.SlowMo.TotalMilliseconds : null,
        };

        try
        {
            return await browserType.LaunchAsync(options).ConfigureAwait(false);
        }
        catch (PlaywrightException exception) when (UiBrowserNotInstalledException.LooksLikeMissingBrowser(exception))
        {
            // Playwright's own message explains the download but not the alternatives, and on a Windows
            // machine the best answer is usually to drive the browser that is already there.
            throw new UiBrowserNotInstalledException(config.StatedBrowser, config.Channel, exception);
        }
    }
}
