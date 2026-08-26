using System;
using System.Collections.Generic;
using System.Linq;
using TestFramework.UI.Browser.Exceptions;

namespace TestFramework.UI.Browser.Configuration;

/// <summary>
/// The configured web applications of a run, resolved by identifier.
/// </summary>
/// <remarks>
/// <para>
/// An entry may inherit from another, so a mobile variant of an application is one line rather than a
/// copy of every setting. Inheritance is resolved on read and the result cached, because a run asks for
/// the same identifier once per step.
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
    private readonly Dictionary<string, WebAppConfig> declared;
    private readonly Dictionary<string, WebAppConfig> resolved = new Dictionary<string, WebAppConfig>(StringComparer.OrdinalIgnoreCase);
    private readonly object syncRoot = new object();

    /// <summary>
    /// Creates the store.
    /// </summary>
    /// <param name="configs">The declared entries, keyed by identifier.</param>
    internal UiConfigStore(IEnumerable<KeyValuePair<string, WebAppConfig>> configs)
    {
        ArgumentNullException.ThrowIfNull(configs);

        this.declared = new Dictionary<string, WebAppConfig>(StringComparer.OrdinalIgnoreCase);

        foreach ((string identifier, WebAppConfig config) in configs)
        {
            this.declared[identifier] = config;
        }
    }

    /// <summary>The identifiers this store knows.</summary>
    public IEnumerable<string> Identifiers => this.declared.Keys;

    /// <summary>
    /// The configuration of one application, with everything it inherits already filled in.
    /// </summary>
    /// <param name="identifier">The application identifier.</param>
    /// <returns>The configuration.</returns>
    /// <exception cref="UiConfigurationException">Nothing is configured under that identifier, or its
    /// inheritance chain is broken.</exception>
    public WebAppConfig Get(string identifier)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identifier);

        lock (this.syncRoot)
        {
            if (this.resolved.TryGetValue(identifier, out WebAppConfig? cached))
            {
                return cached;
            }

            WebAppConfig result = this.Resolve(identifier, []);
            this.resolved[identifier] = result;

            return result;
        }
    }

    /// <summary>
    /// Whether an identifier is configured.
    /// </summary>
    /// <param name="identifier">The application identifier.</param>
    /// <returns>True when the store has an entry for it.</returns>
    public bool Contains(string identifier) => this.declared.ContainsKey(identifier);

    private WebAppConfig Resolve(string identifier, List<string> chain)
    {
        if (chain.Contains(identifier, StringComparer.OrdinalIgnoreCase))
        {
            throw UiConfigurationException.InvalidInheritance(
                identifier,
                $"'BasedOn' loops: {string.Join(" -> ", chain.Append(identifier))}.");
        }

        if (!this.declared.TryGetValue(identifier, out WebAppConfig? config))
        {
            throw UiConfigurationException.MissingIdentifier(identifier, this.declared.Keys);
        }

        if (config.BasedOn is not { Length: > 0 } parentIdentifier)
        {
            return Complete(identifier, config);
        }

        if (!this.declared.ContainsKey(parentIdentifier))
        {
            throw UiConfigurationException.InvalidInheritance(
                identifier,
                $"'BasedOn' names '{parentIdentifier}', which is not configured.");
        }

        chain.Add(identifier);

        return Complete(identifier, config.InheritFrom(this.Resolve(parentIdentifier, chain)));
    }

    /// <summary>
    /// Checks that a resolved entry states what may not be assumed.
    /// </summary>
    /// <remarks>
    /// Here rather than earlier, and here rather than later. Not at registration, because inheritance is
    /// what an entry may be getting its browser from and that is only resolved now; not in the step that
    /// starts a browser, because by then the same entry has already been reported missing an address from
    /// this very method - one entry's problems should be reported from one place, in one kind of exception.
    /// </remarks>
    /// <param name="identifier">The application being resolved.</param>
    /// <param name="config">Its configuration, with inheritance applied.</param>
    /// <returns>The configuration.</returns>
    private static WebAppConfig Complete(string identifier, WebAppConfig config)
    {
        if (config.Browser is not { Length: > 0 })
        {
            throw UiConfigurationException.MissingBrowser(identifier);
        }

        return config;
    }
}
