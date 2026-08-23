using TestFramework.Config.Builder.InstanceBuilder;
using TestFramework.UI.Browser.Configuration;

namespace TestFramework.UI.Browser.Extensions;

/// <summary>
/// Extension methods for loading browser configuration in timeline config builders.
/// </summary>
public static class UiConfigExtension
{
    /// <summary>
    /// Loads the <c>Ui</c> configuration section into the timeline config builder.
    /// </summary>
    /// <param name="builder">The config instance builder.</param>
    /// <returns>The builder for fluent chaining.</returns>
    /// <remarks>
    /// The store is registered even when the section is absent, so the error a step gets names the
    /// missing entry rather than a missing package.
    /// </remarks>
    public static IConfigInstanceBuilder LoadUIConfig(this IConfigInstanceBuilder builder)
    {
        builder.AddService((services, configuration) => new UiConfigLoader().LoadAllConfigs(configuration, services));
        return builder;
    }
}
