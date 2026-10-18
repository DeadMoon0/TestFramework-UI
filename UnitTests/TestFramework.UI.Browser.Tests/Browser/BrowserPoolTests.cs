using System;
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
        // Every parallel step waits behind a launch at the gate; a step whose deadline has passed no longer holds
        // them up, and the launch it let go of is not leaked. Told by the cancellation itself, not by a stopwatch:
        // how long a launch takes is the machine's business, and a bound on it is a timing lead waiting to happen.
        WebAppConfig config = Config(11);
        using CancellationTokenSource deadline = new();
        await deadline.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => PlaywrightHost.BrowserAsync(config, deadline.Token));

        IBrowser browser = await PlaywrightHost.BrowserAsync(config, CancellationToken.None);
        Assert.True(browser.IsConnected);
        await browser.CloseAsync();
    }
}
