using System;
using System.Collections.Generic;
using System.Linq;
using TestFramework.UI.Browser.Exceptions;

namespace TestFramework.UI.Browser.Configuration;

/// <summary>
/// The configured web applications of a run, resolved by identifier.
/// </summary>
/// <remarks>
/// An entry may inherit from another, so a mobile variant of an application is one line rather than a
/// copy of every setting. Inheritance is resolved on read and the result cached, because a run asks for
/// the same identifier once per step.
/// </remarks>
public sealed class UiConfigStore
{
    private readonly Dictionary<string, WebAppConfig> declared;
    private readonly Dictionary<string, WebAppConfig> resolved = new Dictionary<string, WebAppConfig>(StringComparer.OrdinalIgnoreCase);
    private readonly object syncRoot = new object();

    /// <summary>
    /// Creates the store.
    /// </summary>
    /// <param name="configs">The declared entries, keyed by identifier.</param>
    public UiConfigStore(IEnumerable<KeyValuePair<string, WebAppConfig>> configs)
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
            return config;
        }

        if (!this.declared.ContainsKey(parentIdentifier))
        {
            throw UiConfigurationException.InvalidInheritance(
                identifier,
                $"'BasedOn' names '{parentIdentifier}', which is not configured.");
        }

        chain.Add(identifier);

        return config.InheritFrom(this.Resolve(parentIdentifier, chain));
    }
}
