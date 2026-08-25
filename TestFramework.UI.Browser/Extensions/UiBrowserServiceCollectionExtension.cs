using System;
using Microsoft.Extensions.DependencyInjection;
using TestFramework.Core.Steps;
using TestFramework.UI.Browser.Runtime;

namespace TestFramework.UI.Browser.Extensions;

/// <summary>
/// Registers what browser steps need beyond their configuration.
/// </summary>
/// <remarks>
/// Called by <see cref="UiConfigExtension.LoadUIConfig"/>, so a timeline that loads this package's
/// configuration gets this too and nobody has to remember both. It is public for the same reason the Web
/// bridge's registration is: a fixture that assembles its own service collection - which is what a suite
/// testing this package's internals does - needs to be able to turn the package on the same way, rather
/// than half on.
/// </remarks>
public static class UiBrowserServiceCollectionExtension
{
    /// <summary>
    /// Adds the browser package's run-wide services.
    /// </summary>
    /// <param name="services">The collection to add to.</param>
    /// <returns>The collection, for chaining.</returns>
    public static IServiceCollection AddUiBrowser(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // The engine drives it. A run that never opens a browser is told about every step of it and does
        // nothing, which is cheaper than asking every step to decide whether it is worth watching.
        services.AddSingleton<IStepObserver, UiFailureObserver>();

        return services;
    }
}
