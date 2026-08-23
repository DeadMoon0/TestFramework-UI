using System;
using System.Collections.Generic;
using System.Linq;

namespace TestFramework.UI.Browser.Exceptions;

/// <summary>
/// Thrown when a step names an application the configuration does not describe.
/// </summary>
/// <remarks>
/// The message lists the identifiers that <em>are</em> configured, because the mistake is almost always
/// a typo or a missing section, and both are answered by seeing what exists.
/// </remarks>
public sealed class UiConfigurationException : Exception
{
    private UiConfigurationException(string message) : base(message)
    {
    }

    /// <summary>
    /// The configuration knows nothing about this application.
    /// </summary>
    /// <param name="identifier">The identifier a step named.</param>
    /// <param name="knownIdentifiers">The identifiers that are configured.</param>
    /// <returns>The exception.</returns>
    public static UiConfigurationException MissingIdentifier(string identifier, IEnumerable<string> knownIdentifiers)
    {
        List<string> known = knownIdentifiers?.OrderBy(static name => name, StringComparer.Ordinal).ToList() ?? [];

        string knownText = known.Count == 0
            ? "No web application is configured at all. Add a 'Ui' section and call .LoadUIConfig() on the configuration builder."
            : $"Configured applications: {string.Join(", ", known.Select(static name => $"'{name}'"))}.";

        return new UiConfigurationException(
            $"No configuration was found for the web application '{identifier}'. {knownText}");
    }

    /// <summary>
    /// The application is configured, but nothing says where to reach it.
    /// </summary>
    /// <param name="identifier">The identifier.</param>
    /// <returns>The exception.</returns>
    public static UiConfigurationException MissingBaseUrl(string identifier)
        => new UiConfigurationException(
            $"The web application '{identifier}' has no address. Set 'BaseUrl' for it; or configure a site " +
            $"or REST API under the same identifier '{identifier}' and load the bridge with .LoadUIWebBridge() " +
            "(TestFramework.UI.Web package), so the address resolves from there whether a configuration " +
            "entry or a container environment supplied it; or name a differently-named resource with " +
            "'BaseUrlFromSite' / 'BaseUrlFromApi'.");

    /// <summary>
    /// An entry inherits from another that does not exist, or from a chain that loops.
    /// </summary>
    /// <param name="identifier">The entry.</param>
    /// <param name="problem">What is wrong with the chain.</param>
    /// <returns>The exception.</returns>
    public static UiConfigurationException InvalidInheritance(string identifier, string problem)
        => new UiConfigurationException($"The web application '{identifier}' cannot inherit its configuration: {problem}");
}
