using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TestFramework.Core.Steps;
using TestFramework.UI.Browser.Configuration;
using TestFramework.UI.Browser.Exceptions;
using TestFramework.UI.Browser.Extensions;
using TestFramework.UI.Browser.Runtime;
using Xunit;

namespace TestFramework.UI.Browser.Tests.Configuration;

/// <summary>
/// What turning this package on registers, and the two opposite guarantees it has to keep.
/// </summary>
/// <remarks>
/// <para>
/// The engine finds a package's run-wide pieces by resolving them from the run's services, so a piece
/// nobody registered never runs. For the failure observer that is silent: the run goes red for the right
/// reason and the evidence folder is simply empty. So declaring applications and registering what drives
/// them are one act, and the store is internal to make the other shape impossible to write.
/// </para>
/// <para>
/// The opposite guarantee matters just as much, and is the easier one to break by accident: a registration
/// that turns everything on must not also nail it shut. Half of these tests are about what a caller can
/// still replace.
/// </para>
/// </remarks>
public class UiBrowserRegistrationTests
{
    [Fact]
    public void DeclaringApplicationsRegistersWhatDrivesThem()
    {
        ServiceCollection services = new ServiceCollection();

        services.AddUiBrowser(apps => apps.Add("shop", new WebAppConfig { Browser = "chromium", BaseUrl = "http://localhost/" }));

        IServiceProvider provider = services.BuildServiceProvider();

        Assert.Equal("http://localhost/", UiConfigResolver.Resolve(RunContext.Detached(provider), "shop").BaseUrl);
        Assert.IsType<UiFailureObserver>(Assert.Single(provider.GetServices<IStepObserver>()));
    }

    [Fact]
    public void TheConfigurationRoadRegistersTheSameThings()
    {
        // .LoadUIConfig() reaches the same one place, so the two roads cannot register different sets.
        ServiceCollection services = new ServiceCollection();
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
            [
                new KeyValuePair<string, string?>("Ui:shop:BaseUrl", "http://localhost/"),
                new KeyValuePair<string, string?>("Ui:shop:Browser", "chromium"),
            ])
            .Build();

        new UiConfigLoader().LoadAllConfigs(configuration, services);

        IServiceProvider provider = services.BuildServiceProvider();

        Assert.Equal("http://localhost/", UiConfigResolver.Resolve(RunContext.Detached(provider), "shop").BaseUrl);
        Assert.IsType<UiFailureObserver>(Assert.Single(provider.GetServices<IStepObserver>()));
    }

    [Fact]
    public void DeclaringApplicationsTwiceIsRefusedRatherThanSilentlyWon()
    {
        // A container hands out the last registration, so the first set of applications would vanish and the
        // failure would name an application the fixture can plainly see.
        ServiceCollection services = new ServiceCollection();
        services.AddUiBrowser(apps => apps.Add("shop", new WebAppConfig { Browser = "chromium", BaseUrl = "http://one/" }));

        UiConfigurationException failure = Assert.Throws<UiConfigurationException>(
            () => services.AddUiBrowser(apps => apps.Add("other", new WebAppConfig { Browser = "chromium", BaseUrl = "http://two/" })));

        Assert.Contains("already registered", failure.Message, StringComparison.Ordinal);
        Assert.Contains("LoadUIConfig", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void OneIdentifierMayBeDeclaredOnce()
    {
        ServiceCollection services = new ServiceCollection();

        UiConfigurationException failure = Assert.Throws<UiConfigurationException>(
            () => services.AddUiBrowser(apps => apps
                .Add("shop", new WebAppConfig { Browser = "chromium", BaseUrl = "http://one/" })
                .Add("shop", new WebAppConfig { Browser = "chromium", BaseUrl = "http://two/" })));

        Assert.Contains("declared twice", failure.Message, StringComparison.Ordinal);
        Assert.Contains("BasedOn", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void NothingConfiguredNamesBothRoadsOut()
    {
        // The message is the whole value of this failure: a reader who never configured anything needs to be
        // told the two ways to, not that a store is missing.
        ServiceCollection services = new ServiceCollection();
        services.AddUiBrowser(apps => apps.Add("shop", new WebAppConfig { Browser = "chromium", BaseUrl = "http://localhost/" }));

        UiConfigurationException failure = Assert.Throws<UiConfigurationException>(
            () => UiConfigResolver.Resolve(RunContext.Detached(services.BuildServiceProvider()), "typo"));

        Assert.Contains("'shop'", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnObserverOfTheCallersOwnJoinsRatherThanFights()
    {
        // Watching a run is not exclusive. A caller who wants their own evidence gathering should not have to
        // give up this package's, and should not silently lose theirs either.
        ServiceCollection services = new ServiceCollection();
        services.AddSingleton<IStepObserver, CountingObserver>();
        services.AddUiBrowser(apps => apps.Add("shop", new WebAppConfig { Browser = "chromium", BaseUrl = "http://localhost/" }));

        IStepObserver[] observers = [.. services.BuildServiceProvider().GetServices<IStepObserver>()];

        Assert.Equal(2, observers.Length);
        Assert.Contains(observers, static observer => observer is CountingObserver);
        Assert.Contains(observers, static observer => observer is UiFailureObserver);
    }

    [Fact]
    public void LoadingTheConfigurationTwiceIsRefusedToo()
    {
        // The same refusal from the other road, and it is what makes duplicate run-wide pieces unreachable
        // rather than merely unlikely: the applications and the pieces go in together, so refusing a second
        // set of applications refuses a second observer with it. Two observers would otherwise mean two
        // evidence folders per failure and, with the browser held open, two holds to release.
        ServiceCollection services = new ServiceCollection();
        IConfiguration configuration = new ConfigurationBuilder().Build();

        new UiConfigLoader().LoadAllConfigs(configuration, services);

        Assert.Throws<UiConfigurationException>(() => new UiConfigLoader().LoadAllConfigs(configuration, services));
        Assert.Single(services.BuildServiceProvider().GetServices<IStepObserver>());
    }

    [Fact]
    public void AnApplicationThatStatesNoBrowserIsRefused()
    {
        // Which browser a test drives decides what it proves, so there is no fallback to fall into. A suite
        // that silently got one would report a pass for something nobody chose to verify - the failure mode
        // a default cannot be told apart from, which is why the value is stated instead.
        ServiceCollection services = new ServiceCollection();
        services.AddUiBrowser(apps => apps.Add("shop", new WebAppConfig { BaseUrl = "http://localhost/" }));

        UiConfigurationException failure = Assert.Throws<UiConfigurationException>(
            () => UiConfigResolver.Resolve(RunContext.Detached(services.BuildServiceProvider()), "shop"));

        Assert.Contains("states no browser", failure.Message, StringComparison.Ordinal);

        // Both roads out, and the options - a reader on a machine with one browser and not another needs to
        // know which, so the message says what is actually here.
        Assert.Contains("Ui:shop:Browser", failure.Message, StringComparison.Ordinal);
        Assert.Contains("BasedOn", failure.Message, StringComparison.Ordinal);
        Assert.Contains("chromium", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnInheritedBrowserCounts()
    {
        // Stating it once and inheriting it is stating it: the refusal is about a value nobody supplied, not
        // about where it was written.
        ServiceCollection services = new ServiceCollection();
        services.AddUiBrowser(apps => apps
            .Add("shop", new WebAppConfig { Browser = "firefox", BaseUrl = "http://localhost/" })
            .Add("shop-mobile", new WebAppConfig { BasedOn = "shop", Device = "Narrow" }));

        Assert.Equal("firefox", UiConfigResolver.Resolve(RunContext.Detached(services.BuildServiceProvider()), "shop-mobile").Browser);
    }

    [Fact]
    public void AChildMayOverrideAnInheritedBrowserWithAnyValue()
    {
        // The bug this change also closed. The merge used to ask "does this differ from the default?", so a
        // child that deliberately named the default value was indistinguishable from one that said nothing -
        // and the parent quietly won. Unset is now null, so an explicit choice is always an explicit choice.
        ServiceCollection services = new ServiceCollection();
        services.AddUiBrowser(apps => apps
            .Add("shop", new WebAppConfig { Browser = "firefox", BaseUrl = "http://localhost/" })
            .Add("shop-chromium", new WebAppConfig { BasedOn = "shop", Browser = "chromium" }));

        Assert.Equal("chromium", UiConfigResolver.Resolve(RunContext.Detached(services.BuildServiceProvider()), "shop-chromium").Browser);
    }

    [Fact]
    public void TheBrowserSeamIsStillReplaceable()
    {
        // The seam this package's own suite lives on: a fake factory instead of a real browser. If turning
        // the package on had made its pieces exclusive, this is the test that would have stopped compiling -
        // which is why it is here rather than assumed.
        ServiceCollection services = new ServiceCollection();
        services.AddUiBrowser(apps => apps.Add("shop", new WebAppConfig { Browser = "chromium", BaseUrl = "http://localhost/" }));
        services.AddSingleton<IUIComponentFactory, ThrowingFactory>();

        Assert.IsType<ThrowingFactory>(services.BuildServiceProvider().GetUIComponentFactory());
    }

    private sealed class CountingObserver : IStepObserver
    {
        public System.Threading.Tasks.Task OnStepStartingAsync(StepObservation observation, RunContext run)
            => System.Threading.Tasks.Task.CompletedTask;

        public System.Threading.Tasks.Task OnStepFailedAsync(StepObservation observation, Exception exception, RunContext run)
            => System.Threading.Tasks.Task.CompletedTask;

        public System.Threading.Tasks.Task OnStepTimedOutAsync(StepObservation observation, RunContext run)
            => System.Threading.Tasks.Task.CompletedTask;
    }

    private sealed class ThrowingFactory : IUIComponentFactory
    {
        public System.Threading.Tasks.Task<UiSession> SessionAsync(
            string app,
            WebAppConfig config,
            UiRunState runState,
            System.Threading.CancellationToken cancellationToken)
            => throw new NotSupportedException("This factory exists to be resolved, not used.");
    }
}
