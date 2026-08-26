using System;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TestFramework.Core.Conventions;
using TestFramework.Core.Steps;
using TestFramework.UI.Browser.Configuration;
using TestFramework.UI.Browser.Runtime;
using Xunit;
using Xunit.Abstractions;

namespace TestFramework.UI.Browser.Tests;

/// <summary>
/// The family's rules, checked against this package rather than trusted to have been followed.
/// </summary>
/// <remarks>
/// Most of these live in Core (<see cref="StepConventions"/>) and every package calls them on its own
/// assembly: a rule only Core's suite enforces is a rule only Core follows.
/// </remarks>
public class ConventionTests(ITestOutputHelper output)
{
    [Fact]
    public void EveryStepInThisPackageClonesItself()
    {
        // A step that inherits a concrete base class's Clone() runs as that base class and silently loses
        // whatever it added. The compiler only catches the case where the base is abstract, and this
        // package has the most inheritance of any: every verb, wait and inspection sits on a shared base.
        ConventionReport report = StepConventions.AssertEveryStepClonesItself(typeof(BrowserExt).Assembly);

        output.WriteLine(report.ToString());
        Assert.True(report.Checked > 0, "the check found no steps at all, so it proved nothing");
    }

    [Fact]
    public void FreezingCascadesThroughThisPackagesParts()
    {
        ConventionReport report = StepConventions.AssertFreezingCascades(typeof(BrowserExt).Assembly);

        output.WriteLine(report.ToString());
        foreach (string skipped in report.Skipped)
        {
            output.WriteLine($"  skipped {skipped}");
        }
    }

    [Fact]
    public void ThisPackageSerialisesWithOneJsonLibrary()
    {
        // The family picked Newtonsoft.Json. Two libraries mean two sets of attributes, two notions of what
        // null means, and values that survive one round trip but not the other. This package is the one
        // with a real excuse - the browser driver deserialises with the other library, and seven files here
        // used to take it up on that - so the page hands its answers over as text instead and they are
        // parsed like every other payload the family reads. Checked against the compiled assembly, because
        // a stray using is invisible in a diff.
        Assert.DoesNotContain(
            "System.Text.Json",
            typeof(BrowserExt).Assembly.GetReferencedAssemblies().Select(static reference => reference.Name));
    }

    [Fact]
    public void TurningThisPackageOnBringsItsFailureEvidenceWithIt()
    {
        // The engine drives evidence gathering, and it finds an observer by resolving one from the run's
        // services - so an observer nobody registered is an observer that never runs, and the only symptom
        // is an empty folder after a failure. Wiring it into the configuration load is what makes that
        // impossible to half-do: the call a timeline already has to make is the call that registers it.
        IConfiguration configuration = new ConfigurationBuilder().Build();
        ServiceCollection services = new ServiceCollection();

        new UiConfigLoader().LoadAllConfigs(configuration, services);

        IStepObserver observer = Assert.Single(services.BuildServiceProvider().GetServices<IStepObserver>());

        Assert.IsType<UiFailureObserver>(observer);
    }

    [Fact]
    public void ThisPackageKeepsItsInternalsToItself()
    {
        // Every package is a stranger to every other. A grant to another package is a private handshake:
        // two packages understand each other and a third cannot join, so what the favoured one may do stops
        // being what any of them may do - and the grant hides the fact that a surface is missing.
        ConventionReport report = StepConventions.AssertNoPackageSeesAnothersInternals(typeof(BrowserExt).Assembly);

        output.WriteLine(report.ToString());
    }
}
