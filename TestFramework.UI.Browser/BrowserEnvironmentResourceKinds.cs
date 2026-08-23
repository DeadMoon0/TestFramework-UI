namespace TestFramework.UI.Browser;

/// <summary>
/// The kinds of resource browser steps need an environment to provide.
/// </summary>
public static class BrowserEnvironmentResourceKinds
{
    /// <summary>
    /// A web application reachable over HTTP, addressed by a <see cref="Identifier.WebAppIdentifier"/>.
    /// </summary>
    public const string WebApp = "ui.webapp";
}
