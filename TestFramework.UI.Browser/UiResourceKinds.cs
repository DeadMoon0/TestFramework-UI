using TestFramework.Core.Environment.Graph;
using TestFramework.UI.Browser.Configuration;

namespace TestFramework.UI.Browser;

/// <summary>
/// The kinds of resource browser steps need a run to declare.
/// </summary>
public static class UiResourceKinds
{
    /// <summary>
    /// A web application reachable over HTTP, addressed by a <see cref="Identifier.WebAppIdentifier"/>.
    /// </summary>
    public const string WebApp = "ui.webapp";

    /// <summary>
    /// A web application as the run holds it: the browser settings it is driven with, and its address when
    /// the application's own entry states one. Declared for every application the <c>Ui</c> configuration
    /// section or <c>AddUi(...)</c> names, so a browser step's requirement is checked against it
    /// before the run starts.
    /// </summary>
    /// <remarks>
    /// The address is optional because it has two other homes: a bridged site or API, and a resource of
    /// another kind under the same name. Every other value is a setting with a documented default on
    /// <see cref="WebAppConfig"/>, so an entry holds only what it states.
    /// </remarks>
    public static readonly ResourceKind WebAppKind = ResourceKind
        .Named(WebApp)
        .OffersPerVantage(ValueNames.BaseUrl, ValueTraits.Optional)
        .Offers(nameof(WebAppConfig.BaseUrlFrom), ValueTraits.Optional)
        .Offers(nameof(WebAppConfig.Browser), ValueTraits.Optional)
        .Offers(nameof(WebAppConfig.Channel), ValueTraits.Optional)
        .Offers(nameof(WebAppConfig.Headless), ValueTraits.Optional)
        .Offers(nameof(WebAppConfig.Device), ValueTraits.Optional)
        .Offers(nameof(WebAppConfig.ViewportWidth), ValueTraits.Optional)
        .Offers(nameof(WebAppConfig.ViewportHeight), ValueTraits.Optional)
        .Offers(nameof(WebAppConfig.UserAgent), ValueTraits.Optional)
        .Offers(nameof(WebAppConfig.IsMobile), ValueTraits.Optional)
        .Offers(nameof(WebAppConfig.HasTouch), ValueTraits.Optional)
        .Offers(nameof(WebAppConfig.DeviceScaleFactor), ValueTraits.Optional)
        .Offers(nameof(WebAppConfig.Locale), ValueTraits.Optional)
        .Offers(nameof(WebAppConfig.ColorScheme), ValueTraits.Optional)
        .Offers(nameof(WebAppConfig.SlowMo), ValueTraits.Optional)
        .Offers(nameof(WebAppConfig.DefaultActionTimeout), ValueTraits.Optional)
        .Offers(nameof(WebAppConfig.DefaultCompareTimeout), ValueTraits.Optional)
        .Offers(nameof(WebAppConfig.TestIdAttribute), ValueTraits.Optional)
        .Offers(nameof(WebAppConfig.AmbiguityMode), ValueTraits.Optional)
        .Offers(nameof(WebAppConfig.IgnoreHttpsErrors), ValueTraits.Optional)
        .Offers(nameof(WebAppConfig.WidgetCapture), ValueTraits.Optional)
        .Build();
}
