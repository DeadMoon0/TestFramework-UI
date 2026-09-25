using TestFramework.Core.Environment.Graph;
using TestFramework.UI.Browser.Configuration;

namespace TestFramework.UI.Browser;

/// <summary>
/// The kinds of resource browser steps need a run to declare.
/// </summary>
public static class BrowserEnvironmentResourceKinds
{
    /// <summary>
    /// A web application reachable over HTTP, addressed by a <see cref="Identifier.WebAppIdentifier"/>.
    /// </summary>
    public const string WebApp = "ui.webapp";

    /// <summary>
    /// A web application as the run holds it: the browser settings it is driven with, and its address when
    /// the application's own entry states one. Declared for every application the <c>Ui</c> configuration
    /// section or <c>AddUiBrowser(...)</c> names, so a browser step's requirement is checked against it
    /// before the run starts.
    /// </summary>
    /// <remarks>
    /// The address is optional because it has two other homes: a bridged site or API, and a resource of
    /// another kind under the same name. Every other value is a setting with a documented default on
    /// <see cref="WebAppConfig"/>, so an entry holds only what it states.
    /// </remarks>
    public static readonly ResourceKind WebAppKind = ResourceKind
        .Named(WebApp)
        .OffersPerVantage(ValueNames.BaseUrl, optional: true)
        .Offers(nameof(WebAppConfig.BaseUrlFromApi), optional: true)
        .Offers(nameof(WebAppConfig.BaseUrlFromSite), optional: true)
        .Offers(nameof(WebAppConfig.Browser), optional: true)
        .Offers(nameof(WebAppConfig.Channel), optional: true)
        .Offers(nameof(WebAppConfig.Headless), optional: true)
        .Offers(nameof(WebAppConfig.Device), optional: true)
        .Offers(nameof(WebAppConfig.ViewportWidth), optional: true)
        .Offers(nameof(WebAppConfig.ViewportHeight), optional: true)
        .Offers(nameof(WebAppConfig.UserAgent), optional: true)
        .Offers(nameof(WebAppConfig.IsMobile), optional: true)
        .Offers(nameof(WebAppConfig.HasTouch), optional: true)
        .Offers(nameof(WebAppConfig.DeviceScaleFactor), optional: true)
        .Offers(nameof(WebAppConfig.Locale), optional: true)
        .Offers(nameof(WebAppConfig.ColorScheme), optional: true)
        .Offers(nameof(WebAppConfig.SlowMo), optional: true)
        .Offers(nameof(WebAppConfig.DefaultActionTimeout), optional: true)
        .Offers(nameof(WebAppConfig.DefaultCompareTimeout), optional: true)
        .Offers(nameof(WebAppConfig.TestIdAttribute), optional: true)
        .Offers(nameof(WebAppConfig.AmbiguityMode), optional: true)
        .Offers(nameof(WebAppConfig.IgnoreHttpsErrors), optional: true)
        .Offers(nameof(WebAppConfig.WidgetCapture), optional: true)
        .Build();
}
