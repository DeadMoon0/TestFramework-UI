using System;

namespace TestFramework.UI.Browser.Runtime;

/// <summary>
/// Turns what a test wrote into the address to open.
/// </summary>
/// <remarks>
/// <para>
/// A path in a step is relative to the application's configured address, and that includes a path
/// beginning with a slash. Ordinary URL rules would read <c>/products</c> as the server's root and throw
/// away the prefix an application is hosted under, so a suite would pass against
/// <c>https://host/</c> and break against <c>https://host/shop/</c> - a difference no test intended to
/// care about, and one that a reverse proxy can introduce without anybody touching the application.
/// </para>
/// <para>
/// An absolute address is left exactly as written, for the occasional step that has to leave the
/// application under test.
/// </para>
/// </remarks>
internal static class UiUrls
{
    /// <summary>
    /// The address a step should open.
    /// </summary>
    /// <param name="baseUrl">The application's configured address.</param>
    /// <param name="path">What the step wrote: a relative path, or an absolute address.</param>
    /// <returns>The absolute address.</returns>
    public static string Resolve(string? baseUrl, string? path)
    {
        string requested = path ?? string.Empty;

        if (Uri.TryCreate(requested, UriKind.Absolute, out Uri? absolute)
            && (absolute.Scheme == Uri.UriSchemeHttp || absolute.Scheme == Uri.UriSchemeHttps))
        {
            return absolute.ToString();
        }

        if (baseUrl is not { Length: > 0 })
        {
            return requested;
        }

        // A base address always names a folder, whatever it was configured as: everything a step asks for
        // hangs below it.
        string root = baseUrl.EndsWith('/') ? baseUrl : baseUrl + "/";
        string relative = requested.TrimStart('/');

        return relative.Length == 0
            ? root
            : new Uri(new Uri(root, UriKind.Absolute), relative).ToString();
    }
}
