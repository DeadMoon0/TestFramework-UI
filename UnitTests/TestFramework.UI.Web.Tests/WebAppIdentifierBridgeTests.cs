using TestFramework.UI.Browser.Identifier;
using TestFramework.Web;
using Xunit;

namespace TestFramework.UI.Web.Tests;

/// <summary>
/// Covers what the bridging calls put on the identifier: the foreign name the address resolves
/// from, and the requirement the browser steps declare instead of their own.
/// </summary>
public class WebAppIdentifierBridgeTests
{
    [Fact]
    public void FromWebApi_PointsTheAddressAndTheRequirementAtTheApi()
    {
        WebAppIdentifier shop = new WebAppIdentifier("shop").FromWebApi("shop-api");

        Assert.Equal("shop", shop.Identifier);
        Assert.Equal("shop-api", shop.BaseUrlFromIdentifier);
        Assert.Equal(WebEnvironmentResourceKinds.RestApi, shop.ExternalRequirement!.ResourceKind);
        Assert.Equal("shop-api", shop.ExternalRequirement.ResourceIdentifier);
    }

    [Fact]
    public void FromSite_PointsTheAddressAndTheRequirementAtTheSite()
    {
        WebAppIdentifier shop = new WebAppIdentifier("shop").FromSite("shop-ui");

        Assert.Equal("shop-ui", shop.BaseUrlFromIdentifier);
        Assert.Equal(WebEnvironmentResourceKinds.Site, shop.ExternalRequirement!.ResourceKind);
        Assert.Equal("shop-ui", shop.ExternalRequirement.ResourceIdentifier);
    }

    [Fact]
    public void APlainIdentifier_CarriesNoBridge()
    {
        WebAppIdentifier shop = new("shop");

        Assert.Null(shop.BaseUrlFromIdentifier);
        Assert.Null(shop.ExternalRequirement);
    }
}
