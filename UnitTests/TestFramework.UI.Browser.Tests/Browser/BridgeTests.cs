using System;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using TestFramework.Core.Timelines;
using TestFramework.UI.Browser.Identifier;
using TestFramework.UI.Browser.Tests.Shared;
using TestFramework.UI.Web;
using TestFramework.Web;
using TestFramework.Web.Configuration;
using TestFramework.Web.Site;
using Xunit.Abstractions;

namespace TestFramework.UI.Browser.Tests.Browser;

/// <summary>
/// The bridge to the TestFramework.Web family, end to end: a browser driving an application whose
/// address only the Web family's configuration knows.
/// </summary>
/// <remarks>
/// The <c>shop-bridged</c> entry deliberately has no address of its own. Where its address comes from -
/// a site entry under its own name, or an API entry it was pointed at - is exactly what these tests
/// prove, against the same stores a container environment publishes into.
/// </remarks>
[Collection(SampleAppCollection.Name)]
public class BridgeTests(SampleAppFixture fixture, ITestOutputHelper output)
{
    [BrowserFact]
    public async Task ASiteEntryUnderTheApplicationsOwnNameIsItsAddress()
    {
        // The shape a container environment produces: it starts the site, then publishes the address
        // into the site store under the site's name. Nothing on the identifier, nothing bridged by hand.
        IServiceProvider services = fixture.Services(customize: registrations =>
        {
            WebConfigStore<SiteConfig> sites = new();
            sites.AddConfig("shop-bridged", new SiteConfig { BaseUrl = fixture.AppUrl });
            registrations.AddSingleton(sites);

            registrations.AddUiWebBridge();
        });

        Timeline timeline = Timeline.Create()
            .Trigger(BrowserExt.Session("shop-bridged")
                .Navigate("/products")
                .Expect("Anvil"))
                .Name("browse")
            .Build();

        TimelineRun run = await timeline.SetupRun(services, output).RunAsync();

        run.EnsureRanToCompletion();
        run.UiUrl("shop-bridged").Should().Contain("/app/products");
    }

    [BrowserFact]
    public async Task AnApplicationServedByItsApiIsReachedThroughTheApisName()
    {
        // The API process serves the application itself, so the API's address IS the application's -
        // said once, on the identifier, with FromWebApi.
        IServiceProvider services = fixture.Services(customize: registrations =>
        {
            WebConfigStore<ApiConfig> apis = new();
            apis.AddConfig("shop-api", new ApiConfig { BaseUrl = fixture.BaseUrl });
            registrations.AddSingleton(apis);

            registrations.AddUiWebBridge();
        });

        WebAppIdentifier app = new WebAppIdentifier("shop-bridged").FromWebApi("shop-api");

        Timeline timeline = Timeline.Create()
            .Trigger(BrowserExt.Session(app)
                .Navigate("/app/products")
                .Expect("Anvil"))
                .Name("browse")
            .Build();

        TimelineRun run = await timeline.SetupRun(services, output).RunAsync();

        run.EnsureRanToCompletion();
    }

    [BrowserFact]
    public async Task OneNameServesApiStepsAndBrowserStepsInOneTimeline()
    {
        // The point of the whole bridge: the API steps and the browser steps name the same resource,
        // configured once - so an environment that provisions it once serves both doors.
        IServiceProvider services = fixture.Services(customize: registrations =>
        {
            WebConfigStore<ApiConfig> apis = new();
            apis.AddConfig("shop-api", new ApiConfig { BaseUrl = fixture.BaseUrl });
            registrations.AddSingleton(apis);

            registrations.AddUiWebBridge();
        });

        WebAppIdentifier app = new WebAppIdentifier("shop-bridged").FromWebApi("shop-api");

        Timeline timeline = Timeline.Create()
            // The back door: what the API says the orders are.
            .Trigger(WebExt.Api.Http("shop-api").Get("api/orders").Call()).Name("api-orders")

            // The front door: what a person sees of the same orders.
            .Trigger(BrowserExt.Session(app)
                .Navigate("/app/orders")
                .Expect("Anvil"))
                .Name("browser-orders")
            .Build();

        TimelineRun run = await timeline.SetupRun(services, output).RunAsync();

        run.EnsureRanToCompletion();

        run.ApiStatus("api-orders").Should().Be(HttpStatusCode.OK);
        run.ApiBody("api-orders").Should().Contain("Anvil");
    }
}
