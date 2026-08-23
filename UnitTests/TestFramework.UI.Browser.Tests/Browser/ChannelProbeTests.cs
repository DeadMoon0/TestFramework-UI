using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Playwright;
using TestFramework.UI.Browser.Resolution;
using TestFramework.UI.Browser.Targeting;
using TestFramework.UI.Browser.Tests.Shared;
using Xunit;
using Xunit.Abstractions;

namespace TestFramework.UI.Browser.Tests.Browser;

/// <summary>
/// What each channel actually finds on a real page.
/// </summary>
/// <remarks>
/// The ladder's whole design rests on assumptions about how a browser answers: that a label is found
/// exactly when the markup names it exactly, that a field with only a placeholder is found there and
/// nowhere stronger, that an aria-label counts as a label. Those are assumptions about Playwright and
/// about a real rendered application, and the only honest way to hold them is to measure them - the
/// alternative is a matching bug that survives because everybody reasoned about it correctly.
/// </remarks>
[Collection(SampleAppCollection.Name)]
public class ChannelProbeTests(SampleAppFixture fixture, ITestOutputHelper output)
{
    [BrowserFact]
    public async Task TheChannelsAnswerAsTheLadderAssumes()
    {
        using IPlaywright playwright = await Microsoft.Playwright.Playwright.CreateAsync();

        await using IBrowser browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,
            Channel = UiTestEnvironmentGate.RequestedBrowser is "msedge" or "edge" ? "msedge" : null,
        });

        IPage page = await browser.NewPageAsync();
        await page.GotoAsync(fixture.AppUrl + "checkout");
        await page.GetByRole(AriaRole.Heading, new PageGetByRoleOptions { Name = "Checkout" }).WaitForAsync();

        PlaywrightElementQuery query = new PlaywrightElementQuery(page, "data-testid", TimeSpan.FromSeconds(2));

        Dictionary<(string Name, UiMatchChannel Channel, bool Exact), int> counts = new();

        foreach (string name in new[] { "Email", "Street", "City" })
        {
            foreach (bool exact in new[] { true, false })
            {
                foreach (UiMatchChannel channel in new[] { UiMatchChannel.Label, UiMatchChannel.Placeholder })
                {
                    UiQuerySpec spec = new UiQuerySpec(channel, name, exact);
                    int count = await query.CountAsync(spec, CancellationToken.None);

                    counts[(name, channel, exact)] = count;
                    output.WriteLine($"{name,-6} {channel,-12} exact={exact,-5} -> {count}");
                }
            }
        }

        // A field with a real label is found there, exactly. If this ever stops holding, every test that
        // fills a labelled field silently starts matching loosely instead.
        Assert.Equal(1, counts[("Email", UiMatchChannel.Label, true)]);

        // An aria-label is a label as far as a person using the page is concerned, and so it must be here.
        Assert.Equal(1, counts[("City", UiMatchChannel.Label, true)]);

        // A field named only by its placeholder is found on the placeholder channel and on no stronger
        // one - which is why matching there exactly must not count as a guess.
        Assert.Equal(0, counts[("Street", UiMatchChannel.Label, true)]);
        Assert.Equal(0, counts[("Street", UiMatchChannel.Label, false)]);
        Assert.Equal(1, counts[("Street", UiMatchChannel.Placeholder, true)]);
    }
}
