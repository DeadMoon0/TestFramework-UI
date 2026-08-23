using System;
using Microsoft.Extensions.DependencyInjection;
using TestFramework.UI.Browser.Configuration;

namespace TestFramework.UI.Web;

/// <summary>
/// Registers the bridge on a service collection directly, for a test that builds its services by hand.
/// </summary>
/// <remarks>
/// <c>LoadUIWebBridge()</c> is the same registration reached through the configuration builder; this is
/// the level underneath it, so a fixture that assembles its own <see cref="IServiceCollection"/> gets
/// exactly what the builder would have registered.
/// </remarks>
public static class UiWebBridgeServiceCollectionExtension
{
    /// <summary>
    /// Registers the sources that answer a web application's address from the Site and Api
    /// configuration stores.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection, for chaining.</returns>
    /// <remarks>
    /// The site source is registered first: an application's own identifier is a site before it is
    /// anything else, while the API road stays selected by <c>FromWebApi</c> or <c>BaseUrlFromApi</c>.
    /// </remarks>
    public static IServiceCollection AddUiWebBridge(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IUiBaseUrlSource, SiteBaseUrlSource>();
        services.AddSingleton<IUiBaseUrlSource, ApiBaseUrlSource>();

        return services;
    }
}
