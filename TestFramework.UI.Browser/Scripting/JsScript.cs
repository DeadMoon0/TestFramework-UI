using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TestFramework.Core.Variables;
using TestFramework.UI.Structure;

namespace TestFramework.UI.Browser.Scripting;

/// <summary>
/// A piece of JavaScript a test hands to the page, as a named value rather than a bare string.
/// </summary>
/// <remarks>
/// <para>
/// The name is what traces and failure messages show, because a trace line holding four hundred
/// characters of minified JavaScript tells the reader nothing. Name a script after what it does; unnamed
/// inline scripts fall back to their first line, and file scripts to their file name.
/// </para>
/// <para>
/// Arguments come from timeline variables and are declared as the step's inputs, so a script's
/// dependencies are visible to the planner like any other step's. They reach the function as one object:
/// a page script is called as <c>args =&gt; ...</c>, an element script as <c>(el, args) =&gt; ...</c>, and
/// every argument value arrives as a string - parsing it is the script's own, stated decision. A script
/// without arguments is called without the object: <c>() =&gt; ...</c> or <c>el =&gt; ...</c>.
/// </para>
/// </remarks>
public sealed record JsScript
{
    private JsScript(string source, string name, IReadOnlyList<(string Name, VariableReference<string> Value)> arguments)
    {
        this.Source = source;
        this.Name = name;
        this.Arguments = arguments;
    }

    /// <summary>The function's source, as the page will receive it.</summary>
    public string Source { get; }

    /// <summary>How the script reads in a trace or a failure message.</summary>
    public string Name { get; }

    /// <summary>The arguments the function receives, by name, from timeline variables.</summary>
    internal IReadOnlyList<(string Name, VariableReference<string> Value)> Arguments { get; }

    /// <summary>
    /// Gives the script the name traces and failures will use.
    /// </summary>
    /// <param name="name">What the script does, for example <c>seed the cart</c>.</param>
    /// <returns>The named script.</returns>
    public JsScript Named(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return new JsScript(this.Source, name, this.Arguments);
    }

    /// <summary>
    /// Passes a timeline variable to the function, under this name on its argument object.
    /// </summary>
    /// <param name="name">The property name the function reads, for example <c>args.orderId</c>.</param>
    /// <param name="value">Where the value comes from.</param>
    /// <returns>The script with the argument added.</returns>
    public JsScript WithArgument(string name, VariableReference<string> value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(value);

        return new JsScript(this.Source, this.Name, [.. this.Arguments, (name, value)]);
    }

    /// <summary>
    /// Returns the script's name.
    /// </summary>
    /// <returns>The name.</returns>
    public override string ToString() => this.Name;

    internal static JsScript Of(string source)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);

        // The first line is the least-bad automatic name; a real one comes from Named().
        string firstLine = UiText.Normalize(source.TrimStart().Split('\n')[0]) ?? "script";

        return new JsScript(source, UiText.Truncate(firstLine, 60) ?? "script", []);
    }

    internal static JsScript OfFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        // Read where the timeline is built, so a missing file fails before any browser starts, and with
        // the path as it was actually resolved rather than as it was written.
        string fullPath = Path.GetFullPath(path);

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException(
                $"The script file '{path}' does not exist. It was resolved to '{fullPath}'; a relative " +
                "path is resolved against the test process's working directory.",
                fullPath);
        }

        return new JsScript(File.ReadAllText(fullPath), Path.GetFileName(fullPath), []);
    }
}

/// <summary>
/// Where a test's JavaScript comes from.
/// </summary>
public static class Js
{
    /// <summary>
    /// A function written in the test, for example <c>() =&gt; window.scrollTo(0, 0)</c>.
    /// </summary>
    /// <param name="source">The function. With arguments declared it is called as <c>args =&gt; ...</c>
    /// (or <c>(el, args) =&gt; ...</c> on an element); without, as <c>() =&gt; ...</c> (or
    /// <c>el =&gt; ...</c>).</param>
    /// <returns>The script.</returns>
    public static JsScript Inline(string source) => JsScript.Of(source);

    /// <summary>
    /// A function read from a file, for a script long enough to deserve syntax highlighting and review
    /// of its own.
    /// </summary>
    /// <param name="path">The file, resolved against the test process's working directory. Read
    /// immediately, so a missing file fails while the timeline is being built.</param>
    /// <returns>The script, named after the file.</returns>
    public static JsScript FromFile(string path) => JsScript.OfFile(path);
}
