using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
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

    // Concurrent, because the fast-path read above the gate is deliberately lock-free: two parallel
    // steps launching different browsers would otherwise read a plain dictionary mid-resize. The gate
    // still serializes the launches; the dictionary's own safety covers the reads.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, IBrowser> Browsers = new(StringComparer.Ordinal);

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
            config.EffectiveBrowser.ToLowerInvariant(),
            config.Channel ?? string.Empty,
            config.EffectiveHeadless,
            config.EffectiveSlowMo.TotalMilliseconds);

        if (Browsers.TryGetValue(key, out IBrowser? cached) && cached.IsConnected)
        {
            return cached;
        }

        await BrowserGate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            if (Browsers.TryGetValue(key, out cached))
            {
                if (cached.IsConnected)
                {
                    return cached;
                }

                // A browser process that died - crashed, killed, closed by a test - stays dead. Kept in the pool,
                // it failed every later run in the process with "browser has been closed"; it is replaced instead.
                Browsers.TryRemove(key, out _);
            }

            IPlaywright driver = await DriverAsync(cancellationToken).ConfigureAwait(false);
            Task<IBrowser> launch = LaunchAsync(driver, config);

            try
            {
                IBrowser browser = await launch.WaitAsync(cancellationToken).ConfigureAwait(false);
                Browsers[key] = browser;
                return browser;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // The step gave up; the launch cannot be stopped half-way, so it is let finish and its browser
                // pooled for the next run - or closed, if another launch got there first - rather than left running
                // with nothing holding it.
                _ = AdoptWhenLaunchedAsync(key, launch);
                throw;
            }
        }
        finally
        {
            BrowserGate.Release();
        }
    }

    private static async Task AdoptWhenLaunchedAsync(string key, Task<IBrowser> launch)
    {
        try
        {
            IBrowser browser = await launch.ConfigureAwait(false);
            if (!Browsers.TryAdd(key, browser))
                await browser.CloseAsync().ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is PlaywrightException or UiBrowserNotInstalledException)
        {
            // A launch nobody waits for any more has no one to report to; the next run's launch will.
        }
    }

    private static async Task<IBrowser> LaunchAsync(IPlaywright driver, WebAppConfig config)
    {
        // The resolver refuses anything else by name, so a branded build reaches here only as a Channel.
        IBrowserType browserType = config.EffectiveBrowser.ToLowerInvariant() switch
        {
            "chromium" => driver.Chromium,
            "firefox" => driver.Firefox,
            "webkit" => driver.Webkit,
            _ => throw new ArgumentException(
                $"Unknown browser '{config.EffectiveBrowser}'. Use 'chromium', 'firefox' or 'webkit', and name a " +
                "branded build such as 'msedge' with 'Channel' instead.",
                nameof(config)),
        };

        BrowserTypeLaunchOptions options = new BrowserTypeLaunchOptions
        {
            Headless = config.EffectiveHeadless,
            Channel = config.Channel,
            SlowMo = config.EffectiveSlowMo > TimeSpan.Zero ? (float)config.EffectiveSlowMo.TotalMilliseconds : null,
        };

        try
        {
            return await browserType.LaunchAsync(options).ConfigureAwait(false);
        }
        catch (PlaywrightException exception) when (UiBrowserNotInstalledException.IndicatesMissingBrowser(exception, PlaywrightBuildExists(browserType, config)))
        {
            // Playwright's own message explains the download but not the alternatives, and on a Windows
            // machine the best answer is usually to drive the browser that is already there.
            throw new UiBrowserNotInstalledException(config.EffectiveBrowser, config.Channel, exception);
        }
    }

    /// <summary>
    /// Whether the build a launch started is on disk, or null when Playwright does not report its path.
    /// </summary>
    /// <remarks>
    /// Playwright reports the path of its own full build only. A branded build lives elsewhere, and a
    /// headless chromium launch starts a separate headless shell, so for those the path would answer a
    /// question nobody asked - an installation with only the shell would read as missing whatever the
    /// launch actually failed on.
    /// </remarks>
    private static bool? PlaywrightBuildExists(IBrowserType browserType, WebAppConfig config)
    {
        bool reportedPathIsTheLaunchedOne = config.Channel is not { Length: > 0 }
            && !(config.EffectiveHeadless && string.Equals(browserType.Name, "chromium", StringComparison.OrdinalIgnoreCase));

        return reportedPathIsTheLaunchedOne ? File.Exists(browserType.ExecutablePath) : null;
    }
}
