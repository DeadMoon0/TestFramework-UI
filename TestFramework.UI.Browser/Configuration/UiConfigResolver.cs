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

        // The bridged name comes off the requirement, which is the only thing that carries it now: a
        // separate member holding the same string was one the four bridge sites had to keep in step by hand.
        string? foreignIdentifier = identifier.ExternalRequirement?.ResourceIdentifier ?? config.BaseUrlFromSite ?? config.BaseUrlFromApi;

        if (foreignIdentifier is { Length: > 0 }
            && TryResolveForeignBaseUrl(serviceProvider, foreignIdentifier, identifier.ExternalRequirement?.ResourceKind) is { Length: > 0 } foreignBaseUrl)
        {
            return config with { BaseUrl = foreignBaseUrl };
        }

        // The application's own identifier is the last road, and the normal one: a site serving this
        // application publishes its address under the same name, whether a container started it or a
        // configuration file named a deployed one. Deployed and containerized runs both land here.
        if (foreignIdentifier is null
            && TryResolveForeignBaseUrl(serviceProvider, identifier.Identifier, requiredKind: null) is { Length: > 0 } ownBaseUrl)
        {
            return config with { BaseUrl = ownBaseUrl };
        }

        throw UiConfigurationException.MissingBaseUrl(identifier);
    }

    private static string? TryResolveForeignBaseUrl(IServiceProvider serviceProvider, string foreignIdentifier, string? requiredKind)
    {
        // Several bridges may be registered. An identifier that carries a kind is answered by the
        // matching-kind sources first, then by the kind-agnostic ones; without a kind, registration
        // order decides. None of them answering is a configuration error rather than a silent
        // fallback.
        if (requiredKind is not null)
        {
            return AskSources(serviceProvider, foreignIdentifier, source => string.Equals(source.ResourceKind, requiredKind, StringComparison.Ordinal))
                ?? AskSources(serviceProvider, foreignIdentifier, source => source.ResourceKind is null);
        }

        return AskSources(serviceProvider, foreignIdentifier, _ => true);
    }

    private static string? AskSources(IServiceProvider serviceProvider, string foreignIdentifier, Func<IUiBaseUrlSource, bool> filter)
    {
        foreach (IUiBaseUrlSource source in serviceProvider.GetServices<IUiBaseUrlSource>())
        {
            if (filter(source)
                && source.TryGetBaseUrl(serviceProvider, foreignIdentifier, out string? baseUrl)
                && baseUrl is { Length: > 0 })
            {
                return baseUrl;
            }
        }

        return null;
    }
}
