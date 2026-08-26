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
    /// The same application was declared twice while registering them in code.
    /// </summary>
    /// <remarks>
    /// Refused rather than overwritten: a variant of an existing application is its own identifier with
    /// <c>BasedOn</c> pointing at the original, and silently keeping the second declaration is how a suite
    /// ends up driving an application nobody meant to configure.
    /// </remarks>
    /// <param name="identifier">The identifier declared twice.</param>
    /// <returns>The exception.</returns>
    public static UiConfigurationException DuplicateApplication(string identifier)
        => new UiConfigurationException(
            $"The web application '{identifier}' is declared twice. Each identifier may be declared once; "
            + "a variant of an existing application is a separate identifier with 'BasedOn' set to it.");

    /// <summary>
    /// Applications were registered on this service collection twice, by two different roads.
    /// </summary>
    /// <remarks>
    /// A container hands out whichever registration came last, so the other set of applications would
    /// disappear without a word - and the test that then failed would name an application its own fixture
    /// can plainly see.
    /// </remarks>
    /// <returns>The exception.</returns>
    public static UiConfigurationException ApplicationsAlreadyRegistered()
        => new UiConfigurationException(
            "Web applications are already registered on this service collection. Declare them once: either "
            + "in the 'Ui' configuration section with .LoadUIConfig(), or in code with AddUiBrowser(...), "
            + "and not both.");

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
            ? "No web application is configured at all. Either add a 'Ui' section and call .LoadUIConfig() "
              + "on the configuration builder, or declare them in code with services.AddUiBrowser(apps => "
              + "apps.Add(\"name\", new WebAppConfig { ... }))."
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
