using System;

namespace TestFramework.UI.Browser.Configuration;

/// <summary>
/// Supplies a web application's address from configuration that belongs to another package.
/// </summary>
/// <remarks>
/// The seam exists so this package never needs to know about the others. An application configured as
/// a REST API for TestFramework.Web is the same application when a browser opens it, and the address
/// should have exactly one home; the bridge package registers an implementation that reads it there.
/// </remarks>
public interface IUiBaseUrlSource
{
    /// <summary>
    /// The environment requirement kind this source answers for, or <see langword="null"/> for a
    /// source that answers for any.
    /// </summary>
    /// <remarks>
    /// With more than one bridge registered, one identifier could exist in several foreign
    /// configurations. An identifier that carries a kind is then answered only by the source that
    /// declares the same kind; the strings stay opaque to this package.
    /// </remarks>
    string? ResourceKind => null;

    /// <summary>
    /// Looks the address up.
    /// </summary>
    /// <param name="serviceProvider">The run's services, holding whatever configuration this source reads.</param>
    /// <param name="foreignIdentifier">The identifier in the other package's configuration.</param>
    /// <param name="baseUrl">The address, when this source knows it.</param>
    /// <returns>True when this source resolved the address.</returns>
    bool TryGetBaseUrl(IServiceProvider serviceProvider, string foreignIdentifier, out string? baseUrl);
}
