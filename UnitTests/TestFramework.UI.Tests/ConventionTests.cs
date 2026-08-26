using System;
using System.Linq;
using TestFramework.Core.Conventions;
using TestFramework.UI.Session;
using Xunit;
using Xunit.Abstractions;

namespace TestFramework.UI.Tests;

/// <summary>
/// The family's rules, checked against this package rather than trusted to have been followed.
/// </summary>
/// <remarks>
/// These live in Core (<see cref="StepConventions"/>) and every package calls them on its own assembly: a
/// rule only Core's suite enforces is a rule only Core follows. This package ships no steps of its own -
/// it is the technology-neutral half - so the step checks are expected to find nothing and say so.
/// </remarks>
public class ConventionTests(ITestOutputHelper output)
{
    [Fact]
    public void EveryStepInThisPackageClonesItself()
    {
        ConventionReport report = StepConventions.AssertEveryStepClonesItself(typeof(UiSessionPicture).Assembly);

        output.WriteLine(report.ToString());
    }

    [Fact]
    public void FreezingCascadesThroughThisPackagesParts()
    {
        ConventionReport report = StepConventions.AssertFreezingCascades(typeof(UiSessionPicture).Assembly);

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
        // null means, and values that survive one round trip but not the other - and the session picture in
        // this package is written to disk as part of a failure bundle, so it is exactly the shape that would
        // show the seam. Checked against the compiled assembly, because a stray using is invisible in a diff.
        Assert.DoesNotContain(
            "System.Text.Json",
            typeof(UiSessionPicture).Assembly.GetReferencedAssemblies().Select(static reference => reference.Name));
    }

    [Fact]
    public void ThisPackageKeepsItsInternalsToItself()
    {
        // Every package is a stranger to every other. A grant to another package is a private handshake:
        // two packages understand each other and a third cannot join, so what the favoured one may do stops
        // being what any of them may do - and the grant hides the fact that a surface is missing.
        ConventionReport report = StepConventions.AssertNoPackageSeesAnothersInternals(typeof(UiSessionPicture).Assembly);

        output.WriteLine(report.ToString());
    }
}
