using System;
using System.Globalization;
using TestFramework.UI.Browser.Configuration;

namespace TestFramework.UI.Browser.Runtime;

/// <summary>
/// The local knobs a developer turns while chasing a failure, without editing anything that gets
/// committed.
/// </summary>
/// <remarks>
/// A red test is usually understood by watching it happen. These exist so that costs nothing but
/// re-running with a variable set: no configuration change to remember to revert, and no risk of a
/// headed browser reaching a build server.
/// </remarks>
internal static class UiEnvironmentOverrides
{
    /// <summary>Set to 1 to watch the browser work instead of running it hidden.</summary>
    public const string HeadedVariable = "TESTFRAMEWORK_UI_HEADED";

    /// <summary>Set to a number of milliseconds to slow every interaction down by that much.</summary>
    public const string SlowMoVariable = "TESTFRAMEWORK_UI_SLOWMO";

    /// <summary>Set to 1 to leave the browser open on the failure, so the page can be inspected live.</summary>
    public const string PauseOnFailureVariable = "TESTFRAMEWORK_UI_PAUSE_ON_FAILURE";

    /// <summary>Whether the run should hold a failed page open.</summary>
    public static bool PauseOnFailure => IsSet(PauseOnFailureVariable);

    /// <summary>
    /// Applies whatever the machine asks for on top of the configuration.
    /// </summary>
    /// <param name="config">The configured settings.</param>
    /// <returns>The settings to actually run with.</returns>
    public static WebAppConfig Apply(WebAppConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        WebAppConfig result = config;

        if (IsSet(HeadedVariable))
        {
            result = result with { Headless = false };
        }

        if (Environment.GetEnvironmentVariable(SlowMoVariable) is { Length: > 0 } slowMo
            && double.TryParse(slowMo, NumberStyles.Number, CultureInfo.InvariantCulture, out double milliseconds)
            && milliseconds > 0)
        {
            result = result with { SlowMo = TimeSpan.FromMilliseconds(milliseconds) };
        }

        return result;
    }

    private static bool IsSet(string variable)
        => Environment.GetEnvironmentVariable(variable) is { Length: > 0 } value
        && (value == "1" || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase));
}
