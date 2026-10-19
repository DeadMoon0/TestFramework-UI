using System.Collections.Generic;
using System.Linq;
using System;

using TestFramework.Core.Environment;
using TestFramework.Core.Environment.Graph;
using TestFramework.UI.Browser.Exceptions;
using TestFramework.UI.Browser.Identifier;
using TestFramework.UI.Browser.Runtime;

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
        EnvironmentRequirement own = UiResourceKinds.WebAppKind.Requirement(app.Identifier);
        return app.Bridge is { } bridge ? [own, bridge] : [own];
    }

    /// <summary>
    /// The requirements a browser step states while the run is planned.
    /// </summary>
    /// <exception cref="UiConfigurationException">No browser, a browser that is not an engine, two addresses for one
    /// application, or none at all.</exception>
    /// <exception cref="UiBrowserNotInstalledException">The browser build a launch needs is not on this machine, and the
    /// application did not ask the run to install it.</exception>
    public static IReadOnlyCollection<EnvironmentRequirement> For(WebAppIdentifier app, ResourceGraph resources, UiRunState runState)
        => For(app, resources, runState, PlaywrightBuilds.Check);

    /// <summary>
    /// The requirements, with what this machine has installed asked of <paramref name="checkBuild"/>.
    /// </summary>
    /// <remarks>A test states the machine rather than inheriting the one it runs on.</remarks>
    internal static IReadOnlyCollection<EnvironmentRequirement> For(WebAppIdentifier app, ResourceGraph resources, UiRunState runState, Func<string, bool, UiBuildCheck> checkBuild)
    {
        List<EnvironmentRequirement> required = [UiResourceKinds.WebAppKind.Requirement(app.Identifier)];

        // An application nobody declared is the engine's to refuse, naming what is declared.
        if (!resources.TryGetNode(UiResourceKinds.WebApp, app.Identifier, out ResourceNode? own) || own is null)
        {
            return app.Bridge is { } unresolvedBridge ? [.. required, unresolvedBridge] : required;
        }

        // Which browser a test drives decides what it proves, and there is no default (§5): a run that states none is
        // refused now, while nothing has started, rather than in its first browser step after the environment and the
        // earlier stages have run. The resolver keeps the same checks as the last defence.
        string? browser = own.DeclaredValues.FirstOrDefault(static pair => pair.Key.ValueName == nameof(WebAppConfig.Browser)).Value;
        if (browser is not { Length: > 0 })
        {
            throw UiConfigurationException.MissingBrowser(app.Identifier);
        }

        if (!UiConfigResolver.PlaywrightEngines.Contains(browser))
        {
            throw UiConfigurationException.NotABrowserEngine(app.Identifier, browser);
        }

        EnsureTheBrowserIsHere(app.Identifier, own, runState, checkBuild);

        bool statesAddress = own.DeclaredValues.Keys.Any(static key => key.ValueName == ValueNames.BaseUrl);
        EnvironmentRequirement? bridge = app.Bridge ?? ConfiguredBridge(own);
        IReadOnlyList<ResourceNode> sameName = [.. resources.NodesNamed(app.Identifier)
            .Where(static node => node.KindName != UiResourceKinds.WebApp && node.Kind.Offers(ValueNames.BaseUrl))];

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
    /// Refuses, while nothing has started, a run whose browser is not on this machine - or notes the install the
    /// application asked for.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A pipeline agent without the browser used to find out at the first browser step, after the environment had
    /// started and every earlier step had run, and then tore it all down. The build a launch looks for is a file
    /// question, so it is asked here.
    /// </para>
    /// <para>
    /// A branded build is left to the launch: where Edge or Chrome is installed is the machine's business, and a
    /// guess at its path would refuse runs that would have worked.
    /// </para>
    /// </remarks>
    private static void EnsureTheBrowserIsHere(string identifier, ResourceNode own, UiRunState runState, Func<string, bool, UiBuildCheck> checkBuild)
    {
        Dictionary<string, string> values = [];
        foreach ((ValueKey key, string value) in own.DeclaredValues)
        {
            values.TryAdd(key.ValueName, value);
        }

        // The machine's own overrides decide headless or headed, and chromium's two modes are separate builds.
        WebAppConfig config = UiEnvironmentOverrides.Apply(WebAppValues.Read(values, identifier));

        if (config.Channel is { Length: > 0 } channel)
        {
            if (config.Install == true)
            {
                throw UiConfigurationException.InstallWithChannel(identifier, channel);
            }

            return;
        }

        string engine = config.EffectiveBrowser;
        UiBuildCheck check = checkBuild(engine, config.EffectiveHeadless);

        if (config.Install == true)
        {
            // Asked for, so it happens whenever the build is not known to be here: Playwright's installer skips what
            // is already in place, and a cache this cannot see is no reason to skip what the application asked for.
            if (!check.IsInstalled)
            {
                runState.InstallBeforeTheRun(new UiPlannedInstall(engine.ToLowerInvariant(), config.EffectiveHeadless, check.Needed));
            }

            return;
        }

        if (check.IsKnownMissing)
        {
            throw UiBrowserNotInstalledException.BeforeTheRun(identifier, engine, check);
        }
    }

    /// <summary>
    /// A bridge stated in the application's own entry - <c>BaseUrlFrom</c> - names a resource without its kind, the
    /// way the resolver later reads it.
    /// </summary>
    private static EnvironmentRequirement? ConfiguredBridge(ResourceNode own)
    {
        foreach ((ValueKey key, string value) in own.DeclaredValues)
        {
            if (key.ValueName is nameof(WebAppConfig.BaseUrlFrom) && value is { Length: > 0 })
            {
                return EnvironmentRequirement.AnyKind(value);
            }
        }

        return null;
    }
}
