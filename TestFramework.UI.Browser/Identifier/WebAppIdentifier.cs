using System;
using TestFramework.Core.Environment;

namespace TestFramework.UI.Browser.Identifier;

/// <summary>
/// Names the application a browser step drives, rather than its address.
/// </summary>
/// <remarks>
/// The address belongs to configuration, so the same timeline runs against a developer's machine, a
/// container an environment provisioned, or a deployed environment, without a line of it changing.
/// </remarks>
/// <param name="Identifier">The name the configuration uses for this application.</param>
public record WebAppIdentifier(string Identifier)
{
    /// <summary>
    /// Converts an identifier to its name, so it can be used wherever a string is expected.
    /// </summary>
    /// <param name="identifier">The identifier.</param>
    public static implicit operator string(WebAppIdentifier identifier) => identifier.Identifier;

    /// <summary>
    /// Converts a name to an identifier, so a step reads <c>BrowserExt.Session("shop")</c>.
    /// </summary>
    /// <param name="identifier">The name.</param>
    public static implicit operator WebAppIdentifier(string identifier) => new WebAppIdentifier(identifier);

    /// <summary>
    /// Returns the name.
    /// </summary>
    /// <returns>The name.</returns>
    public override string ToString() => this.Identifier;

    /// <summary>
    /// The resource another package provisions that this application is really the front door of, when a
    /// bridge pointed it at one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It rides on the identifier because environment requirements are collected while the plan is built,
    /// where there is no service provider to look a mapping up in. Two things come from it: the requirement
    /// the browser steps declare instead of their own, and the name whose configuration supplies the
    /// address at run time.
    /// </para>
    /// <para>
    /// Settable only through <see cref="BridgedTo"/>, which is why the setter is private. Those two things
    /// used to be two members set side by side, and every one of the four places that set them had to
    /// remember to set them to the same name; nothing checked it, and a pair that disagreed would have
    /// declared a requirement for one resource while reading its address from another.
    /// </para>
    /// </remarks>
    public EnvironmentRequirement? ExternalRequirement { get; private init; }

    /// <summary>
    /// Points this application at a resource another package provisions.
    /// </summary>
    /// <remarks>
    /// The one way to bridge an application, and public so that it is genuinely one way: any package may
    /// teach an identifier where its application really lives, not only the ones this package was built
    /// alongside. A caller names the kind the serving package defines - <c>WebEnvironmentResourceKinds.Site</c>,
    /// say - so no package in between is needed to introduce the two.
    /// </remarks>
    /// <param name="requirement">The resource, by kind and name, that serves this application.</param>
    /// <returns>An identifier bridged to that resource.</returns>
    public WebAppIdentifier BridgedTo(EnvironmentRequirement requirement)
    {
        ArgumentNullException.ThrowIfNull(requirement);

        return this with { ExternalRequirement = requirement };
    }
}
