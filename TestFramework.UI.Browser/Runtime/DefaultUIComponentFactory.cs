using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Playwright;
using TestFramework.UI.Browser.Configuration;

namespace TestFramework.UI.Browser.Runtime;

/// <summary>
/// Creates sessions on a real local browser.
/// </summary>
internal sealed class DefaultUIComponentFactory : IUIComponentFactory
{
    /// <summary>The instance a run uses when it registered no other factory.</summary>
    public static DefaultUIComponentFactory Instance { get; } = new DefaultUIComponentFactory();

    private DefaultUIComponentFactory()
    {
    }

    /// <inheritdoc />
    public Task<UiSession> SessionAsync(
        string app,
        WebAppConfig config,
        UiRunState runState,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(runState);

        return runState.SessionAsync(app, () => CreateAsync(app, config, cancellationToken), cancellationToken);
    }

    private static async Task<UiSession> CreateAsync(string app, WebAppConfig config, CancellationToken cancellationToken)
    {
        IBrowser browser = await PlaywrightHost.BrowserAsync(config, cancellationToken).ConfigureAwait(false);
        IPlaywright driver = await PlaywrightHost.DriverAsync(cancellationToken).ConfigureAwait(false);

        BrowserNewContextOptions options = await BuildContextOptionsAsync(driver, config).ConfigureAwait(false);
        IBrowserContext context = await browser.NewContextAsync(options).ConfigureAwait(false);

        // One place to set the per-action limit, so every interaction fails inside the step's own budget
        // and the failure names the action rather than the step that timed out around it.
        context.SetDefaultTimeout((float)config.DefaultActionTimeout.TotalMilliseconds);

        return await UiSession.CreateAsync(app, config, context).ConfigureAwait(false);
    }

    private static Task<BrowserNewContextOptions> BuildContextOptionsAsync(IPlaywright driver, WebAppConfig config)
    {
        BrowserNewContextOptions options = new BrowserNewContextOptions
        {
            BaseURL = config.BaseUrl,
            IgnoreHTTPSErrors = config.IgnoreHttpsErrors,
            Locale = config.Locale,
            ColorScheme = ParseColorScheme(config.ColorScheme),
        };

        ApplyDevice(driver, config, options);

        // Explicit values win over whatever the device says, so a test can emulate a phone at an unusual
        // size without inventing a device for it.
        if (config.ViewportWidth is { } width && config.ViewportHeight is { } height)
        {
            options.ViewportSize = new ViewportSize { Width = width, Height = height };
        }

        if (config.UserAgent is { Length: > 0 })
        {
            options.UserAgent = config.UserAgent;
        }

        if (config.IsMobile is { } isMobile)
        {
            options.IsMobile = isMobile;
        }

        if (config.HasTouch is { } hasTouch)
        {
            options.HasTouch = hasTouch;
        }

        if (config.DeviceScaleFactor is { } scale)
        {
            options.DeviceScaleFactor = scale;
        }

        return Task.FromResult(options);
    }

    private static void ApplyDevice(IPlaywright driver, WebAppConfig config, BrowserNewContextOptions options)
    {
        if (config.Device is not { Length: > 0 } device)
        {
            return;
        }

        // This package's own desktop presets first, so "Desktop 1080p" means one agreed thing across a
        // suite; then Playwright's descriptors, so "iPhone 14" keeps meaning exactly what Playwright says.
        if (UiDeviceProfiles.TryGet(device, out UiDeviceProfile? profile) && profile is not null)
        {
            options.ViewportSize = new ViewportSize { Width = profile.Width, Height = profile.Height };
            options.IsMobile = profile.IsMobile;
            options.HasTouch = profile.HasTouch;
            options.DeviceScaleFactor = profile.DeviceScaleFactor;

            return;
        }

        if (driver.Devices.TryGetValue(device, out BrowserNewContextOptions? descriptor) && descriptor is not null)
        {
            options.ViewportSize = descriptor.ViewportSize;
            options.UserAgent = descriptor.UserAgent;
            options.IsMobile = descriptor.IsMobile;
            options.HasTouch = descriptor.HasTouch;
            options.DeviceScaleFactor = descriptor.DeviceScaleFactor;

            return;
        }

        throw new ArgumentException(
            $"Unknown device '{device}'. Use one of this package's presets ({string.Join(", ", UiDeviceProfiles.Names)}) " +
            "or a device Playwright defines, such as 'iPhone 14' or 'Pixel 7'.",
            nameof(config));
    }

    private static ColorScheme? ParseColorScheme(string? colorScheme)
        => colorScheme?.ToLowerInvariant() switch
        {
            null or "" => null,
            "light" => ColorScheme.Light,
            "dark" => ColorScheme.Dark,
            "no-preference" or "nopreference" => ColorScheme.NoPreference,
            _ => throw new ArgumentException(
                $"Unknown colour scheme '{colorScheme}'. Use 'light', 'dark' or 'no-preference'.",
                nameof(colorScheme)),
        };
}
