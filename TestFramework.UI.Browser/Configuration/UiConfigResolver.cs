using TestFramework.UI.Browser.Runtime;
using System;
using Microsoft.Extensions.DependencyInjection;
using TestFramework.Core.Environment.Graph;
using TestFramework.Core.Steps;
using TestFramework.UI.Browser.Exceptions;
using TestFramework.UI.Browser.Identifier;

namespace TestFramework.UI.Browser.Configuration;

/// <summary>
/// Turns an application identifier into the configuration a step runs with.
/// </summary>
/// <remarks>
/// <para>
/// An application's address has two possible homes and only one of them is this package's. A browser opens
/// the same application another package configured as a site or as a REST API, and one a container started
/// answers on a port the operating system chose. So the address is asked of the <em>run</em>: whatever
/// declared or started the thing published it there, and this package never learns which.
/// </para>
/// <para>
/// That replaced a bridge. There used to be an <c>IUiBaseUrlSource</c> seam with an implementation per
/// foreign package, each reading that package's configuration store, plus rules here for which
/// implementation got asked first. Every part of it was a hand-built version of what the run's resource
/// resolution does - including the interesting part, kind disambiguation, which it did worse: two packages
/// answering to one identifier were settled by registration order, where the run refuses and says both
/// names. The seam existed because this package must not depend on Web; asking the run needs no such
/// dependency, because a kind is a string the requirement already carries.
/// </para>
/// </remarks>
internal static class UiConfigResolver
{
    /// <summary>
    /// Resolves the configuration, including an address that belongs to another package's resource.
    /// </summary>
    /// <summary>
    /// The configuration a step actually runs with, recorded on the run as it is resolved.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The three callers all did the same two things - resolve, then apply the environment overrides - and
    /// the second one can change the browser, so anything recording what a run drove had to happen after
    /// both. Doing it here rather than at each call site is what keeps one answer: three places recording
    /// the same fact is three chances for them to disagree about what it is called.
    /// </para>
    /// <para>
    /// What it records is what &#167;5 asks of every default: not what the run did, but what it did it with.
    /// A suite that passes on two machines with different browsers now says so on each run, instead of both
    /// runs looking identical and neither naming the browser that proved it.
    /// </para>
    /// </remarks>
    /// <param name="context">The run.</param>
    /// <param name="identifier">The application.</param>
    /// <returns>The configuration, with overrides applied.</returns>
    public static WebAppConfig ResolveEffective(RunContext context, WebAppIdentifier identifier)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(identifier);

        WebAppConfig config = UiEnvironmentOverrides.Apply(Resolve(context, identifier));

        context.EffectiveSettings.Record(Source, $"{identifier.Identifier}:Browser", config.EffectiveBrowser);
        context.EffectiveSettings.Record(Source, $"{identifier.Identifier}:Headless", config.EffectiveHeadless ? "true" : "false");

        // How much evidence was kept decides what a later reader can look at, and it defaults - so a run
        // with no pictures on its steps should be able to say whether that was the policy or a failure.
        context.EffectiveSettings.Record(
            Source,
            $"{identifier.Identifier}:WidgetCapture",
            config.EffectiveWidgetCapture.ToString());

        if (config.Channel is { Length: > 0 } channel)
        {
            context.EffectiveSettings.Record(Source, $"{identifier.Identifier}:Channel", channel);
        }

        return config;
    }

    /// <summary>Who recorded it, so another package's "Browser" is a different entry rather than a clash.</summary>
    private const string Source = "TestFramework.UI.Browser";

    /// <param name="context">The run.</param>
    /// <param name="identifier">The application.</param>
    /// <returns>The configuration, with a usable base address.</returns>
    /// <exception cref="UiConfigurationException">The application is not configured, or has no address.</exception>
    public static WebAppConfig Resolve(RunContext context, WebAppIdentifier identifier)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(identifier);

        UiConfigStore? store = context.Services.GetService<UiConfigStore>();

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
            && Address(context, foreignIdentifier, identifier.ExternalRequirement?.ResourceKind) is { Length: > 0 } foreignBaseUrl)
        {
            return config with { BaseUrl = foreignBaseUrl };
        }

        // The application's own identifier is the last road, and the normal one: a site serving this
        // application publishes its address under the same name, whether a container started it or a
        // configuration file named a deployed one. Deployed and containerized runs both land here.
        if (foreignIdentifier is null
            && Address(context, identifier.Identifier, requiredKind: null) is { Length: > 0 } ownBaseUrl)
        {
            return config with { BaseUrl = ownBaseUrl };
        }

        throw UiConfigurationException.MissingBaseUrl(identifier);
    }

    /// <summary>
    /// The address the run holds for a resource, from the viewpoint of the process opening the browser.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The host viewpoint, because the browser runs here. A container-side address would open in a browser
    /// that cannot route to it, and the two really do differ once a container serves the application - which
    /// is precisely what a single <c>BaseUrl</c> in somebody's configuration store could not say.
    /// </para>
    /// <para>
    /// Without a kind the run answers by identifier alone and refuses if two kinds claim the same name,
    /// naming both. That refusal is the behaviour the bridge could not have: it asked its sources in
    /// registration order and returned the first answer.
    /// </para>
    /// </remarks>
    private static string? Address(RunContext context, string resourceIdentifier, string? requiredKind)
    {
        ValueRef reference = requiredKind is { Length: > 0 } kind
            ? ValueRef.For(kind, resourceIdentifier, ValueNames.BaseUrl)
            : ValueRef.AnyKind(resourceIdentifier, ValueNames.BaseUrl);

        return context.Values.TryGet(reference, ResourceVantage.Host, out string? baseUrl) ? baseUrl : null;
    }
}
