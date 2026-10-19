using System;
using System.Collections.Generic;
using System.Linq;
using TestFramework.Core.Environment;
using TestFramework.Core.Environment.Graph;
using TestFramework.UI.Browser.Configuration;
using TestFramework.UI.Browser.Exceptions;
using TestFramework.UI.Browser.Identifier;
using TestFramework.UI.Browser.Runtime;
using TestFramework.Web;
using Xunit;

namespace TestFramework.UI.Browser.Tests.Configuration;

/// <summary>
/// A browser step requires its application and the one resource its address comes from, decided before the
/// run starts - so the environment that declared that resource is the one that starts it, and a second
/// address for one name is refused rather than settled by whichever the resolver asked first.
/// </summary>
public class UiRequirementsTests
{
    private static readonly WebAppIdentifier Shop = new("shop");

    [Fact]
    public void AnApplicationThatStatesNoBrowser_IsRefusedWhileTheRunIsPlanned()
    {
        // Refused in the first browser step, a missing browser cost the environment's start-up and every stage
        // before it; nothing has run yet when the plan is made.
        ResourceGraph resources = Resources(App("shop", new WebAppConfig { BaseUrl = "http://shop/" }));

        UiConfigurationException refusal = Assert.Throws<UiConfigurationException>(() => For(Shop, resources));

        Assert.Contains("'shop'", refusal.Message, StringComparison.Ordinal);
        Assert.Contains("browser", refusal.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ABrandedBuildNamedAsTheBrowser_IsRefusedWhileTheRunIsPlanned()
    {
        ResourceGraph resources = Resources(App("shop", new WebAppConfig { BaseUrl = "http://shop/", Browser = "msedge" }));

        UiConfigurationException refusal = Assert.Throws<UiConfigurationException>(() => For(Shop, resources));

        Assert.Contains("Channel = \"msedge\"", refusal.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnApplicationStatingItsOwnAddress_RequiresOnlyItself()
    {
        ResourceGraph resources = Resources(App("shop", new WebAppConfig { BaseUrl = "http://shop/", Browser = "chromium" }));

        Assert.Equal(["ui.webapp/shop"], Describe(For(Shop, resources)));
    }

    [Fact]
    public void AnApplicationWithoutAnAddress_RequiresTheSiteOfTheSameName_SoTheEnvironmentThatDeclaredItStartsIt()
    {
        // Damage before: the Docker environment had to know "ui.webapp" and treat it as a site; now the
        // browser package applies its own convention and requires the site itself.
        ResourceGraph resources = Resources(App("shop", new WebAppConfig { Browser = "chromium" }), Other(WebResourceKinds.SiteKind, "shop"));

        Assert.Equal(["ui.webapp/shop", "web.site/shop"], Describe(For(Shop, resources)));
    }

    [Fact]
    public void TwoAddressesForOneApplication_AreRefused_InsteadOfTheOwnSilentlyWinning()
    {
        ResourceGraph resources = Resources(App("shop", new WebAppConfig { BaseUrl = "http://shop/", Browser = "chromium" }), Other(WebResourceKinds.SiteKind, "shop"));

        UiConfigurationException refusal = Assert.Throws<UiConfigurationException>(() => For(Shop, resources));

        Assert.Contains("states its own 'BaseUrl'", refusal.Message);
        Assert.Contains("web.site 'shop'", refusal.Message);
    }

    [Fact]
    public void TwoResourcesCarryingTheName_AreRefusedNamingBoth()
    {
        ResourceGraph resources = Resources(
            App("shop", new WebAppConfig { Browser = "chromium" }),
            Other(WebResourceKinds.SiteKind, "shop"),
            Other(WebResourceKinds.RestApiKind, "shop"));

        UiConfigurationException refusal = Assert.Throws<UiConfigurationException>(() => For(Shop, resources));

        Assert.Contains("web.site 'shop'", refusal.Message);
        Assert.Contains("web.restapi 'shop'", refusal.Message);
    }

    [Fact]
    public void AnApplicationWithNoAddressAnywhere_IsRefusedBeforeTheRunStarts()
    {
        ResourceGraph resources = Resources(App("shop", new WebAppConfig { Browser = "chromium" }));

        UiConfigurationException refusal = Assert.Throws<UiConfigurationException>(() => For(Shop, resources));

        Assert.Contains("has no address", refusal.Message);
    }

    [Fact]
    public void ABridge_IsRequiredAlongsideTheApplication()
    {
        ResourceGraph resources = Resources(App("shop", new WebAppConfig { Browser = "chromium" }), Other(WebResourceKinds.SiteKind, "storefront"));
        WebAppIdentifier bridged = Shop.BridgedTo(WebResourceKinds.SiteKind.Requirement("storefront"));

        Assert.Equal(["ui.webapp/shop", "web.site/storefront"], Describe(For(bridged, resources)));
    }

    [Fact]
    public void ABridgeInTheApplicationsOwnEntry_IsRequiredByName()
    {
        ResourceGraph resources = Resources(App("shop", new WebAppConfig { Browser = "chromium", BaseUrlFrom = "storefront" }), Other(WebResourceKinds.SiteKind, "storefront"));

        Assert.Equal(["ui.webapp/shop", "*/storefront"], Describe(For(Shop, resources)));
    }

    [Fact]
    public void ABrowserThisMachineDoesNotHave_IsRefusedWhileTheRunIsPlanned_NamingTheBuildAndTheStaleOne()
    {
        // A pipeline agent found out at the first browser step, after the environment had started; and a cache restored
        // from an older Playwright said "installed" because some chromium was there.
        ResourceGraph resources = Resources(App("shop", new WebAppConfig { BaseUrl = "http://shop/", Browser = "chromium" }));

        UiBrowserNotInstalledException refusal = Assert.Throws<UiBrowserNotInstalledException>(
            () => UiRequirements.For(Shop, resources, new UiRunState(), (_, _) => UiBuildCheck.Missing("chromium_headless_shell-1234", ["chromium_headless_shell-1223"])));

        Assert.Contains("'shop'", refusal.Message, StringComparison.Ordinal);
        Assert.Contains("chromium_headless_shell-1234", refusal.Message, StringComparison.Ordinal);
        Assert.Contains("chromium_headless_shell-1223", refusal.Message, StringComparison.Ordinal);
        Assert.Contains("\"Install\": true", refusal.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AMissingBrowserTheApplicationAskedToInstall_IsPlannedForInstall_NotRefused()
    {
        ResourceGraph resources = Resources(App("shop", new WebAppConfig { BaseUrl = "http://shop/", Browser = "chromium", Install = true }));
        UiRunState runState = new();

        UiRequirements.For(Shop, resources, runState, (_, _) => UiBuildCheck.Missing("chromium_headless_shell-1234", []));

        Assert.Equal("chromium_headless_shell-1234", Assert.Single(runState.PlannedInstalls).Build);
    }

    [Fact]
    public void AHeadlessAndAHeadedApplication_EachGetTheirBuildInstalled()
    {
        // Chromium's headless shell and its full browser are two downloads; noting one per engine installed only one.
        ResourceGraph resources = Resources(
            App("shop", new WebAppConfig { BaseUrl = "http://shop/", Browser = "chromium", Headless = true, Install = true }),
            App("admin", new WebAppConfig { BaseUrl = "http://admin/", Browser = "chromium", Headless = false, Install = true }));
        UiRunState runState = new();

        UiRequirements.For(Shop, resources, runState, (_, headless) => UiBuildCheck.Missing(headless ? "chromium_headless_shell-1234" : "chromium-1234", []));
        UiRequirements.For(new WebAppIdentifier("admin"), resources, runState, (_, headless) => UiBuildCheck.Missing(headless ? "chromium_headless_shell-1234" : "chromium-1234", []));

        Assert.Equal(["chromium-1234", "chromium_headless_shell-1234"], runState.PlannedInstalls.Select(static install => install.Build).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void AHeadlessChromiumInstall_FetchesOnlyTheHeadlessShell()
    {
        Assert.Equal(["install", "--only-shell", "chromium"], new UiPlannedInstall("chromium", Headless: true, "chromium_headless_shell-1234").InstallerArguments);
        Assert.Equal(["install", "chromium"], new UiPlannedInstall("chromium", Headless: false, "chromium-1234").InstallerArguments);
    }

    [Fact]
    public void AnInstalledBrowser_IsNeitherRefusedNorInstalled()
    {
        ResourceGraph resources = Resources(App("shop", new WebAppConfig { BaseUrl = "http://shop/", Browser = "chromium", Install = true }));
        UiRunState runState = new();

        UiRequirements.For(Shop, resources, runState, (_, _) => UiBuildCheck.Installed);

        Assert.Empty(runState.PlannedInstalls);
    }

    [Theory]
    [InlineData(true, "chromium", "chromium_headless_shell")]
    [InlineData(false, "chromium", "chromium")]
    public void TheBuildAskedForFollowsHeadlessness_BecauseChromiumsTwoModesAreTwoDownloads(bool headless, string engine, string build)
    {
        ResourceGraph resources = Resources(App("shop", new WebAppConfig { BaseUrl = "http://shop/", Browser = engine, Headless = headless }));
        bool? askedHeadless = null;

        For(Shop, resources, (_, asked) => { askedHeadless = asked; return UiBuildCheck.Installed; });

        Assert.Equal(headless, askedHeadless);
        Assert.StartsWith(build, PlaywrightBuildsFolderFor(engine, headless), StringComparison.Ordinal);
    }

    [Fact]
    public void InstallTogetherWithABrandedBuild_IsRefused_BecauseOnlyTheMachineInstallsThose()
    {
        ResourceGraph resources = Resources(App("shop", new WebAppConfig { BaseUrl = "http://shop/", Browser = "chromium", Channel = "msedge", Install = true }));

        UiConfigurationException refusal = Assert.Throws<UiConfigurationException>(() => For(Shop, resources));

        Assert.Contains("Channel = \"msedge\"", refusal.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ABrandedBuild_IsLeftToTheLaunch_BecauseWhereItIsInstalledIsTheMachinesBusiness()
    {
        ResourceGraph resources = Resources(App("shop", new WebAppConfig { BaseUrl = "http://shop/", Browser = "chromium", Channel = "msedge" }));

        For(Shop, resources, (_, _) => throw new InvalidOperationException("A branded build must not be looked up in Playwright's cache."));
    }

    private static string PlaywrightBuildsFolderFor(string engine, bool headless)
        => PlaywrightBuilds.Check(engine, headless, new Dictionary<string, IReadOnlyList<string>> { ["chromium"] = ["1"], ["chromium-headless-shell"] = ["1"] }, []).Needed!;

    private static IReadOnlyCollection<EnvironmentRequirement> For(WebAppIdentifier app, ResourceGraph resources)
        => For(app, resources, (_, _) => UiBuildCheck.Installed);

    private static IReadOnlyCollection<EnvironmentRequirement> For(WebAppIdentifier app, ResourceGraph resources, Func<string, bool, UiBuildCheck> checkBuild)
        => UiRequirements.For(app, resources, new UiRunState(), checkBuild);

    private static IResourceNodeSource App(string identifier, WebAppConfig config)
        => new UiApplications([new(identifier, config)]);

    private static IResourceNodeSource Other(ResourceKind kind, string identifier)
        => new OtherResources(kind, identifier);

    private static ResourceGraph Resources(params IResourceNodeSource[] sources)
        => ResourceGraph.Compose(sources);

    private static string[] Describe(IEnumerable<EnvironmentRequirement> requirements)
        => [.. requirements.Select(requirement => $"{requirement.ResourceKind}/{requirement.ResourceIdentifier}")];

    private sealed class OtherResources(ResourceKind kind, string identifier) : DeclaredNodeSource
    {
        public override string SourceName => "another package";

        protected override IEnumerable<DeclaredResource> Declarations
            => [new DeclaredResource { Kind = kind, Identifier = identifier, Values = new Dictionary<ValueKey, string>(), DeclaredBy = this.SourceName }];
    }
}
