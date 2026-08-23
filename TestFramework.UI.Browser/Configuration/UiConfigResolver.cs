using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using TestFramework.UI.Browser.Exceptions;
using TestFramework.UI.Browser.Identifier;

namespace TestFramework.UI.Browser.Configuration;

/// <summary>
/// Turns an application identifier into the configuration a step runs with.
/// </summary>
internal static class UiConfigResolver
{
    /// <summary>
    /// Resolves the configuration, including an address that lives in another package's configuration.
    /// </summary>
    /// <param name="serviceProvider">The run's services.</param>
    /// <param name="identifier">The application.</param>
    /// <returns>The configuration, with a usable base address.</returns>
    /// <exception cref="UiConfigurationException">The application is not configured, or has no address.</exception>
    public static WebAppConfig Resolve(IServiceProvider serviceProvider, WebAppIdentifier identifier)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ArgumentNullException.ThrowIfNull(identifier);

        UiConfigStore? store = serviceProvider.GetService<UiConfigStore>();

        if (store is null)
        {
            throw UiConfigurationException.MissingIdentifier(identifier, []);
        }

        WebAppConfig config = store.Get(identifier);

        if (config.BaseUrl is { Length: > 0 })
        {
            return config;
        }

        string? foreignIdentifier = identifier.BaseUrlFromIdentifier ?? config.BaseUrlFromApi;

        if (foreignIdentifier is { Length: > 0 }
            && TryResolveForeignBaseUrl(serviceProvider, foreignIdentifier) is { Length: > 0 } foreignBaseUrl)
        {
            return config with { BaseUrl = foreignBaseUrl };
        }

        throw UiConfigurationException.MissingBaseUrl(identifier);
    }

    private static string? TryResolveForeignBaseUrl(IServiceProvider serviceProvider, string foreignIdentifier)
    {
        // Several bridges may be registered; the first that recognises the identifier wins, and none of
        // them being able to answer is a configuration error rather than a silent fallback.
        foreach (IUiBaseUrlSource source in serviceProvider.GetServices<IUiBaseUrlSource>())
        {
            if (source.TryGetBaseUrl(serviceProvider, foreignIdentifier, out string? baseUrl)
                && baseUrl is { Length: > 0 })
            {
                return baseUrl;
            }
        }

        return null;
    }
}
