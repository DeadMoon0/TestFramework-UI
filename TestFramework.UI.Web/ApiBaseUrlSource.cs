using System;
using Microsoft.Extensions.DependencyInjection;
using TestFramework.UI.Browser.Configuration;
using TestFramework.Web;
using TestFramework.Web.Configuration;

namespace TestFramework.UI.Web;

/// <summary>
/// Answers a web application's address from the REST API configuration store, for an application
/// the API process serves itself.
/// </summary>
internal sealed class ApiBaseUrlSource : IUiBaseUrlSource
{
    public string? ResourceKind => WebEnvironmentResourceKinds.RestApi;

    public bool TryGetBaseUrl(IServiceProvider serviceProvider, string foreignIdentifier, out string? baseUrl)
    {
        baseUrl = null;

        WebConfigStore<ApiConfig>? store = serviceProvider.GetService<WebConfigStore<ApiConfig>>();
        if (store is null || !store.TryGetConfig(foreignIdentifier, out ApiConfig? config) || config is null)
            return false;

        baseUrl = config.BaseUrl;
        return !string.IsNullOrWhiteSpace(baseUrl);
    }
}
