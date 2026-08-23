using System;
using System.IO;
using System.Text;
using TestFramework.Core.Debugger;

namespace TestFramework.UI.Browser.Runtime;

/// <summary>
/// Where a run's browser evidence is written.
/// </summary>
/// <remarks>
/// Under the run output root, which nothing deletes and a build publishes as artifacts - not under the
/// debug journal, which only exists when a debugging tool is installed. A screenshot of a failure has to
/// survive on a machine that has never heard of the debugging UI.
/// </remarks>
internal static class UiRunPaths
{
    private const string UiFolderName = "ui";

    /// <summary>
    /// Creates the folder this run writes its browser evidence into.
    /// </summary>
    /// <param name="startedUtc">When the run started, for a folder name a person can order by eye.</param>
    /// <param name="discriminator">A short unique suffix, so two runs in the same second never collide.</param>
    /// <returns>The folder's full path. The folder itself is created lazily on first write.</returns>
    public static string RunDirectory(DateTimeOffset startedUtc, string discriminator)
        => Path.Combine(
            RunOutput.Root,
            UiFolderName,
            $"{startedUtc:yyyyMMdd-HHmmss}-{SafeName(discriminator, 12)}");

    /// <summary>
    /// Turns anything into a usable path segment.
    /// </summary>
    /// <remarks>
    /// Core has its own version of this, but it is internal, and duplicating twenty lines is better than
    /// building the package against a member that could change without notice.
    /// </remarks>
    /// <param name="name">The text to sanitize.</param>
    /// <param name="maxLength">The most characters to keep.</param>
    /// <returns>A path segment safe on every platform.</returns>
    public static string SafeName(string? name, int maxLength)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxLength);

        if (string.IsNullOrWhiteSpace(name))
        {
            return "unnamed";
        }

        StringBuilder builder = new StringBuilder(Math.Min(name.Length, maxLength));

        foreach (char character in name)
        {
            if (builder.Length >= maxLength)
            {
                break;
            }

            if (char.IsLetterOrDigit(character) || character is '-' or '_' or '.')
            {
                builder.Append(character);
            }
            else if (builder.Length > 0 && builder[^1] != '-')
            {
                builder.Append('-');
            }
        }

        string result = builder.ToString().Trim('-', '.');

        return result.Length == 0 ? "unnamed" : result;
    }
}
