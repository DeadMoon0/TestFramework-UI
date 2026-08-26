using System;
using System.Collections.Generic;
using TestFramework.Config.Configuration;
using TestFramework.UI.Browser.Exceptions;

namespace TestFramework.UI.Browser.Configuration;

/// <summary>
/// The configured web applications of a run, resolved by identifier.
/// </summary>
/// <remarks>
/// <para>
/// An entry may inherit from another, so a mobile variant of an application is one line rather than a copy.
/// The inheriting is <c>TestFramework.Config</c>'s and not this package's: it is a general mechanism, this is
/// an edge pack, and the one time this package wrote the merge itself it got "did anybody state this?" wrong
/// for seven of twenty-one values. Everything left here is the part that is genuinely about browsers.
/// </para>
/// <para>
/// Internal on purpose. A caller who could register this by hand could register it <em>alone</em>, and this
/// package would then be half on: configured applications, and nothing watching the steps that drive them.
/// Declaring applications goes through <c>AddUiBrowser(...)</c> or <c>.LoadUIConfig()</c>, which is what
/// makes that impossible to express rather than merely inadvisable.
/// </para>
/// </remarks>
internal sealed class UiConfigStore
{
    private readonly IReadOnlyDictionary<string, WebAppConfig> resolved;

    /// <summary>
    /// Creates the store, resolving what each entry inherits.
    /// </summary>
    /// <remarks>
    /// Resolved once here rather than per read. It is also the earliest moment it can be done - a chain that
    /// loops or names an entry nobody declared is a mistake in the configuration, not in the run that later
    /// touched it, and load time beats waiting for a step to ask.
    /// </remarks>
    /// <param name="configs">The declared entries, keyed by identifier.</param>
    /// <exception cref="TestFramework.Core.Exceptions.FrameworkConfigurationException">
    /// An entry inherits from itself or from something undeclared.
    /// </exception>
    internal UiConfigStore(IEnumerable<KeyValuePair<string, WebAppConfig>> configs)
    {
        ArgumentNullException.ThrowIfNull(configs);

        Dictionary<string, WebAppConfig> declared = new Dictionary<string, WebAppConfig>(StringComparer.OrdinalIgnoreCase);

        foreach ((string identifier, WebAppConfig config) in configs)
        {
            declared[identifier] = config;
        }

        this.resolved = ConfigInheritance.Resolve(declared);
    }

    /// <summary>The identifiers this store knows.</summary>
    public IEnumerable<string> Identifiers => this.resolved.Keys;

    /// <summary>
    /// The configuration of one application, with everything it inherits already filled in.
    /// </summary>
    /// <param name="identifier">The application identifier.</param>
    /// <returns>The configuration.</returns>
    /// <exception cref="UiConfigurationException">Nothing is configured under that identifier, or the entry
    /// states no browser.</exception>
    public WebAppConfig Get(string identifier)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identifier);

        if (!this.resolved.TryGetValue(identifier, out WebAppConfig? config))
        {
            throw UiConfigurationException.MissingIdentifier(identifier, this.resolved.Keys);
        }

        // Checked on the way out rather than at construction, and only for what is asked for: an application
        // nobody drives cannot break a run, and failing a whole suite over an entry it never touches would
        // make a fixture's health depend on the entries beside the one it uses.
        if (config.Browser is not { Length: > 0 })
        {
            throw UiConfigurationException.MissingBrowser(identifier);
        }

        return config;
    }

    /// <summary>
    /// Whether an identifier is configured.
    /// </summary>
    /// <param name="identifier">The application identifier.</param>
    /// <returns>True when the store has an entry for it.</returns>
    public bool Contains(string identifier) => this.resolved.ContainsKey(identifier);
}
