using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TestFramework.Core.Environment.Graph;
using TestFramework.UI.Browser.Configuration;
using TestFramework.UI.Browser.Resolution;
using Xunit;

namespace TestFramework.UI.Browser.Tests.Configuration;

/// <summary>
/// Covers loading the <c>Ui</c> section into the run's declared resources, which is what .LoadUIConfig()
/// wires up.
/// </summary>
public class UiConfigLoaderTests
{
    private static UiApplications Load(params (string Key, string Value)[] values)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(pair => new KeyValuePair<string, string?>(pair.Key, pair.Value)))
            .Build();

        ServiceCollection services = new();
        new UiConfigLoader().LoadAllConfigs(configuration, services);
        ServiceProvider provider = services.BuildServiceProvider();

        // Declared as a resource source, so the run composes it into its resource list.
        Assert.Contains(provider.GetServices<IResourceNodeSource>(), source => source is UiApplications);
        return provider.GetRequiredService<UiApplications>();
    }

    /// <summary>
    /// What a step reads for the application: its declared values, as the host sees them.
    /// </summary>
    private static WebAppConfig Get(UiApplications applications, string identifier)
    {
        ResourceNode node = Assert.Single(applications.Nodes, candidate => candidate.Identifier == identifier);
        Dictionary<string, string> values = [];
        foreach ((ValueKey key, string value) in node.DeclaredValues)
        {
            if (key.Vantage is null or ResourceVantage.Host)
            {
                values[key.ValueName] = value;
            }
        }

        return WebAppValues.Read(values);
    }

    [Fact]
    public void Load_ReadsEntriesWithTheirValues()
    {
        UiApplications applications = Load(
            ("Ui:shop:BaseUrl", "http://localhost:5090/"),
            ("Ui:shop:Browser", "firefox"),
            ("Ui:shop:Headless", "false"),
            ("Ui:shop:ViewportWidth", "390"),
            ("Ui:shop:AmbiguityMode", "FirstMatch"));

        WebAppConfig config = Get(applications, "shop");

        Assert.Equal("http://localhost:5090/", config.BaseUrl);
        Assert.Equal("firefox", config.Browser);
        Assert.False(config.Headless);
        Assert.Equal(390, config.ViewportWidth);
        Assert.Equal(UiAmbiguityMode.FirstMatch, config.AmbiguityMode);
    }

    [Fact]
    public void Load_ResolvesBasedOnInheritance()
    {
        UiApplications applications = Load(
            ("Ui:shop:BaseUrl", "http://localhost:5090/"),
            ("Ui:shop:Browser", "firefox"),
            ("Ui:shop-mobile:BasedOn", "shop"),
            ("Ui:shop-mobile:Device", "iPhone 14"));

        WebAppConfig mobile = Get(applications, "shop-mobile");

        Assert.Equal("http://localhost:5090/", mobile.BaseUrl);
        Assert.Equal("firefox", mobile.Browser);
        Assert.Equal("iPhone 14", mobile.Device);
    }

    [Fact]
    public void Load_DeclaresNothingWhenTheSectionIsAbsent()
    {
        UiApplications applications = Load();

        Assert.Empty(applications.Nodes);
    }

    [Fact]
    public void EverySetting_SurvivesTheTripThroughTheRunsValues()
    {
        // Damage without it: a setting written under a name the reader does not read is one nothing can ever
        // see - the browser would silently run with the default instead.
        WebAppConfig stated = new()
        {
            BaseUrl = "http://localhost:5090/",
            BaseUrlFromApi = "api",
            BaseUrlFromSite = "site",
            Browser = "firefox",
            Channel = "msedge",
            Headless = false,
            Device = "iPhone 14",
            ViewportWidth = 390,
            ViewportHeight = 844,
            UserAgent = "probe",
            IsMobile = true,
            HasTouch = true,
            DeviceScaleFactor = 2.5f,
            Locale = "de-DE",
            ColorScheme = "dark",
            SlowMo = TimeSpan.FromMilliseconds(250),
            DefaultActionTimeout = TimeSpan.FromSeconds(7),
            DefaultCompareTimeout = TimeSpan.FromSeconds(3),
            TestIdAttribute = "data-qa",
            AmbiguityMode = UiAmbiguityMode.FirstMatch,
            IgnoreHttpsErrors = true,
            WidgetCapture = UiWidgetCapture.EveryAction,
        };

        WebAppConfig read = Get(new UiApplications([new("shop", stated)]), "shop");

        foreach (System.Reflection.PropertyInfo property in typeof(WebAppConfig).GetProperties()
            .Where(property => property.CanWrite && property.Name != nameof(WebAppConfig.BasedOn)))
        {
            Assert.True(Equals(property.GetValue(stated), property.GetValue(read)), $"{property.Name} did not survive: stated {property.GetValue(stated)}, read {property.GetValue(read)}.");
        }
    }

    [Fact]
    public void Identifiers_AreCaseSensitive_LikeEveryOtherResourceName()
    {
        UiApplications applications = Load(("Ui:Shop:Browser", "firefox"));

        Assert.Equal(["Shop"], applications.Nodes.Select(node => node.Identifier));
    }
}
