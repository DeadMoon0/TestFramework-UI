using System;
using Microsoft.Extensions.DependencyInjection;
using TestFramework.UI.Browser.Configuration;
using TestFramework.UI.Browser.Exceptions;
using TestFramework.UI.Browser.Identifier;
using TestFramework.Web.Configuration;
using TestFramework.Web.Site;
using Xunit;

namespace TestFramework.UI.Web.Tests;

/// <summary>
/// Covers the whole address resolution through the real bridge sources: the same identifier and the
/// same stores serve a deployed and a containerized run, because the environment is just another
/// writer of the configuration.
/// </summary>
public class BridgeResolutionTests
{
    private static ServiceProvider BuildServices(
        WebAppConfig shopEntry,
        SiteConfig? site = null,
        ApiConfig? api = null,
        string siteIdentifier = "shop",
        string apiIdentifier = "shop")
    {
        ServiceCollection services = new();
        services.AddSingleton(new UiConfigStore([new("shop", shopEntry)]));

        WebConfigStore<SiteConfig> siteStore = new();
        if (site is not null)
            siteStore.AddConfig(siteIdentifier, site);
        services.AddSingleton(siteStore);

        WebConfigStore<ApiConfig> apiStore = new();
        if (api is not null)
            apiStore.AddConfig(apiIdentifier, api);
        services.AddSingleton(apiStore);

        // Registered the way LoadUIWebBridge registers them: site first.
        services.AddSingleton<IUiBaseUrlSource, SiteBaseUrlSource>();
        services.AddSingleton<IUiBaseUrlSource, ApiBaseUrlSource>();

        return services.BuildServiceProvider();
    }

    [Fact]
    public void OwnIdentifier_ResolvesFromTheSiteStore_WithNoBridgingCall()
    {
        using ServiceProvider services = BuildServices(new WebAppConfig { Browser = "chromium" }, site: new SiteConfig { BaseUrl = "http://localhost:39001/" });

        WebAppConfig resolved = UiConfigResolver.Resolve(services, new WebAppIdentifier("shop"));

        Assert.Equal("http://localhost:39001/", resolved.BaseUrl);
    }

    [Fact]
    public void AnExplicitBaseUrl_AlwaysWins()
    {
        using ServiceProvider services = BuildServices(
            new WebAppConfig { Browser = "chromium", BaseUrl = "https://deployed.example/" },
            site: new SiteConfig { BaseUrl = "http://localhost:39001/" });

        WebAppConfig resolved = UiConfigResolver.Resolve(services, new WebAppIdentifier("shop"));

        Assert.Equal("https://deployed.example/", resolved.BaseUrl);
    }

    [Fact]
    public void AnIdentifierInBothStores_ResolvesByTheDeclaredKind()
    {
        using ServiceProvider services = BuildServices(
            new WebAppConfig { Browser = "chromium" },
            site: new SiteConfig { BaseUrl = "http://site/" },
            api: new ApiConfig { BaseUrl = "http://api/" });

        WebAppConfig viaSite = UiConfigResolver.Resolve(services, new WebAppIdentifier("shop").FromSite("shop"));
        WebAppConfig viaApi = UiConfigResolver.Resolve(services, new WebAppIdentifier("shop").FromWebApi("shop"));

        Assert.Equal("http://site/", viaSite.BaseUrl);
        Assert.Equal("http://api/", viaApi.BaseUrl);
    }

    [Fact]
    public void BaseUrlFromSite_ResolvesADifferentlyNamedSite()
    {
        using ServiceProvider services = BuildServices(
            new WebAppConfig { Browser = "chromium", BaseUrlFromSite = "shop-ui" },
            site: new SiteConfig { BaseUrl = "http://localhost:39001/" },
            siteIdentifier: "shop-ui");

        WebAppConfig resolved = UiConfigResolver.Resolve(services, new WebAppIdentifier("shop"));

        Assert.Equal("http://localhost:39001/", resolved.BaseUrl);
    }

    [Fact]
    public void BaseUrlFromApi_StillMeansTheApiStore()
    {
        using ServiceProvider services = BuildServices(
            new WebAppConfig { Browser = "chromium", BaseUrlFromApi = "shop-api" },
            api: new ApiConfig { BaseUrl = "http://api/" },
            apiIdentifier: "shop-api");

        WebAppConfig resolved = UiConfigResolver.Resolve(services, new WebAppIdentifier("shop"));

        Assert.Equal("http://api/", resolved.BaseUrl);
    }

    [Fact]
    public void NothingAnswering_FailsWithTheAddressError()
    {
        using ServiceProvider services = BuildServices(new WebAppConfig { Browser = "chromium" });

        UiConfigurationException exception = Assert.Throws<UiConfigurationException>(
            () => UiConfigResolver.Resolve(services, new WebAppIdentifier("shop")));

        Assert.Contains("has no address", exception.Message, StringComparison.Ordinal);
    }
}
