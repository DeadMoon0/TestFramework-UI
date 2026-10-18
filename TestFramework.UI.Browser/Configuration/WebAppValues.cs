using System;
using System.Collections.Generic;
using System.Globalization;

using TestFramework.Core.Environment.Graph;
using TestFramework.UI.Browser.Resolution;

using TestFramework.UI.Browser.Exceptions;
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

        Add(values, nameof(WebAppConfig.BaseUrlFrom), config.BaseUrlFrom);
        Add(values, nameof(WebAppConfig.Browser), config.Browser);
        Add(values, nameof(WebAppConfig.Channel), config.Channel);
        Add(values, nameof(WebAppConfig.Headless), Format(config.Headless));
        Add(values, nameof(WebAppConfig.Install), Format(config.Install));
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
    /// <remarks>
    /// Any resource source may declare a <c>ui.webapp</c> resource by hand, so a value that does not read is refused
    /// naming the application, the setting, what it held and what it takes - not as a bare parse exception that
    /// names none of them.
    /// </remarks>
    /// <param name="values">The application's values.</param>
    /// <param name="identifier">The application, for a refusal.</param>
    /// <exception cref="UiConfigurationException">A value cannot be read as its setting's type.</exception>
    public static WebAppConfig Read(IReadOnlyDictionary<string, string> values, string identifier)
    {
        ArgumentNullException.ThrowIfNull(values);

        return new WebAppConfig
        {
            BaseUrl = Get(values, ValueNames.BaseUrl),
            BaseUrlFrom = Get(values, nameof(WebAppConfig.BaseUrlFrom)),
            Browser = Get(values, nameof(WebAppConfig.Browser)),
            Channel = Get(values, nameof(WebAppConfig.Channel)),
            Headless = ParseBool(values, identifier, nameof(WebAppConfig.Headless)),
            Install = ParseBool(values, identifier, nameof(WebAppConfig.Install)),
            Device = Get(values, nameof(WebAppConfig.Device)),
            ViewportWidth = ParseInt(values, identifier, nameof(WebAppConfig.ViewportWidth)),
            ViewportHeight = ParseInt(values, identifier, nameof(WebAppConfig.ViewportHeight)),
            UserAgent = Get(values, nameof(WebAppConfig.UserAgent)),
            IsMobile = ParseBool(values, identifier, nameof(WebAppConfig.IsMobile)),
            HasTouch = ParseBool(values, identifier, nameof(WebAppConfig.HasTouch)),
            DeviceScaleFactor = ParseFloat(values, identifier, nameof(WebAppConfig.DeviceScaleFactor)),
            Locale = Get(values, nameof(WebAppConfig.Locale)),
            ColorScheme = Get(values, nameof(WebAppConfig.ColorScheme)),
            SlowMo = ParseTime(values, identifier, nameof(WebAppConfig.SlowMo)),
            DefaultActionTimeout = ParseTime(values, identifier, nameof(WebAppConfig.DefaultActionTimeout)),
            DefaultCompareTimeout = ParseTime(values, identifier, nameof(WebAppConfig.DefaultCompareTimeout)),
            TestIdAttribute = Get(values, nameof(WebAppConfig.TestIdAttribute)),
            AmbiguityMode = ParseEnum<UiAmbiguityMode>(values, identifier, nameof(WebAppConfig.AmbiguityMode)),
            IgnoreHttpsErrors = ParseBool(values, identifier, nameof(WebAppConfig.IgnoreHttpsErrors)),
            WidgetCapture = ParseEnum<UiWidgetCapture>(values, identifier, nameof(WebAppConfig.WidgetCapture)),
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

    private static bool? ParseBool(IReadOnlyDictionary<string, string> values, string identifier, string name)
        => Get(values, name) is not { } text ? null
        : bool.TryParse(text, out bool flag) ? flag
        : throw UiConfigurationException.InvalidValue(identifier, name, text, "true or false");

    private static int? ParseInt(IReadOnlyDictionary<string, string> values, string identifier, string name)
        => Get(values, name) is not { } text ? null
        : int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int number) ? number
        : throw UiConfigurationException.InvalidValue(identifier, name, text, "a whole number");

    private static float? ParseFloat(IReadOnlyDictionary<string, string> values, string identifier, string name)
        => Get(values, name) is not { } text ? null
        : float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float number) ? number
        : throw UiConfigurationException.InvalidValue(identifier, name, text, "a number such as 2.5");

    private static TimeSpan? ParseTime(IReadOnlyDictionary<string, string> values, string identifier, string name)
        => Get(values, name) is not { } text ? null
        : TimeSpan.TryParseExact(text, "c", CultureInfo.InvariantCulture, out TimeSpan time) ? time
        : throw UiConfigurationException.InvalidValue(identifier, name, text, "a time span such as 00:00:05");

    private static TEnum? ParseEnum<TEnum>(IReadOnlyDictionary<string, string> values, string identifier, string name)
        where TEnum : struct, Enum
        => Get(values, name) is not { } text ? null
        : Enum.TryParse(text, ignoreCase: false, out TEnum parsed) && Enum.IsDefined(parsed) ? parsed
        : throw UiConfigurationException.InvalidValue(identifier, name, text, $"one of {string.Join(", ", Enum.GetNames<TEnum>())}");
}
