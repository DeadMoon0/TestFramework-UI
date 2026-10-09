using System;
using System.Collections.Generic;
using TestFramework.UI.Browser.Configuration;
using TestFramework.UI.Browser.Exceptions;
using TestFramework.UI.Browser.Resolution;
using Xunit;

namespace TestFramework.UI.Browser.Tests.Configuration;

/// <summary>
/// A value that does not read as its setting is refused naming the application, the setting, what it held and what it
/// takes.
/// </summary>
/// <remarks>
/// Any resource source may declare a <c>ui.webapp</c> resource by hand, and a malformed value used to surface as a bare
/// <see cref="FormatException"/> naming none of those - the one fact a reader needs is which entry to open.
/// </remarks>
public sealed class WebAppValuesTests
{
    [Theory]
    [InlineData("Headless", "maybe", "true or false")]
    [InlineData("ViewportWidth", "wide", "a whole number")]
    [InlineData("DeviceScaleFactor", "big", "a number")]
    [InlineData("SlowMo", "250ms", "a time span")]
    [InlineData("AmbiguityMode", "Whatever", "one of ")]
    public void AValueThatDoesNotRead_IsRefusedNamingWhereItIsAndWhatItTakes(string setting, string text, string takes)
    {
        UiConfigurationException refusal = Assert.Throws<UiConfigurationException>(
            () => WebAppValues.Read(new Dictionary<string, string> { [setting] = text }, "shop"));

        Assert.Contains("'shop'", refusal.Message, StringComparison.Ordinal);
        Assert.Contains($"'{text}' for '{setting}'", refusal.Message, StringComparison.Ordinal);
        Assert.Contains(takes, refusal.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnEnumSettingNamesItsMembers()
    {
        UiConfigurationException refusal = Assert.Throws<UiConfigurationException>(
            () => WebAppValues.Read(new Dictionary<string, string> { ["WidgetCapture"] = "Sometimes" }, "shop"));

        foreach (string member in Enum.GetNames<UiWidgetCapture>())
            Assert.Contains(member, refusal.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void WellFormedValuesStillRead()
    {
        WebAppConfig read = WebAppValues.Read(new Dictionary<string, string>
        {
            ["Headless"] = "false",
            ["ViewportWidth"] = "390",
            ["DeviceScaleFactor"] = "2.5",
            ["SlowMo"] = "00:00:00.2500000",
            ["AmbiguityMode"] = nameof(UiAmbiguityMode.FirstMatch),
        }, "shop");

        Assert.Equal((false, 390, 2.5f, TimeSpan.FromMilliseconds(250), UiAmbiguityMode.FirstMatch),
            (read.Headless, read.ViewportWidth, read.DeviceScaleFactor, read.SlowMo, read.AmbiguityMode));
    }
}
