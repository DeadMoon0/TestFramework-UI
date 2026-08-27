using System;
using Microsoft.Extensions.DependencyInjection;

namespace TestFramework.UI.Web;

/// <summary>
/// Registers the bridge on a service collection directly, for a test that builds its services by hand.
/// </summary>
public static class UiWebBridgeServiceCollectionExtension
{
    internal const string ObsoleteMessage =
        "The bridge is gone and this call registers nothing. A browser step now asks the run where its application is, "
        + "and whatever configured or started that application published the address there - so LoadWebConfig() is all "
        + "that is needed. Delete the call; FromSite(...) and FromWebApi(...) are unaffected.";

    /// <summary>
    /// Does nothing. The run answers a web application's address now.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection, for chaining.</returns>
    /// <remarks>
    /// Kept as a no-op rather than removed so that an existing caller is told what happened instead of being
    /// handed a missing-method error. There were two sources behind this - one per foreign configuration store -
    /// plus rules for which got asked first, and all of it was a hand-built version of the run's own resource
    /// resolution. Nothing needs registering because a kind is a string the requirement already carries.
    /// </remarks>
    [Obsolete(ObsoleteMessage)]
    public static IServiceCollection AddUiWebBridge(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services;
    }
}
