using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TestFramework.UI.Browser.Extensions;

namespace TestFramework.UI.Browser.Configuration;

/// <summary>
/// Reads the <c>Ui</c> configuration section into the store steps resolve their applications from.
/// </summary>
internal sealed class UiConfigLoader
{
    /// <summary>
    /// Configuration section name for <see cref="WebAppConfig"/> records.
    /// </summary>
    public const string UiSelector = "Ui";

    internal void LoadAllConfigs(IConfiguration configuration, IServiceCollection serviceCollection)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(serviceCollection);

        // Registered even when the section is absent: the resolver then reports "nothing is
        // configured" with its own message instead of "no store exists", which reads like a missing
        // package rather than a missing section.
        List<KeyValuePair<string, WebAppConfig>> entries = [];
        foreach (IConfigurationSection child in configuration.GetSection(UiSelector).GetChildren())
            entries.Add(new KeyValuePair<string, WebAppConfig>(child.Key, child.Get<WebAppConfig>() ?? new WebAppConfig()));

        serviceCollection.AddSingleton(new UiConfigStore(entries));

        // The rest of what the package needs at run time, registered here so that loading the browser
        // configuration and having the browser's failure evidence are one decision rather than two.
        serviceCollection.AddUiBrowser();
    }
}
