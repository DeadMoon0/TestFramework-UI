using System;
using System.Threading.Tasks;
using Microsoft.Playwright;
using TestFramework.UI.Browser.Tests.Shared;
using Xunit;
using Xunit.Abstractions;

namespace TestFramework.UI.Browser.Tests.Browser;

/// <summary>
/// Narrow checks that tell a broken machine apart from a broken framework.
/// </summary>
/// <remarks>
/// When the browser suite goes wrong, the first question is always which layer failed: the sample host,
/// the Playwright driver, the browser itself, or this package. These answer that in order, so the answer
/// is never guessed at.
/// </remarks>
[Collection(SampleAppCollection.Name)]
public class DiagnosticTests(SampleAppFixture fixture, ITestOutputHelper output)
{
    [BrowserFact]
    public void TheSampleApplicationIsServed()
    {
        output.WriteLine($"base url: {fixture.BaseUrl}");
        output.WriteLine($"app url: {fixture.AppUrl}");
        output.WriteLine($"angular output: {UiTestEnvironmentGate.AngularOutput}");

        Assert.NotEmpty(fixture.BaseUrl);
    }

    [BrowserFact]
    public async Task TheDriverStartsAndTheBrowserOpensThePage()
    {
        using IPlaywright playwright = await Microsoft.Playwright.Playwright.CreateAsync();

        output.WriteLine("driver started");

        await using IBrowser browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,
            Channel = UiTestEnvironmentGate.RequestedBrowser is "msedge" or "edge" ? "msedge" : null,
        });

        output.WriteLine($"browser started: {browser.Version}");

        IPage page = await browser.NewPageAsync();
        await page.GotoAsync(fixture.AppUrl + "products");

        string title = await page.TitleAsync();
        string heading = await page.GetByRole(AriaRole.Heading, new PageGetByRoleOptions { Name = "Products" }).InnerTextAsync();

        output.WriteLine($"title: {title}, heading: {heading}");

        Assert.Equal("Products", heading);
    }
}
