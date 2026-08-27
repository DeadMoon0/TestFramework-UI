using System;
using TestFramework.Config.Builder.InstanceBuilder;

namespace TestFramework.UI.Web;

/// <summary>
/// Extension methods that let browser steps resolve their addresses from the TestFramework.Web
/// family's configuration.
/// </summary>
public static class UiWebBridgeConfigExtension
{
    /// <summary>
    /// Does nothing. The run answers a web application's address now.
    /// </summary>
    /// <param name="builder">The config instance builder.</param>
    /// <returns>The builder for fluent chaining.</returns>
    /// <remarks>
    /// See <see cref="UiWebBridgeServiceCollectionExtension.AddUiWebBridge"/>. Keeping <c>.LoadWebConfig()</c>
    /// is what puts a site's or an API's address into the run; this call added nothing to that.
    /// </remarks>
    [Obsolete(UiWebBridgeServiceCollectionExtension.ObsoleteMessage)]
    public static IConfigInstanceBuilder LoadUIWebBridge(this IConfigInstanceBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder;
    }
}
