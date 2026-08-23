using TestFramework.UI.Browser.Runtime;

namespace TestFramework.UI.Browser.Tests.Runtime;

/// <summary>
/// How a path in a step becomes an address.
/// </summary>
public class UiUrlsTests
{
    [Theory]
    // A leading slash still means "inside the application", so a suite does not change meaning when the
    // application moves behind a prefix.
    [InlineData("http://host/app/", "/products", "http://host/app/products")]
    [InlineData("http://host/app/", "products", "http://host/app/products")]
    [InlineData("http://host/app", "/products", "http://host/app/products")]
    [InlineData("http://host/", "/products", "http://host/products")]
    [InlineData("http://host", "products", "http://host/products")]
    // Nothing asked for is the application's own front door.
    [InlineData("http://host/app/", "", "http://host/app/")]
    [InlineData("http://host/app/", "/", "http://host/app/")]
    // Query strings and fragments survive.
    [InlineData("http://host/app/", "/orders?status=open", "http://host/app/orders?status=open")]
    [InlineData("http://host/app/", "orders#latest", "http://host/app/orders#latest")]
    // Deeper paths keep their shape.
    [InlineData("http://host/app/", "/orders/A-1001/lines", "http://host/app/orders/A-1001/lines")]
    public void ARelativePathHangsBelowTheConfiguredAddress(string baseUrl, string path, string expected)
        => Assert.Equal(expected, UiUrls.Resolve(baseUrl, path));

    [Theory]
    [InlineData("http://host/app/", "https://elsewhere.test/login", "https://elsewhere.test/login")]
    [InlineData("http://host/app/", "http://another.test/", "http://another.test/")]
    public void AnAbsoluteAddressIsLeftAlone(string baseUrl, string path, string expected)
        => Assert.Equal(expected, UiUrls.Resolve(baseUrl, path));

    [Fact]
    public void WithoutAConfiguredAddressWhatWasWrittenIsUsed()
        => Assert.Equal("/products", UiUrls.Resolve(null, "/products"));
}
