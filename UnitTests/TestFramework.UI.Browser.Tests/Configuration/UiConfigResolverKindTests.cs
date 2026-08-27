using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using TestFramework.Core.Environment;
using TestFramework.Core.Environment.Graph;
using TestFramework.Core.Exceptions;
using TestFramework.Core.Steps;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Timelines;
using TestFramework.Core.Timelines.Builder.TimelineRunBuilder;
using TestFramework.UI.Browser.Configuration;
using TestFramework.UI.Browser.Identifier;
using Xunit;
using Xunit.Abstractions;

namespace TestFramework.UI.Browser.Tests.Configuration;

/// <summary>
/// How a browser step finds the address of an application another package owns.
/// </summary>
/// <remarks>
/// <para>
/// It asks the run. Whatever declared the application or started it published the address there, and this
/// package never learns which - which is what let the <c>IUiBaseUrlSource</c> bridge, its two
/// implementations and the rules for which of them got asked first all be deleted.
/// </para>
/// <para>
/// <strong>Two behaviours changed with it, both toward refusing rather than guessing.</strong> A requirement
/// naming a kind nothing published used to fall back to the kind-agnostic sources; there is no such thing as
/// a kind-agnostic resource, so it is now a failure that names what the identifier actually is. And an
/// identifier two kinds answer to used to be settled by registration order; the run refuses and names both.
/// Neither could be kept: they were properties of a registration list, and there is no list any more.
/// </para>
/// </remarks>
public class UiConfigResolverKindTests(ITestOutputHelper output)
{
    [Fact]
    public async Task ADeclaredKindIsAnsweredByThatKindAndNotAnother()
    {
        // The case the bridge got right, kept: naming a kind means that kind, whatever else answers to the
        // same identifier.
        TimelineRun run = await Run(
            new WebAppIdentifier("shop").BridgedTo(new EnvironmentRequirement("kind.b", "target")),
            ("kind.a", "target", "http://wrong/"),
            ("kind.b", "target", "http://right/"));

        run.EnsureRanToCompletion();

        Assert.Equal("http://right/", run.VariableStore.GetVariable<string>("address"));
    }

    [Fact]
    public async Task ADeclaredKindNobodyPublishesIsARefusal()
    {
        // Changed deliberately. The old gate fell back to whatever answered without a kind, which for a
        // browser meant opening some other resource that happened to share the name.
        TimelineRun run = await Run(
            new WebAppIdentifier("shop").BridgedTo(new EnvironmentRequirement("kind.unknown", "target")),
            ("kind.a", "target", "http://something-else/"));

        TimelineRunFailedException failure = Assert.Throws<TimelineRunFailedException>(run.EnsureRanToCompletion);

        Assert.Contains("shop", failure.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task WithoutAKindTwoKindsAnsweringOneNameIsARefusal()
    {
        // Also changed deliberately, and this is the one worth having: registration order decided before, so
        // which application a browser opened depended on the order two packages were registered in.
        TimelineRun run = await Run(
            new WebAppIdentifier("shop"),
            ("kind.a", "shop", "http://first/"),
            ("kind.b", "shop", "http://second/"));

        TimelineRunFailedException failure = Assert.Throws<TimelineRunFailedException>(run.EnsureRanToCompletion);

        // Names both, so the fix is a rename or a kind rather than a guess.
        Assert.Contains("kind.a", failure.ToString(), StringComparison.Ordinal);
        Assert.Contains("kind.b", failure.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheOwnIdentifierIsOnlyAskedWhenNoForeignOneIsDeclared()
    {
        // A declared foreign identifier that nothing answers is a configuration error, not a reason to
        // quietly try the application's own name instead. Unchanged.
        TimelineRun run = await Run(
            new WebAppIdentifier("shop"),
            entry: new WebAppConfig { Browser = "chromium", BaseUrlFromSite = "missing" },
            published: ("kind.a", "shop", "http://own/"));

        Assert.Throws<TimelineRunFailedException>(run.EnsureRanToCompletion);
    }

    private Task<TimelineRun> Run(WebAppIdentifier identifier, params (string Kind, string Identifier, string BaseUrl)[] published)
        => Run(identifier, new WebAppConfig { Browser = "chromium" }, published);

    private Task<TimelineRun> Run(WebAppIdentifier identifier, WebAppConfig entry, (string Kind, string Identifier, string BaseUrl) published)
        => Run(identifier, entry, [published]);

    private async Task<TimelineRun> Run(WebAppIdentifier identifier, WebAppConfig entry, (string Kind, string Identifier, string BaseUrl)[] published)
    {
        ServiceCollection services = new ServiceCollection();
        services.AddSingleton(new UiConfigStore([new(identifier.Identifier, entry)]));

        Timeline timeline = Timeline.Create()
            .Trigger(new ResolvesAppStep(identifier)).Name("resolves")
            .Build();

        ITimelineRunBuilder builder = timeline
            .SetupRun(services.BuildServiceProvider(), output)
            .SetEnv(new PublishingEnvironment(published));

        return await builder.RunAsync();
    }

    /// <summary>Stands in for whatever serves the application: it starts, then it knows its address.</summary>
    private sealed class PublishingEnvironment((string Kind, string Identifier, string BaseUrl)[] published) : EnvironmentProviderBase
    {
        private readonly EnvComponentIdentifier component = "serves";

        public override IReadOnlyCollection<EnvComponentIdentifier> ResolveComponents(
            IEnumerable<Core.Artifacts.ArtifactInstanceGeneric> artifacts,
            IEnumerable<EnvironmentRequirement> requirements)
        {
            this.AddComponent(new PublishingComponent(this.component, published));

            return [this.component];
        }
    }

    private sealed class PublishingComponent(EnvComponentIdentifier id, (string Kind, string Identifier, string BaseUrl)[] published) : EnvComponent
    {
        public override EnvComponentIdentifier Id => id;

        public override Task<object?> CreateAsync(IEnvironmentProvider environment, RunContext context)
        {
            EnvironmentResources resources = PublishOn(context);

            foreach ((string kind, string identifier, string baseUrl) in published)
            {
                // Built here rather than taken from a package's constants, because the point is that these
                // strings stay opaque to the browser package - it never learns whose kind it is holding.
                ResourceKind resourceKind = ResourceKind.Named(kind).OffersPerVantage(ValueNames.BaseUrl).Build();

                resources.Produce(resourceKind, identifier, ValueNames.BaseUrl, ResourceVantage.Host, baseUrl);
            }

            return Task.FromResult<object?>(null);
        }

        public override Task DeconstructAsync(object? state, IEnvironmentProvider environment, RunContext context)
            => Task.CompletedTask;
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
