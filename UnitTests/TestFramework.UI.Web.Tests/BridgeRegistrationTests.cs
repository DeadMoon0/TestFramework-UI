using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using TestFramework.UI.Browser.Configuration;
using Xunit;

namespace TestFramework.UI.Web.Tests;

/// <summary>
/// What registering the bridge twice leaves behind, and what a caller can still add to it.
/// </summary>
/// <remarks>
/// The bridge has no single object to refuse a second registration the way the browser package's
/// application store does, so it carries the guarantee itself: its sources are registered by implementation
/// type, once each, however many times it is asked.
/// </remarks>
public class BridgeRegistrationTests
{
    [Fact]
    public void RegisteringTheBridgeTwiceLeavesOneOfEachSource()
    {
        // Reachable: LoadUIWebBridge() registers them and a fixture assembling services by hand may too.
        // Two copies would answer an application's address from the same store twice, and the ambiguity
        // report would then read as though two candidates existed.
        ServiceCollection services = new ServiceCollection();

        services.AddUiWebBridge();
        services.AddUiWebBridge();

        Assert.Equal(2, services.BuildServiceProvider().GetServices<IUiBaseUrlSource>().Count());
    }

    [Fact]
    public void ASourceOfTheCallersOwnJoinsTheList()
    {
        // IUiBaseUrlSource is a seam, not a slot: where an address comes from is exactly the kind of thing a
        // caller is meant to be able to answer for themselves.
        ServiceCollection services = new ServiceCollection();

        services.AddSingleton<IUiBaseUrlSource, FixedSource>();
        services.AddUiWebBridge();

        IUiBaseUrlSource[] sources = [.. services.BuildServiceProvider().GetServices<IUiBaseUrlSource>()];

        Assert.Equal(3, sources.Length);
        Assert.Contains(sources, static source => source is FixedSource);
    }

    private sealed class FixedSource : IUiBaseUrlSource
    {
        public bool TryGetBaseUrl(IServiceProvider serviceProvider, string foreignIdentifier, out string? baseUrl)
        {
            baseUrl = "http://fixed/";

            return true;
        }
    }
}
