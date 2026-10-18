using System.Collections.Generic;

namespace TestFramework.UI.Browser.Runtime;

/// <summary>
/// Whether the browser build a launch needs is on this machine.
/// </summary>
/// <param name="IsKnown">Whether it could be told from here at all.</param>
/// <param name="IsInstalled">Whether the build is installed.</param>
/// <param name="Needed">The build folder a launch looks for, when it is missing.</param>
/// <param name="Found">Other builds of the same browser that are installed - an older Playwright's, usually.</param>
internal sealed record UiBuildCheck(bool IsKnown, bool IsInstalled, string? Needed, IReadOnlyList<string> Found)
{
    /// <summary>Could not be told from here; the launch remains the judge.</summary>
    public static UiBuildCheck Unknown { get; } = new(false, false, null, []);

    /// <summary>The build is installed.</summary>
    public static UiBuildCheck Installed { get; } = new(true, true, null, []);

    /// <summary>The build is missing.</summary>
    /// <param name="needed">The build folder a launch looks for.</param>
    /// <param name="found">Other builds of the same browser that are installed.</param>
    /// <returns>The answer.</returns>
    public static UiBuildCheck Missing(string needed, IReadOnlyList<string> found) => new(true, false, needed, found);

    /// <summary>Whether a launch is known to fail for want of this build.</summary>
    public bool IsKnownMissing => this.IsKnown && !this.IsInstalled;
}
