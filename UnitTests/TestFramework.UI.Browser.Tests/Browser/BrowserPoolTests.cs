using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Playwright;
using TestFramework.UI.Browser.Configuration;
using TestFramework.UI.Browser.Runtime;
using TestFramework.UI.Browser.Tests.Shared;
using Xunit;

namespace TestFramework.UI.Browser.Tests.Browser;

/// <summary>
/// The process-wide browser pool: a dead browser is replaced, and a launch answers to the step's deadline.
/// </summary>
/// <remarks>
/// Each test uses its own slow-motion value, which is part of the pool key, so it gets a browser no other test shares
/// - closing one here must not take a parallel test's browser with it.
/// </remarks>
public sealed class BrowserPoolTests
{
    private static WebAppConfig Config(int uniqueSlowMoMs)
    {
        UiAvailableBrowser browser = UiTestEnvironmentGate.Browser!;
        return new WebAppConfig { Browser = browser.Browser, Channel = browser.Channel, SlowMo = TimeSpan.FromMilliseconds(uniqueSlowMoMs) };
    }

    [BrowserFact]
    public async Task ABrowserThatDied_IsReplacedRatherThanHandedToTheNextRun()
    {
        // Kept in the pool, a dead browser failed every later run in the process with "browser has been closed".
        WebAppConfig config = Config(7);
        IBrowser first = await PlaywrightHost.BrowserAsync(config, CancellationToken.None);
        await first.CloseAsync();

        IBrowser second = await PlaywrightHost.BrowserAsync(config, CancellationToken.None);

        Assert.NotSame(first, second);
        Assert.True(second.IsConnected);
        await second.CloseAsync();
    }

    [BrowserFact]
    public async Task ALaunchStopsWaitingWhenTheStepDoes()
    {
        // A launch takes the better part of a second, and every parallel step waits behind it at the gate; a step
        // whose deadline has passed no longer holds them up.
        WebAppConfig config = Config(11);

        // The driver started, so what the deadline meets is the launch itself rather than the driver's start.
        await PlaywrightHost.DriverAsync(CancellationToken.None);

        // A full launch on this machine, right now, is the yardstick: a loaded machine slows both alike, where a
        // fixed bound would read a slow machine as a step that waited.
        Stopwatch full = Stopwatch.StartNew();
        IBrowser measured = await PlaywrightHost.BrowserAsync(Config(13), CancellationToken.None);
        full.Stop();
        await measured.CloseAsync();

        using CancellationTokenSource deadline = new(TimeSpan.FromMilliseconds(50));
        Stopwatch elapsed = Stopwatch.StartNew();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => PlaywrightHost.BrowserAsync(config, deadline.Token));

        Assert.True(elapsed.Elapsed < full.Elapsed / 2, $"the launch held the step for {elapsed.Elapsed}; a full launch took {full.Elapsed}");

        // And the launch it let go of was not leaked: the pool still hands out a working browser.
        IBrowser browser = await PlaywrightHost.BrowserAsync(config, CancellationToken.None);
        Assert.True(browser.IsConnected);
        await browser.CloseAsync();
    }
}
