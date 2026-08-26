using System;
using System.Linq;
using TestFramework.Core.Conventions;
using TestFramework.UI.Web;
using Xunit;
using Xunit.Abstractions;

namespace TestFramework.UI.Web.Tests;

/// <summary>
/// The family's rules, checked against this package rather than trusted to have been followed.
/// </summary>
/// <remarks>
/// These live in Core (<see cref="StepConventions"/>) and every package calls them on its own assembly: a
/// rule only Core's suite enforces is a rule only Core follows. This package is a bridge - it teaches a
/// browser identifier to take its address from the Web family's configuration - so it ships no steps, and
/// the step checks are expected to find nothing and say so. They stay because the day this package does add
/// one is the day nobody remembers to add the check.
/// </remarks>
public class ConventionTests(ITestOutputHelper output)
{
    [Fact]
    public void EveryStepInThisPackageClonesItself()
    {
        ConventionReport report = StepConventions.AssertEveryStepClonesItself(typeof(UiWebBridgeConfigExtension).Assembly);

        output.WriteLine(report.ToString());
    }

    [Fact]
    public void FreezingCascadesThroughThisPackagesParts()
    {
        ConventionReport report = StepConventions.AssertFreezingCascades(typeof(UiWebBridgeConfigExtension).Assembly);

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
        // null means, and values that survive one round trip but not the other - and a bridge sitting between
        // two packages is precisely where that seam would show. Checked against the compiled assembly,
        // because a stray using is invisible in a diff.
        Assert.DoesNotContain(
            "System.Text.Json",
            typeof(UiWebBridgeConfigExtension).Assembly.GetReferencedAssemblies().Select(static reference => reference.Name));
    }

    [Fact]
    public void ThisPackageKeepsItsInternalsToItself()
    {
        // Every package is a stranger to every other. A grant to another package is a private handshake:
        // two packages understand each other and a third cannot join, so what the favoured one may do stops
        // being what any of them may do - and the grant hides the fact that a surface is missing.
        ConventionReport report = StepConventions.AssertNoPackageSeesAnothersInternals(typeof(UiWebBridgeConfigExtension).Assembly);

        output.WriteLine(report.ToString());
    }
}
