using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
    /// <para>
    /// The site source is registered first: an application's own identifier is a site before it is
    /// anything else, while the API road stays selected by <c>FromWebApi</c> or <c>BaseUrlFromApi</c>.
    /// </para>
    /// <para>
    /// <c>TryAddEnumerable</c> rather than <c>Add</c>, keyed on the implementation type, so calling this
    /// twice registers each source once - a bridge asked the same question twice would answer an
    /// application's address from the same store twice and make the ambiguity report read as if two
    /// candidates existed. A source of the caller's own is a different type and still joins the list,
    /// because <see cref="IUiBaseUrlSource"/> is a seam and not a slot.
    /// </para>
    /// </remarks>
    public static IServiceCollection AddUiWebBridge(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IUiBaseUrlSource, SiteBaseUrlSource>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IUiBaseUrlSource, ApiBaseUrlSource>());

        return services;
    }
}
