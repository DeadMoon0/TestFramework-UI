using System;
using System.Collections.Generic;
using System.Linq;

using TestFramework.Config.Configuration;
using TestFramework.Core.Environment.Graph;

namespace TestFramework.UI.Browser.Configuration;

/// <summary>
/// The web applications a run drives, declared as resources on the run's list.
/// </summary>
/// <remarks>
/// <para>
/// This replaced a store the resolver read beside the run's values. An application's address was the one
/// coordinate in the family that bypassed the run's resource list, so nothing could check before the run
/// started that a browser step's application existed - and a store a step resolves from a service provider
/// is run data arriving the way §7 says it must not. Declared here, the application is on the list the engine
/// checks every requirement against, and its settings reach a step through the run.
/// </para>
/// <para>
/// Inheritance is resolved once, here, so a declared application already holds everything it inherited.
/// Identifiers are case-sensitive, like every other resource name in a run.
/// </para>
/// </remarks>
internal sealed class UiApplications : DeclaredNodeSource
{
    private readonly IReadOnlyDictionary<string, WebAppConfig> _applications;

    public UiApplications(IEnumerable<KeyValuePair<string, WebAppConfig>> applications)
    {
        ArgumentNullException.ThrowIfNull(applications);

        Dictionary<string, WebAppConfig> declared = new(StringComparer.Ordinal);
        foreach ((string identifier, WebAppConfig config) in applications)
        {
            declared[identifier] = config;
        }

        this._applications = ConfigInheritance.Resolve(declared);
    }

    public override string SourceName => "TestFramework.UI.Browser applications";

    protected override IEnumerable<DeclaredResource> Declarations
        => this._applications
            .OrderBy(static application => application.Key, StringComparer.Ordinal)
            .Select(application => new DeclaredResource(
                BrowserEnvironmentResourceKinds.WebAppKind,
                application.Key,
                WebAppValues.From(application.Value),
                $"UI application '{application.Key}'"));
}
