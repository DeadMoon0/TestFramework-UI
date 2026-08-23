using System;
using Microsoft.Extensions.DependencyInjection;
using TestFramework.UI.Browser.Configuration;
using TestFramework.Web;
using TestFramework.Web.Configuration;
using TestFramework.Web.Site;

namespace TestFramework.UI.Web;

/// <summary>
/// Answers a web application's address from the site configuration store.
/// </summary>
/// <remarks>
/// The store is the same one a container environment publishes into and a <c>Site</c> configuration
/// section fills, so the answer is right for a deployed and a containerized run alike.
/// </remarks>
internal sealed class SiteBaseUrlSource : IUiBaseUrlSource
{
    public string? ResourceKind => WebEnvironmentResourceKinds.Site;

    public bool TryGetBaseUrl(IServiceProvider serviceProvider, string foreignIdentifier, out string? baseUrl)
    {
        baseUrl = null;

        WebConfigStore<SiteConfig>? store = serviceProvider.GetService<WebConfigStore<SiteConfig>>();
        if (store is null || !store.TryGetConfig(foreignIdentifier, out SiteConfig? config) || config is null)
            return false;

        baseUrl = config.BaseUrl;
        return !string.IsNullOrWhiteSpace(baseUrl);
    }
}
