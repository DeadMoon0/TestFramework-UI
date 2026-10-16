using System;
using System.Collections.Generic;
using System.Linq;
using TestFramework.Core.Environment;
using TestFramework.Core.Environment.Graph;
using TestFramework.UI.Browser.Configuration;
using TestFramework.UI.Browser.Exceptions;
using TestFramework.UI.Browser.Identifier;
using TestFramework.Web;
using Xunit;

namespace TestFramework.UI.Browser.Tests.Configuration;

/// <summary>
/// A browser step requires its application and the one resource its address comes from, decided before the
/// run starts - so the environment that declared that resource is the one that starts it, and a second
/// address for one name is refused rather than settled by whichever the resolver asked first.
/// </summary>
public class UiRequirementsTests
{
    private static readonly WebAppIdentifier Shop = new("shop");

    [Fact]
    public void AnApplicationThatStatesNoBrowser_IsRefusedWhileTheRunIsPlanned()
    {
        // Refused in the first browser step, a missing browser cost the environment's start-up and every stage
        // before it; nothing has run yet when the plan is made.
        ResourceGraph resources = Resources(App("shop", new WebAppConfig { BaseUrl = "http://shop/" }));

        UiConfigurationException refusal = Assert.Throws<UiConfigurationException>(() => UiRequirements.For(Shop, resources));

        Assert.Contains("'shop'", refusal.Message, StringComparison.Ordinal);
        Assert.Contains("browser", refusal.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ABrandedBuildNamedAsTheBrowser_IsRefusedWhileTheRunIsPlanned()
    {
        ResourceGraph resources = Resources(App("shop", new WebAppConfig { BaseUrl = "http://shop/", Browser = "msedge" }));

        UiConfigurationException refusal = Assert.Throws<UiConfigurationException>(() => UiRequirements.For(Shop, resources));

        Assert.Contains("Channel = \"msedge\"", refusal.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnApplicationStatingItsOwnAddress_RequiresOnlyItself()
    {
        ResourceGraph resources = Resources(App("shop", new WebAppConfig { BaseUrl = "http://shop/", Browser = "chromium" }));

        Assert.Equal(["ui.webapp/shop"], Describe(UiRequirements.For(Shop, resources)));
    }

    [Fact]
    public void AnApplicationWithoutAnAddress_RequiresTheSiteOfTheSameName_SoTheEnvironmentThatDeclaredItStartsIt()
    {
        // Damage before: the Docker environment had to know "ui.webapp" and treat it as a site; now the
        // browser package applies its own convention and requires the site itself.
        ResourceGraph resources = Resources(App("shop", new WebAppConfig { Browser = "chromium" }), Other(WebResourceKinds.SiteKind, "shop"));

        Assert.Equal(["ui.webapp/shop", "web.site/shop"], Describe(UiRequirements.For(Shop, resources)));
    }

    [Fact]
    public void TwoAddressesForOneApplication_AreRefused_InsteadOfTheOwnSilentlyWinning()
    {
        ResourceGraph resources = Resources(App("shop", new WebAppConfig { BaseUrl = "http://shop/", Browser = "chromium" }), Other(WebResourceKinds.SiteKind, "shop"));

        UiConfigurationException refusal = Assert.Throws<UiConfigurationException>(() => UiRequirements.For(Shop, resources));

        Assert.Contains("states its own 'BaseUrl'", refusal.Message);
        Assert.Contains("web.site 'shop'", refusal.Message);
    }

    [Fact]
    public void TwoResourcesCarryingTheName_AreRefusedNamingBoth()
    {
        ResourceGraph resources = Resources(
            App("shop", new WebAppConfig { Browser = "chromium" }),
            Other(WebResourceKinds.SiteKind, "shop"),
            Other(WebResourceKinds.RestApiKind, "shop"));

        UiConfigurationException refusal = Assert.Throws<UiConfigurationException>(() => UiRequirements.For(Shop, resources));

        Assert.Contains("web.site 'shop'", refusal.Message);
        Assert.Contains("web.restapi 'shop'", refusal.Message);
    }

    [Fact]
    public void AnApplicationWithNoAddressAnywhere_IsRefusedBeforeTheRunStarts()
    {
        ResourceGraph resources = Resources(App("shop", new WebAppConfig { Browser = "chromium" }));

        UiConfigurationException refusal = Assert.Throws<UiConfigurationException>(() => UiRequirements.For(Shop, resources));

        Assert.Contains("has no address", refusal.Message);
    }

    [Fact]
    public void ABridge_IsRequiredAlongsideTheApplication()
    {
        ResourceGraph resources = Resources(App("shop", new WebAppConfig { Browser = "chromium" }), Other(WebResourceKinds.SiteKind, "storefront"));
        WebAppIdentifier bridged = Shop.BridgedTo(WebResourceKinds.SiteKind.Requirement("storefront"));

        Assert.Equal(["ui.webapp/shop", "web.site/storefront"], Describe(UiRequirements.For(bridged, resources)));
    }

    [Fact]
    public void ABridgeInTheApplicationsOwnEntry_IsRequiredByName()
    {
        ResourceGraph resources = Resources(App("shop", new WebAppConfig { Browser = "chromium", BaseUrlFrom = "storefront" }), Other(WebResourceKinds.SiteKind, "storefront"));

        Assert.Equal(["ui.webapp/shop", "*/storefront"], Describe(UiRequirements.For(Shop, resources)));
    }

    private static IResourceNodeSource App(string identifier, WebAppConfig config)
        => new UiApplications([new(identifier, config)]);

    private static IResourceNodeSource Other(ResourceKind kind, string identifier)
        => new OtherResources(kind, identifier);

    private static ResourceGraph Resources(params IResourceNodeSource[] sources)
        => ResourceGraph.Compose(sources);

    private static string[] Describe(IEnumerable<EnvironmentRequirement> requirements)
        => [.. requirements.Select(requirement => $"{requirement.ResourceKind}/{requirement.ResourceIdentifier}")];

    private sealed class OtherResources(ResourceKind kind, string identifier) : DeclaredNodeSource
    {
        public override string SourceName => "another package";

        protected override IEnumerable<DeclaredResource> Declarations
            => [new DeclaredResource { Kind = kind, Identifier = identifier, Values = new Dictionary<ValueKey, string>(), DeclaredBy = this.SourceName }];
    }
}
