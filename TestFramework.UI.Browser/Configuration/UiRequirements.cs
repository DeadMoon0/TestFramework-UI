using System.Collections.Generic;
using System.Linq;

using TestFramework.Core.Environment;
using TestFramework.Core.Environment.Graph;
using TestFramework.UI.Browser.Exceptions;
using TestFramework.UI.Browser.Identifier;

namespace TestFramework.UI.Browser.Configuration;

/// <summary>
/// What a browser step needs from the run, decided with the run's resources in view.
/// </summary>
/// <remarks>
/// <para>
/// Always the application itself - its settings - and, unless its own entry states one, the resource its
/// address comes from: the site or API it is bridged to, or the one resource of another kind under the same
/// name. That last rule is this package's convention, so this package applies it: the engine only checks
/// that whatever is required here is on the run's list, and hands the environment that declared it the
/// requirement - which is what starts a site container for the application a browser opens.
/// </para>
/// <para>
/// A second address for one name is refused here, before the run starts, rather than settled by an order
/// of precedence: an application whose own entry states a <c>BaseUrl</c> while a bridge or a same-name
/// resource also answers would otherwise open whichever the resolver happened to ask first.
/// </para>
/// </remarks>
internal static class UiRequirements
{
    /// <summary>
    /// The requirements a browser step states when the run's resources are not in view - its application,
    /// and the bridge when there is one.
    /// </summary>
    public static IReadOnlyCollection<EnvironmentRequirement> Without(WebAppIdentifier app)
    {
        EnvironmentRequirement own = BrowserEnvironmentResourceKinds.WebAppKind.Requirement(app.Identifier);
        return app.ExternalRequirement is { } bridge ? [own, bridge] : [own];
    }

    /// <summary>
    /// The requirements a browser step states while the run is planned.
    /// </summary>
    /// <exception cref="UiConfigurationException">Two addresses for one application, or none at all.</exception>
    public static IReadOnlyCollection<EnvironmentRequirement> For(WebAppIdentifier app, ResourceGraph resources)
    {
        List<EnvironmentRequirement> required = [BrowserEnvironmentResourceKinds.WebAppKind.Requirement(app.Identifier)];

        // An application nobody declared is the engine's to refuse, naming what is declared.
        if (!resources.TryGetNode(BrowserEnvironmentResourceKinds.WebApp, app.Identifier, out ResourceNode? own) || own is null)
        {
            return app.ExternalRequirement is { } unresolvedBridge ? [.. required, unresolvedBridge] : required;
        }

        bool statesAddress = own.DeclaredValues.Keys.Any(static key => key.ValueName == ValueNames.BaseUrl);
        EnvironmentRequirement? bridge = app.ExternalRequirement ?? ConfiguredBridge(own);
        IReadOnlyList<ResourceNode> sameName = [.. resources.NodesNamed(app.Identifier)
            .Where(static node => node.KindName != BrowserEnvironmentResourceKinds.WebApp && node.Kind.Offers(ValueNames.BaseUrl))];

        if (statesAddress)
        {
            List<string> others = [];
            if (bridge is not null)
            {
                others.Add($"the bridge to '{bridge.ResourceIdentifier}'");
            }

            others.AddRange(sameName.Select(static node => $"{node.KindName} '{node.Identifier}'"));
            if (others.Count > 0)
            {
                throw UiConfigurationException.TwoAddresses(app.Identifier, others);
            }

            return required;
        }

        if (bridge is not null)
        {
            required.Add(bridge);
            return required;
        }

        if (sameName.Count > 1)
        {
            throw UiConfigurationException.AmbiguousAddress(app.Identifier, sameName.Select(static node => $"{node.KindName} '{node.Identifier}'"));
        }

        if (sameName.Count == 0)
        {
            throw UiConfigurationException.MissingBaseUrl(app.Identifier);
        }

        required.Add(new EnvironmentRequirement(sameName[0].KindName, sameName[0].Identifier));
        return required;
    }

    /// <summary>
    /// A bridge stated in the application's own entry - <c>BaseUrlFromSite</c> or <c>BaseUrlFromApi</c> - names
    /// a resource without its kind, the way the resolver later reads it.
    /// </summary>
    private static EnvironmentRequirement? ConfiguredBridge(ResourceNode own)
    {
        foreach ((ValueKey key, string value) in own.DeclaredValues)
        {
            if (key.ValueName is nameof(WebAppConfig.BaseUrlFromSite) or nameof(WebAppConfig.BaseUrlFromApi) && value is { Length: > 0 })
            {
                return EnvironmentRequirement.AnyKind(value);
            }
        }

        return null;
    }
}
