using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using TestFramework.UI.Browser.Configuration;

namespace TestFramework.UI.Browser.Runtime;

/// <summary>
/// Creates the live browser session a step drives.
/// </summary>
/// <remarks>
/// The seam a run replaces to change where the browser comes from - a container, a remote grid, a fake
/// in the package's own tests - without any step knowing the difference.
/// </remarks>
internal interface IUIComponentFactory
{
    /// <summary>
    /// The session for an application within one run, created on first use and reused afterwards.
    /// </summary>
    /// <param name="app">The application identifier.</param>
    /// <param name="config">The configuration to create it from.</param>
    /// <param name="runState">The run the session belongs to.</param>
    /// <param name="cancellationToken">Cancels the creation.</param>
    /// <returns>The session.</returns>
    Task<UiSession> SessionAsync(
        string app,
        WebAppConfig config,
        UiRunState runState,
        CancellationToken cancellationToken);
}

/// <summary>
/// Reaches the factory a run uses.
/// </summary>
internal static class UIComponentFactoryExtensions
{
    /// <summary>
    /// The registered factory, or the real one when nothing was registered.
    /// </summary>
    /// <param name="serviceProvider">The run's services.</param>
    /// <returns>The factory.</returns>
    public static IUIComponentFactory GetUIComponentFactory(this IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);

        return serviceProvider.GetService<IUIComponentFactory>() ?? DefaultUIComponentFactory.Instance;
    }
}
