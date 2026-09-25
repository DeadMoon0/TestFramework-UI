using System;
using System.Collections.Generic;
using System.Globalization;

using TestFramework.Core.Environment.Graph;
using TestFramework.UI.Browser.Resolution;

namespace TestFramework.UI.Browser.Configuration;

/// <summary>
/// A web application's configuration as resource values, and back.
/// </summary>
/// <remarks>
/// Both directions live on one type on purpose: they have to agree about every value name and every format,
/// and a value written under a name the reader does not read is a setting nothing can ever see. Only what an
/// entry states becomes a value, so an unset setting keeps the default <see cref="WebAppConfig"/> documents.
/// </remarks>
internal static class WebAppValues
{
    public static IReadOnlyDictionary<ValueKey, string> From(WebAppConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        Dictionary<ValueKey, string> values = [];

        // A written address reads the same from the test process and from inside a network: it is the
        // author's description of where the application is.
        if (config.BaseUrl is { Length: > 0 } baseUrl)
        {
            values[new ValueKey(ValueNames.BaseUrl, ResourceVantage.Host)] = baseUrl;
            values[new ValueKey(ValueNames.BaseUrl, ResourceVantage.Network)] = baseUrl;
        }

        Add(values, nameof(WebAppConfig.BaseUrlFromApi), config.BaseUrlFromApi);
        Add(values, nameof(WebAppConfig.BaseUrlFromSite), config.BaseUrlFromSite);
        Add(values, nameof(WebAppConfig.Browser), config.Browser);
        Add(values, nameof(WebAppConfig.Channel), config.Channel);
        Add(values, nameof(WebAppConfig.Headless), Format(config.Headless));
        Add(values, nameof(WebAppConfig.Device), config.Device);
        Add(values, nameof(WebAppConfig.ViewportWidth), config.ViewportWidth?.ToString(CultureInfo.InvariantCulture));
        Add(values, nameof(WebAppConfig.ViewportHeight), config.ViewportHeight?.ToString(CultureInfo.InvariantCulture));
        Add(values, nameof(WebAppConfig.UserAgent), config.UserAgent);
        Add(values, nameof(WebAppConfig.IsMobile), Format(config.IsMobile));
        Add(values, nameof(WebAppConfig.HasTouch), Format(config.HasTouch));
        Add(values, nameof(WebAppConfig.DeviceScaleFactor), config.DeviceScaleFactor?.ToString("R", CultureInfo.InvariantCulture));
        Add(values, nameof(WebAppConfig.Locale), config.Locale);
        Add(values, nameof(WebAppConfig.ColorScheme), config.ColorScheme);
        Add(values, nameof(WebAppConfig.SlowMo), Format(config.SlowMo));
        Add(values, nameof(WebAppConfig.DefaultActionTimeout), Format(config.DefaultActionTimeout));
        Add(values, nameof(WebAppConfig.DefaultCompareTimeout), Format(config.DefaultCompareTimeout));
        Add(values, nameof(WebAppConfig.TestIdAttribute), config.TestIdAttribute);
        Add(values, nameof(WebAppConfig.AmbiguityMode), config.AmbiguityMode?.ToString());
        Add(values, nameof(WebAppConfig.IgnoreHttpsErrors), Format(config.IgnoreHttpsErrors));
        Add(values, nameof(WebAppConfig.WidgetCapture), config.WidgetCapture?.ToString());

        return values;
    }

    /// <summary>
    /// The configuration those values amount to. The address is left out: where it comes from is the
    /// resolver's decision, because it may belong to another resource.
    /// </summary>
    public static WebAppConfig Read(IReadOnlyDictionary<string, string> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        return new WebAppConfig
        {
            BaseUrl = Get(values, ValueNames.BaseUrl),
            BaseUrlFromApi = Get(values, nameof(WebAppConfig.BaseUrlFromApi)),
            BaseUrlFromSite = Get(values, nameof(WebAppConfig.BaseUrlFromSite)),
            Browser = Get(values, nameof(WebAppConfig.Browser)),
            Channel = Get(values, nameof(WebAppConfig.Channel)),
            Headless = ParseBool(values, nameof(WebAppConfig.Headless)),
            Device = Get(values, nameof(WebAppConfig.Device)),
            ViewportWidth = ParseInt(values, nameof(WebAppConfig.ViewportWidth)),
            ViewportHeight = ParseInt(values, nameof(WebAppConfig.ViewportHeight)),
            UserAgent = Get(values, nameof(WebAppConfig.UserAgent)),
            IsMobile = ParseBool(values, nameof(WebAppConfig.IsMobile)),
            HasTouch = ParseBool(values, nameof(WebAppConfig.HasTouch)),
            DeviceScaleFactor = Get(values, nameof(WebAppConfig.DeviceScaleFactor)) is { } factor ? float.Parse(factor, CultureInfo.InvariantCulture) : null,
            Locale = Get(values, nameof(WebAppConfig.Locale)),
            ColorScheme = Get(values, nameof(WebAppConfig.ColorScheme)),
            SlowMo = ParseTime(values, nameof(WebAppConfig.SlowMo)),
            DefaultActionTimeout = ParseTime(values, nameof(WebAppConfig.DefaultActionTimeout)),
            DefaultCompareTimeout = ParseTime(values, nameof(WebAppConfig.DefaultCompareTimeout)),
            TestIdAttribute = Get(values, nameof(WebAppConfig.TestIdAttribute)),
            AmbiguityMode = Get(values, nameof(WebAppConfig.AmbiguityMode)) is { } mode ? Enum.Parse<UiAmbiguityMode>(mode) : null,
            IgnoreHttpsErrors = ParseBool(values, nameof(WebAppConfig.IgnoreHttpsErrors)),
            WidgetCapture = Get(values, nameof(WebAppConfig.WidgetCapture)) is { } capture ? Enum.Parse<UiWidgetCapture>(capture) : null,
        };
    }

    private static void Add(Dictionary<ValueKey, string> values, string name, string? value)
    {
        if (value is { Length: > 0 })
        {
            values[new ValueKey(name)] = value;
        }
    }

    private static string? Format(bool? value) => value is null ? null : value.Value ? "true" : "false";

    private static string? Format(TimeSpan? value) => value?.ToString("c", CultureInfo.InvariantCulture);

    private static string? Get(IReadOnlyDictionary<string, string> values, string name)
        => values.TryGetValue(name, out string? value) ? value : null;

    private static bool? ParseBool(IReadOnlyDictionary<string, string> values, string name)
        => Get(values, name) is { } text ? bool.Parse(text) : null;

    private static int? ParseInt(IReadOnlyDictionary<string, string> values, string name)
        => Get(values, name) is { } text ? int.Parse(text, CultureInfo.InvariantCulture) : null;

    private static TimeSpan? ParseTime(IReadOnlyDictionary<string, string> values, string name)
        => Get(values, name) is { } text ? TimeSpan.ParseExact(text, "c", CultureInfo.InvariantCulture) : null;
}
