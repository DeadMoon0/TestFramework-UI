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
        ResourceGraph resources = Resources(App("shop", new WebAppConfig { Browser = "chromium" }), Other(WebEnvironmentResourceKinds.SiteKind, "shop"));

        Assert.Equal(["ui.webapp/shop", "web.site/shop"], Describe(UiRequirements.For(Shop, resources)));
    }

    [Fact]
    public void TwoAddressesForOneApplication_AreRefused_InsteadOfTheOwnSilentlyWinning()
    {
        ResourceGraph resources = Resources(App("shop", new WebAppConfig { BaseUrl = "http://shop/", Browser = "chromium" }), Other(WebEnvironmentResourceKinds.SiteKind, "shop"));

        UiConfigurationException refusal = Assert.Throws<UiConfigurationException>(() => UiRequirements.For(Shop, resources));

        Assert.Contains("states its own 'BaseUrl'", refusal.Message);
        Assert.Contains("web.site 'shop'", refusal.Message);
    }

    [Fact]
    public void TwoResourcesCarryingTheName_AreRefusedNamingBoth()
    {
        ResourceGraph resources = Resources(
            App("shop", new WebAppConfig { Browser = "chromium" }),
            Other(WebEnvironmentResourceKinds.SiteKind, "shop"),
            Other(WebEnvironmentResourceKinds.RestApiKind, "shop"));

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
        ResourceGraph resources = Resources(App("shop", new WebAppConfig { Browser = "chromium" }), Other(WebEnvironmentResourceKinds.SiteKind, "storefront"));
        WebAppIdentifier bridged = Shop.BridgedTo(WebEnvironmentResourceKinds.SiteKind.Requirement("storefront"));

        Assert.Equal(["ui.webapp/shop", "web.site/storefront"], Describe(UiRequirements.For(bridged, resources)));
    }

    [Fact]
    public void ABridgeInTheApplicationsOwnEntry_IsRequiredByName()
    {
        ResourceGraph resources = Resources(App("shop", new WebAppConfig { Browser = "chromium", BaseUrlFromSite = "storefront" }), Other(WebEnvironmentResourceKinds.SiteKind, "storefront"));

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
            => [new DeclaredResource(kind, identifier, new Dictionary<ValueKey, string>(), this.SourceName)];
    }
}
