using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Timelines;
using TestFramework.UI.Browser.Tests.Shared;
using Xunit.Abstractions;

namespace TestFramework.UI.Browser.Tests.Browser;

/// <summary>
/// The retry rule: a browser flow may only retry when every attempt starts from a known page.
/// </summary>
[Collection(SampleAppCollection.Name)]
public class RetryGuardTests(SampleAppFixture fixture, ITestOutputHelper output)
{
    [Fact]
    public async Task ARetryOnAFlowThatDoesNotNavigateFirstIsRefusedBeforeAnyBrowserExists()
    {
        // A retried attempt replays its actions against whatever the failed one left behind - a second
        // "Place order" places a second order. That must be refused at plan time: no configuration, no
        // session, no browser - which is why this test needs no [BrowserFact] gate at all.
        Timeline timeline = Timeline.Create()
            .Trigger(BrowserExt.Session("shop").Click("Place order"))
                .WithRetry(2, CalcDelays.None).Name("dangerous")
            .Build();

        InvalidOperationException failure = await Assert.ThrowsAsync<InvalidOperationException>(
            () => timeline.SetupRun(new ServiceCollection().BuildServiceProvider(), output).RunAsync());

        Assert.Contains("WithRetry", failure.Message, StringComparison.Ordinal);
        Assert.Contains("starts with Click", failure.Message, StringComparison.Ordinal);
        Assert.Contains("first action is Navigate", failure.Message, StringComparison.Ordinal);

        output.WriteLine(failure.Message);
    }

    [BrowserFact]
    public async Task AFlowThatNavigatesFirstMayRetry()
    {
        // The sanctioned shape: every attempt begins at a known page, so replaying is harmless.
        Timeline timeline = Timeline.Create()
            .Trigger(BrowserExt.Session("shop")
                .Navigate("/products")
                .Expect("Anvil"))
                .WithRetry(2, CalcDelays.None).Name("safe")
            .Build();

        TimelineRun run = await timeline.SetupRun(fixture.Services(), output).RunAsync();

        run.EnsureRanToCompletion();
    }
}
