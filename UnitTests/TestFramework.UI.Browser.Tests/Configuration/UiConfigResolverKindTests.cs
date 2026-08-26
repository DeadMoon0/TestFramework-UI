using System;
using Microsoft.Extensions.DependencyInjection;
using TestFramework.Core.Environment;
using TestFramework.UI.Browser.Configuration;
using TestFramework.UI.Browser.Identifier;
using Xunit;

namespace TestFramework.UI.Browser.Tests.Configuration;

/// <summary>
/// Covers how the resolver walks the base-URL sources: an identifier that carries a kind is
/// answered by the matching source, the own-identifier road is the last fallback, and everything
/// stays in opaque strings.
/// </summary>
public class UiConfigResolverKindTests
{
    private sealed class FakeSource(string? kind, string identifier, string? answer) : IUiBaseUrlSource
    {
        public string? ResourceKind => kind;

        public int Asked { get; private set; }

        public bool TryGetBaseUrl(IServiceProvider serviceProvider, string foreignIdentifier, out string? baseUrl)
        {
            Asked++;
            baseUrl = string.Equals(foreignIdentifier, identifier, StringComparison.Ordinal) ? answer : null;
            return baseUrl is not null;
        }
    }

    private static ServiceProvider BuildServices(WebAppConfig entry, params IUiBaseUrlSource[] sources)
    {
        ServiceCollection services = new();
        services.AddSingleton(new UiConfigStore([new("shop", entry)]));
        foreach (IUiBaseUrlSource source in sources)
            services.AddSingleton(source);

        return services.BuildServiceProvider();
    }

    [Fact]
    public void ADeclaredKind_SelectsTheMatchingSource_NotTheFirstRegistered()
    {
        FakeSource wrongKind = new("kind.a", "target", "http://wrong/");
        FakeSource rightKind = new("kind.b", "target", "http://right/");
        using ServiceProvider services = BuildServices(new WebAppConfig { Browser = "chromium" }, wrongKind, rightKind);

        // Bridged the way any caller bridges one. The requirement cannot be assigned from outside the
        // record, which is deliberate: it used to travel beside a second member naming the same resource,
        // and nothing stopped the two from disagreeing.
        WebAppIdentifier identifier = new WebAppIdentifier("shop")
            .BridgedTo(new EnvironmentRequirement("kind.b", "target"));

        WebAppConfig resolved = UiConfigResolver.Resolve(services, identifier);

        Assert.Equal("http://right/", resolved.BaseUrl);
        Assert.Equal(0, wrongKind.Asked);
    }

    [Fact]
    public void ADeclaredKindNobodyServes_FallsBackToTheKindAgnosticSources()
    {
        FakeSource agnostic = new(null, "target", "http://agnostic/");
        using ServiceProvider services = BuildServices(new WebAppConfig { Browser = "chromium" }, agnostic);

        WebAppIdentifier identifier = new WebAppIdentifier("shop")
            .BridgedTo(new EnvironmentRequirement("kind.unknown", "target"));

        Assert.Equal("http://agnostic/", UiConfigResolver.Resolve(services, identifier).BaseUrl);
    }

    [Fact]
    public void WithoutAKind_RegistrationOrderDecides()
    {
        FakeSource first = new("kind.a", "shop", "http://first/");
        FakeSource second = new("kind.b", "shop", "http://second/");
        using ServiceProvider services = BuildServices(new WebAppConfig { Browser = "chromium" }, first, second);

        Assert.Equal("http://first/", UiConfigResolver.Resolve(services, new WebAppIdentifier("shop")).BaseUrl);
    }

    [Fact]
    public void TheOwnIdentifier_IsOnlyAskedWhenNoForeignOneIsDeclared()
    {
        // A declared foreign identifier that nothing answers is a configuration error, not a reason
        // to quietly try the application's own name instead.
        FakeSource ownAnswer = new(null, "shop", "http://own/");
        using ServiceProvider services = BuildServices(new WebAppConfig { Browser = "chromium", BaseUrlFromSite = "missing" }, ownAnswer);

        Assert.Throws<TestFramework.UI.Browser.Exceptions.UiConfigurationException>(
            () => UiConfigResolver.Resolve(services, new WebAppIdentifier("shop")));
    }
}
