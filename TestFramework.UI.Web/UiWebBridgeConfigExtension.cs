using TestFramework.Config.Builder.InstanceBuilder;
using Microsoft.Extensions.DependencyInjection;
using TestFramework.UI.Browser.Configuration;

namespace TestFramework.UI.Web;

/// <summary>
/// Extension methods that let browser steps resolve their addresses from the TestFramework.Web
/// family's configuration.
/// </summary>
public static class UiWebBridgeConfigExtension
{
    /// <summary>
    /// Registers the sources that answer a web application's address from the Site and Api
    /// configuration stores.
    /// </summary>
    /// <param name="builder">The config instance builder.</param>
    /// <returns>The builder for fluent chaining.</returns>
    /// <remarks>
    /// Add it alongside <c>.LoadUIConfig()</c> and <c>.LoadWebConfig()</c>. The site source is
    /// registered first: an application's own identifier is a site before it is anything else, while
    /// the API road stays selected by <c>FromWebApi</c> or <c>BaseUrlFromApi</c>.
    /// </remarks>
    public static IConfigInstanceBuilder LoadUIWebBridge(this IConfigInstanceBuilder builder)
    {
        builder.AddService((services, _) =>
        {
            services.AddSingleton<IUiBaseUrlSource, SiteBaseUrlSource>();
            services.AddSingleton<IUiBaseUrlSource, ApiBaseUrlSource>();
        });

        return builder;
    }
}
