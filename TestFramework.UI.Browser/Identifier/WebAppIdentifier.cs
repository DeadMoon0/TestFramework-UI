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
    /// The environment requirement this application declares instead of its own, when a bridge pointed
    /// it at a resource another package provisions.
    /// </summary>
    /// <remarks>
    /// It rides on the identifier because environment requirements are collected while the plan is
    /// built, where there is no service provider to look a mapping up in.
    /// </remarks>
    internal EnvironmentRequirement? ExternalRequirement { get; init; }

    /// <summary>
    /// The foreign identifier whose configuration supplies this application's base address at run time,
    /// when a bridge pointed it at one.
    /// </summary>
    internal string? BaseUrlFromIdentifier { get; init; }
}
