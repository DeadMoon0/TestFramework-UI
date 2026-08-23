using System;
using System.Collections.Generic;

namespace TestFramework.UI.Browser.Configuration;

/// <summary>
/// One emulated device: a viewport, and whether the page should believe it is on a phone.
/// </summary>
/// <param name="Width">The viewport width in pixels.</param>
/// <param name="Height">The viewport height in pixels.</param>
/// <param name="IsMobile">Whether the page is told it is on a mobile device.</param>
/// <param name="HasTouch">Whether touch events are available.</param>
/// <param name="DeviceScaleFactor">The device pixel ratio.</param>
public sealed record UiDeviceProfile(int Width, int Height, bool IsMobile, bool HasTouch, float DeviceScaleFactor);

/// <summary>
/// The desktop sizes this package names itself, for the cases Playwright's phone and tablet
/// descriptors do not cover.
/// </summary>
/// <remarks>
/// A configured device name is looked up here first and in Playwright's own descriptor list second, so
/// <c>iPhone 14</c> and <c>Pixel 7</c> work exactly as Playwright defines them while
/// <c>Desktop 1080p</c> means one agreed thing across a suite instead of a viewport somebody typed.
/// </remarks>
public static class UiDeviceProfiles
{
    private static readonly Dictionary<string, UiDeviceProfile> Profiles =
        new Dictionary<string, UiDeviceProfile>(StringComparer.OrdinalIgnoreCase)
        {
            ["Desktop 720p"] = new UiDeviceProfile(1280, 720, false, false, 1f),
            ["Desktop 1080p"] = new UiDeviceProfile(1920, 1080, false, false, 1f),
            ["Desktop 1440p"] = new UiDeviceProfile(2560, 1440, false, false, 1f),
            ["Laptop"] = new UiDeviceProfile(1440, 900, false, false, 2f),
            ["Tablet"] = new UiDeviceProfile(768, 1024, false, true, 2f),

            // Just below the usual breakpoint, for the case a responsive layout is the thing under test
            // and the exact phone does not matter.
            ["Narrow"] = new UiDeviceProfile(390, 844, true, true, 3f),
        };

    /// <summary>
    /// The names this package defines itself.
    /// </summary>
    public static IEnumerable<string> Names => Profiles.Keys;

    /// <summary>
    /// Looks up one of this package's own presets.
    /// </summary>
    /// <param name="name">The device name, matched without regard to case.</param>
    /// <param name="profile">The profile, when the name is one of these presets.</param>
    /// <returns>True when the name is a preset defined here.</returns>
    public static bool TryGet(string name, out UiDeviceProfile? profile)
        => Profiles.TryGetValue(name, out profile);
}
