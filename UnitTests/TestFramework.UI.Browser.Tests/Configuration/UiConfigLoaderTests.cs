using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TestFramework.UI.Browser.Configuration;
using TestFramework.UI.Browser.Resolution;
using Xunit;

namespace TestFramework.UI.Browser.Tests.Configuration;

/// <summary>
/// Covers loading the <c>Ui</c> section into the store, which is what .LoadUIConfig() wires up.
/// </summary>
public class UiConfigLoaderTests
{
    private static UiConfigStore Load(params (string Key, string Value)[] values)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(pair => new KeyValuePair<string, string?>(pair.Key, pair.Value)))
            .Build();

        ServiceCollection services = new();
        new UiConfigLoader().LoadAllConfigs(configuration, services);
        return services.BuildServiceProvider().GetRequiredService<UiConfigStore>();
    }

    [Fact]
    public void Load_ReadsEntriesWithTheirValues()
    {
        UiConfigStore store = Load(
            ("Ui:shop:BaseUrl", "http://localhost:5090/"),
            ("Ui:shop:Browser", "firefox"),
            ("Ui:shop:Headless", "false"),
            ("Ui:shop:ViewportWidth", "390"),
            ("Ui:shop:AmbiguityMode", "FirstMatch"));

        WebAppConfig config = store.Get("shop");

        Assert.Equal("http://localhost:5090/", config.BaseUrl);
        Assert.Equal("firefox", config.Browser);
        Assert.False(config.Headless);
        Assert.Equal(390, config.ViewportWidth);
        Assert.Equal(UiAmbiguityMode.FirstMatch, config.AmbiguityMode);
    }

    [Fact]
    public void Load_ResolvesBasedOnInheritance()
    {
        UiConfigStore store = Load(
            ("Ui:shop:BaseUrl", "http://localhost:5090/"),
            ("Ui:shop:Browser", "firefox"),
            ("Ui:shop-mobile:BasedOn", "shop"),
            ("Ui:shop-mobile:Device", "iPhone 14"));

        WebAppConfig mobile = store.Get("shop-mobile");

        Assert.Equal("http://localhost:5090/", mobile.BaseUrl);
        Assert.Equal("firefox", mobile.Browser);
        Assert.Equal("iPhone 14", mobile.Device);
    }

    [Fact]
    public void Load_RegistersAnEmptyStoreWhenTheSectionIsAbsent()
    {
        UiConfigStore store = Load();

        Assert.Empty(store.Identifiers);
    }
}
