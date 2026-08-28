using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TestFramework.Core.Exceptions;
using TestFramework.Core.Steps;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Timelines;
using TestFramework.UI.Browser.Configuration;
using TestFramework.UI.Browser.Identifier;
using TestFramework.UI.Web;
using TestFramework.Web;
using TestFramework.Web.Extensions;
using Xunit;
using Xunit.Abstractions;

namespace TestFramework.UI.Browser.Tests.Configuration;

/// <summary>
/// A browser finding the address of an application the TestFramework.Web family configures.
/// </summary>
/// <remarks>
/// <para>
/// The <c>shop</c> entry deliberately has no address of its own. Where its address comes from - a site
/// under its own name, an API it was pointed at, or a differently named entry - is what these prove, and
/// they now prove it through the run's resources rather than through a bridge that read Web's configuration
/// stores directly. The declaration goes in the way a user's would, through Web's own loader, so the
/// section, the store and the graph cannot come to different conclusions about it.
/// </para>
/// <para>
/// Same cases as before the bridge was deleted, with one added: two kinds answering to one name is now a
/// refusal that names both, where registration order used to decide.
/// </para>
/// <para>
/// <strong>Why these live here rather than in <c>TestFramework.UI.Web.Tests</c>.</strong> What they exercise
/// is this package's resolver, so this is the suite that owns them - a suite reaching into another package's
/// internals needed a grant, and a grant between packages is the private handshake the family forbids. The
/// bridging calls arrive the way a user's would, through <c>TestFramework.UI.Web</c>'s public DSL, and what
/// that DSL puts on an identifier is pinned by its own suite, on its own public surface.
/// </para>
/// </remarks>
public class BridgeResolutionTests(ITestOutputHelper output)
{
    [Fact]
    public async Task OwnIdentifierResolvesFromTheSiteSectionWithNoBridgingCall()
    {
        TimelineRun run = await Run(
            new WebAppIdentifier("shop"),
            new WebAppConfig { Browser = "chromium" },
            ("Site:shop:BaseUrl", "http://localhost:39001/"));

        run.EnsureRanToCompletion();

        Assert.Equal("http://localhost:39001/", run.VariableStore.GetVariable<string>("address"));
    }

    [Fact]
    public async Task AnExplicitBaseUrlAlwaysWins()
    {
        TimelineRun run = await Run(
            new WebAppIdentifier("shop"),
            new WebAppConfig { Browser = "chromium", BaseUrl = "https://deployed.example/" },
            ("Site:shop:BaseUrl", "http://localhost:39001/"));

        run.EnsureRanToCompletion();

        Assert.Equal("https://deployed.example/", run.VariableStore.GetVariable<string>("address"));
    }

    [Fact]
    public async Task AnIdentifierInBothSectionsResolvesByTheDeclaredKind()
    {
        // FromSite and FromWebApi both put a kind on the requirement, which is the whole reason they exist
        // rather than a bare name - and it is the only thing that can tell these two entries apart.
        TimelineRun viaSite = await Run(
            new WebAppIdentifier("shop").FromSite("shop"),
            new WebAppConfig { Browser = "chromium" },
            ("Site:shop:BaseUrl", "http://site/"),
            ("Api:shop:BaseUrl", "http://api/"));

        TimelineRun viaApi = await Run(
            new WebAppIdentifier("shop").FromWebApi("shop"),
            new WebAppConfig { Browser = "chromium" },
            ("Site:shop:BaseUrl", "http://site/"),
            ("Api:shop:BaseUrl", "http://api/"));

        viaSite.EnsureRanToCompletion();
        viaApi.EnsureRanToCompletion();

        Assert.Equal("http://site/", viaSite.VariableStore.GetVariable<string>("address"));
        Assert.Equal("http://api/", viaApi.VariableStore.GetVariable<string>("address"));
    }

    [Fact]
    public async Task AnIdentifierInBothSectionsWithoutAKindIsARefusal()
    {
        // New, and the reason the two behaviour changes were worth taking: the old bridge asked its sources
        // in registration order, so which application a browser opened depended on which package registered
        // first. Ambiguity is now a sentence naming both candidates.
        TimelineRun run = await Run(
            new WebAppIdentifier("shop"),
            new WebAppConfig { Browser = "chromium" },
            ("Site:shop:BaseUrl", "http://site/"),
            ("Api:shop:BaseUrl", "http://api/"));

        TimelineRunFailedException failure = Assert.Throws<TimelineRunFailedException>(run.EnsureRanToCompletion);

        Assert.Contains(WebEnvironmentResourceKinds.Site, failure.ToString(), StringComparison.Ordinal);
        Assert.Contains(WebEnvironmentResourceKinds.RestApi, failure.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task BaseUrlFromSiteResolvesADifferentlyNamedSite()
    {
        TimelineRun run = await Run(
            new WebAppIdentifier("shop"),
            new WebAppConfig { Browser = "chromium", BaseUrlFromSite = "shop-ui" },
            ("Site:shop-ui:BaseUrl", "http://localhost:39001/"));

        run.EnsureRanToCompletion();

        Assert.Equal("http://localhost:39001/", run.VariableStore.GetVariable<string>("address"));
    }

    [Fact]
    public async Task BaseUrlFromApiResolvesADifferentlyNamedApi()
    {
        // Named for what it does rather than for a store it reads. Neither of these two members ever
        // restricted the search to a kind - only FromSite and FromWebApi do that - so the old name promised
        // something the code did not deliver; see the debt ledger.
        TimelineRun run = await Run(
            new WebAppIdentifier("shop"),
            new WebAppConfig { Browser = "chromium", BaseUrlFromApi = "shop-api" },
            ("Api:shop-api:BaseUrl", "http://api/"));

        run.EnsureRanToCompletion();

        Assert.Equal("http://api/", run.VariableStore.GetVariable<string>("address"));
    }

    [Fact]
    public async Task NothingAnsweringFailsWithTheAddressError()
    {
        TimelineRun run = await Run(new WebAppIdentifier("shop"), new WebAppConfig { Browser = "chromium" });

        TimelineRunFailedException failure = Assert.Throws<TimelineRunFailedException>(run.EnsureRanToCompletion);

        Assert.Contains("has no address", failure.ToString(), StringComparison.Ordinal);
    }

    private async Task<TimelineRun> Run(WebAppIdentifier app, WebAppConfig entry, params (string Key, string Value)[] configured)
    {
        List<KeyValuePair<string, string?>> entries = [];

        foreach ((string key, string value) in configured)
        {
            entries.Add(new KeyValuePair<string, string?>(key, value));
        }

        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(entries).Build();

        ServiceCollection services = new ServiceCollection();
        services.AddSingleton(configuration);
        services.AddSingleton(new UiConfigStore([new(app.Identifier, entry)]));

        // Both sections, because the point is that a reader does not care which one an identifier came from.
        services.LoadWebConfigs(configuration);
        services.LoadWebSiteConfigs(configuration);

        Timeline timeline = Timeline.Create()
            .Trigger(new ResolvesAppStep(app)).Name("resolves")
            .Build();

        return await timeline.SetupRun(services.BuildServiceProvider(), output).RunAsync();
    }

    /// <summary>Resolves the application the way every browser step now does.</summary>
    private sealed class ResolvesAppStep(WebAppIdentifier app) : Step<EmptyStepResultContext>
    {
        public override string Name => "Resolves an application";

        public override string Description => "Resolves a web application's configuration through the run.";

        public override bool DoesReturn => false;

        public override Step<EmptyStepResultContext> Clone() => new ResolvesAppStep(app).WithClonedOptions(this);

        public override StepInstance<Step<EmptyStepResultContext>, EmptyStepResultContext> GetInstance() => new(this);

        public override void DeclareIO(StepIOContract contract)
        {
        }

        public override Task<EmptyStepResultContext?> Execute(RunContext context)
        {
            context.Variables.SetVariable("address", UiConfigResolver.Resolve(context, app).BaseUrl);

            return Task.FromResult<EmptyStepResultContext?>(EmptyStepResultContext.Instance);
        }
    }
}
