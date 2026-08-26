using System;
using TestFramework.Core.Environment;
using TestFramework.UI.Browser.Identifier;
using TestFramework.Web;

namespace TestFramework.UI.Web;

/// <summary>
/// Points a web application at a resource the TestFramework.Web family configures under another name.
/// </summary>
/// <remarks>
/// Neither call is needed when the names already match: a site configured or served under the
/// application's own identifier resolves by itself. These are the explicit tools for the two
/// exceptions -- an application served by the REST API process itself, and a name mismatch.
/// </remarks>
public static class WebAppIdentifierExtensions
{
    /// <summary>
    /// Resolves the application's address from a configured REST API, for an application the API
    /// process serves itself.
    /// </summary>
    /// <param name="identifier">The application.</param>
    /// <param name="apiIdentifier">The API identifier whose address is the application's address.</param>
    /// <remarks>
    /// Besides the address, the browser steps then declare the API's own environment requirement, so
    /// an environment that provisions the application for API steps satisfies the browser steps too:
    /// one container, both doors.
    /// </remarks>
    public static WebAppIdentifier FromWebApi(this WebAppIdentifier identifier, string apiIdentifier)
    {
        ArgumentNullException.ThrowIfNull(identifier);
        ArgumentException.ThrowIfNullOrWhiteSpace(apiIdentifier);

        return identifier.BridgedTo(new EnvironmentRequirement(WebEnvironmentResourceKinds.RestApi, apiIdentifier));
    }

    /// <summary>
    /// Resolves the application's address from a configured site whose identifier differs from the
    /// application's own.
    /// </summary>
    /// <param name="identifier">The application.</param>
    /// <param name="siteIdentifier">The site identifier whose address is the application's address.</param>
    public static WebAppIdentifier FromSite(this WebAppIdentifier identifier, string siteIdentifier)
    {
        ArgumentNullException.ThrowIfNull(identifier);
        ArgumentException.ThrowIfNullOrWhiteSpace(siteIdentifier);

        return identifier.BridgedTo(new EnvironmentRequirement(WebEnvironmentResourceKinds.Site, siteIdentifier));
    }
}
