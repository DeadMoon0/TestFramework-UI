using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using TestFramework.UI.Browser.Configuration;
using TestFramework.UI.SampleWebApp;
using Xunit;

namespace TestFramework.UI.Browser.Tests.Shared;

/// <summary>
/// Runs the sample application for the browser suite, once for all of it.
/// </summary>
/// <remarks>
/// A real host on a port the operating system picks, driven over a real socket - the same choice the HTTP
/// package's own suite makes, and for the same reason: the thing under test is what a browser really does
/// with a real server, which an in-memory host would not exercise.
/// </remarks>
public sealed class SampleAppFixture : IAsyncLifetime
{
    private WebApplication? app;

    /// <summary>Where the application is reachable.</summary>
    public string BaseUrl { get; private set; } = string.Empty;

    /// <summary>Where the Angular application is served.</summary>
    public string AppUrl => $"{this.BaseUrl}app/";

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        if (UiTestEnvironmentGate.SkipReason() is not null)
        {
            // Nothing to start: every test in the collection is going to skip anyway, and starting a host
            // for them would only slow a run down on a machine that cannot use it.
            return;
        }

        if (UiTestEnvironmentGate.MayInstallBrowsers)
        {
            BrowserExt.Tooling.InstallBrowsers("chromium");
        }

        // The port is the operating system's choice so parallel suites never collide, and the Angular path
        // is passed in because the host is running inside the test project's output folder.
        this.app = Program.CreateApp(
        [
            "--urls", "http://127.0.0.1:0",
            $"--{Program.AngularRootSetting}={UiTestEnvironmentGate.AngularOutput}",
        ]);

        await this.app.StartAsync().ConfigureAwait(false);

        this.BaseUrl = this.app.Urls.First().Replace("127.0.0.1", "localhost", StringComparison.Ordinal).TrimEnd('/') + "/";

        // Confirm it really is up before a browser is pointed at it, so a failing test means the framework
        // and never the fixture.
        using HttpClient client = new HttpClient();
        using HttpResponseMessage response = await client.GetAsync(new Uri($"{this.BaseUrl}health")).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();
    }

    /// <inheritdoc />
    public async Task DisposeAsync()
    {
        if (this.app is { } running)
        {
            await running.StopAsync().ConfigureAwait(false);
            await running.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// The services a timeline run needs: the sample application configured under the names the tests use.
    /// </summary>
    /// <param name="device">The device to emulate, or null for the default desktop.</param>
    /// <returns>The service provider.</returns>
    public IServiceProvider Services(string? device = null)
    {
        WebAppConfig shop = new WebAppConfig
        {
            BaseUrl = this.AppUrl,
            Browser = BrowserFor(UiTestEnvironmentGate.RequestedBrowser),
            Channel = ChannelFor(UiTestEnvironmentGate.RequestedBrowser),
            Headless = true,
            Device = device ?? "Desktop 1080p",
        };

        ServiceCollection services = new ServiceCollection();

        services.AddSingleton(new UiConfigStore(new Dictionary<string, WebAppConfig>(StringComparer.OrdinalIgnoreCase)
        {
            ["shop"] = shop,

            // The same application as a phone sees it, expressed as one line of inheritance rather than a
            // second copy of everything above.
            ["shop-mobile"] = new WebAppConfig { BasedOn = "shop", Device = "Narrow" },

            // Deliberately lenient, for the test that shows the dial exists.
            ["shop-lenient"] = new WebAppConfig
            {
                BasedOn = "shop",
                AmbiguityMode = TestFramework.UI.Browser.Resolution.UiAmbiguityMode.FirstMatch,
            },
        }));

        return services.BuildServiceProvider();
    }

    private static string BrowserFor(string? requested)
        => requested?.ToLowerInvariant() switch
        {
            "firefox" => "firefox",
            "webkit" or "safari" => "webkit",
            _ => "chromium",
        };

    private static string? ChannelFor(string? requested)
        => requested?.ToLowerInvariant() switch
        {
            // A branded build already on the machine, so nothing has to be downloaded.
            "msedge" or "edge" => "msedge",
            "chrome" => "chrome",
            _ => null,
        };
}

/// <summary>
/// The collection every browser test belongs to, so one host serves them all.
/// </summary>
[CollectionDefinition(Name)]
public sealed class SampleAppCollection : ICollectionFixture<SampleAppFixture>
{
    /// <summary>The collection's name.</summary>
    public const string Name = "SampleApp";
}
