using System;
using Microsoft.Extensions.DependencyInjection;
using TestFramework.Core.Steps;
using TestFramework.UI.Browser.Configuration;
using TestFramework.UI.Browser.Extensions;
using TestFramework.UI.Browser.Resolution;

namespace TestFramework.UI.Browser.Tests.Configuration;

/// <summary>
/// What a variant entry takes over from its parent, and what it keeps.
/// </summary>
/// <remarks>
/// <para>
/// The merge itself belongs to <c>TestFramework.Config</c> and is tested there. What these pin is that this
/// package is wired to it, value by value - because the seven values below are exactly the ones this package
/// got wrong when it wrote the merge itself. It asked "does this differ from the default?", which cannot tell
/// a deliberate choice from silence, so a child naming the default lost to its parent without a word.
/// </para>
/// <para>
/// There is one case per value rather than one case for the idea. "A child may override its parent" was
/// twenty-one separate claims and only the address ones were ever pinned, which is how seven of them stayed
/// wrong through several releases.
/// </para>
/// </remarks>
public class WebAppConfigInheritanceTests
{
    [Fact]
    public void AChildOverridesHeadlessWithTheValueThatUsedToLose()
    {
        // Headless defaults to true, so a child asking for true under a headed parent used to run headed.
        Assert.True(Resolve(
            parent: new WebAppConfig { Browser = "chromium", BaseUrl = "http://localhost/", Headless = false },
            child: new WebAppConfig { Headless = true }).Headless);
    }

    [Fact]
    public void AChildOverridesAmbiguityModeWithTheValueThatUsedToLose()
    {
        // The sharpest of the seven: Strict was the compared-against value, so a variant asking to refuse
        // ambiguous matches stayed lenient. A test that asked to be stricter was quietly looser.
        Assert.Equal(
            UiAmbiguityMode.Strict,
            Resolve(
                parent: new WebAppConfig { Browser = "chromium", BaseUrl = "http://localhost/", AmbiguityMode = UiAmbiguityMode.FirstMatch },
                child: new WebAppConfig { AmbiguityMode = UiAmbiguityMode.Strict }).AmbiguityMode);
    }

    [Fact]
    public void AChildMayTurnHttpsErrorsBackOn()
    {
        // This one was not a comparison but a latch - 'this || parent' - so a child of an entry that ignored
        // certificate errors could never ask for them to fail again, whatever it stated.
        Assert.False(Resolve(
            parent: new WebAppConfig { Browser = "chromium", BaseUrl = "http://localhost/", IgnoreHttpsErrors = true },
            child: new WebAppConfig { IgnoreHttpsErrors = false }).IgnoreHttpsErrors);
    }

    [Fact]
    public void AChildOverridesTheTimeoutsWithTheValuesThatUsedToLose()
    {
        WebAppConfig resolved = Resolve(
            parent: new WebAppConfig
            {
                Browser = "chromium",
                BaseUrl = "http://localhost/",
                DefaultActionTimeout = TimeSpan.FromSeconds(60),
                DefaultCompareTimeout = TimeSpan.FromSeconds(60),
                SlowMo = TimeSpan.FromMilliseconds(250),
            },
            child: new WebAppConfig
            {
                DefaultActionTimeout = TimeSpan.FromSeconds(10),
                DefaultCompareTimeout = TimeSpan.FromSeconds(5),
                SlowMo = TimeSpan.Zero,
            });

        Assert.Equal(TimeSpan.FromSeconds(10), resolved.DefaultActionTimeout);
        Assert.Equal(TimeSpan.FromSeconds(5), resolved.DefaultCompareTimeout);
        Assert.Equal(TimeSpan.Zero, resolved.SlowMo);
    }

    [Fact]
    public void AChildOverridesTheTestIdAttributeWithTheValueThatUsedToLose()
        => Assert.Equal(
            "data-testid",
            Resolve(
                parent: new WebAppConfig { Browser = "chromium", BaseUrl = "http://localhost/", TestIdAttribute = "data-qa" },
                child: new WebAppConfig { TestIdAttribute = "data-testid" }).TestIdAttribute);

    [Fact]
    public void WhatAChildLeavesUnsetStillComesFromItsParent()
    {
        // The other half, and the reason inheritance exists: unset is null, so a variant is one line.
        WebAppConfig resolved = Resolve(
            parent: new WebAppConfig
            {
                Browser = "firefox",
                BaseUrl = "http://localhost/",
                Headless = false,
                AmbiguityMode = UiAmbiguityMode.FirstMatch,
                TestIdAttribute = "data-qa",
            },
            child: new WebAppConfig { Device = "Narrow" });

        Assert.Equal("Narrow", resolved.Device);
        Assert.Equal("firefox", resolved.Browser);
        Assert.False(resolved.Headless);
        Assert.Equal(UiAmbiguityMode.FirstMatch, resolved.AmbiguityMode);
        Assert.Equal("data-qa", resolved.TestIdAttribute);
    }

    [Fact]
    public void AnUnsetValueFallsBackToItsDocumentedDefault()
    {
        // Nullable declarations do not mean the run has no answer - the default moved to where the effective
        // value is read, which is what let the declaration say "nobody set this" at all.
        WebAppConfig bare = Resolve(
            parent: new WebAppConfig { Browser = "chromium", BaseUrl = "http://localhost/" },
            child: new WebAppConfig { Device = "Narrow" });

        Assert.Null(bare.Headless);
        Assert.True(bare.EffectiveHeadless);
        Assert.Equal(UiAmbiguityMode.Strict, bare.EffectiveAmbiguityMode);
        Assert.Equal("data-testid", bare.EffectiveTestIdAttribute);
        Assert.Equal(TimeSpan.FromSeconds(10), bare.EffectiveActionTimeout);
        Assert.Equal(TimeSpan.FromSeconds(5), bare.EffectiveCompareTimeout);
        Assert.Equal(TimeSpan.Zero, bare.EffectiveSlowMo);
        Assert.False(bare.EffectiveIgnoreHttpsErrors);
    }

    private static WebAppConfig Resolve(WebAppConfig parent, WebAppConfig child)
    {
        ServiceCollection services = new ServiceCollection();

        services.AddUiBrowser(apps => apps
            .Add("parent", parent)
            .Add("child", child with { BasedOn = "parent" }));

        return UiConfigResolver.Resolve(RunContext.Detached(services.BuildServiceProvider()), "child");
    }
}
