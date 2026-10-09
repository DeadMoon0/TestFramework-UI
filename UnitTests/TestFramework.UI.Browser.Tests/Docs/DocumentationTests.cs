using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using TestFramework.UI.Browser.Exceptions;

namespace TestFramework.UI.Browser.Tests.Docs;

/// <summary>
/// The documentation, held to the same standard as the code: what it grounds on must exist, and what
/// exists must be documented.
/// </summary>
public class DocumentationTests
{
    [Fact]
    public void TheSkillFileExistsAndItsGroundingFilesDo()
    {
        string root = RepositoryRoot();
        string skillPath = Path.Combine(root, "AI", "TestFramework.UI.SKILL.md");

        Assert.True(File.Exists(skillPath), $"The skill file is missing at \"{skillPath}\".");

        string skill = File.ReadAllText(skillPath);
        string[] missing = ListedFiles(skill, "grounding_files")
            .Where(relative => !File.Exists(Path.Combine(root, relative)))
            .ToArray();

        Assert.True(missing.Length == 0, $"The skill file grounds on files that do not exist: {string.Join(", ", missing)}");
    }

    [Fact]
    public void TheSkillFilesSourcesAllExist()
    {
        string root = RepositoryRoot();
        string skill = File.ReadAllText(Path.Combine(root, "AI", "TestFramework.UI.SKILL.md"));

        string[] missing = ListedFiles(skill, "sources")
            .Where(relative => !File.Exists(Path.Combine(root, relative)))
            .ToArray();

        Assert.True(missing.Length == 0, $"The skill file cites sources that do not exist: {string.Join(", ", missing)}");
    }

    [Fact]
    public void EveryPublicExceptionIsInTheErrorHandlingDocument()
    {
        // The document promises "every failure, what it captures, and the way out" - a promise a new
        // exception type must not quietly break. Shipping one means documenting it.
        string document = File.ReadAllText(Path.Combine(RepositoryRoot(), "Documentation", "ERROR-HANDLING-UI.md"));

        IEnumerable<Type> exceptionTypes = new[]
            {
                typeof(UiActionFailedException).Assembly,           // TestFramework.UI.Browser
            }
            .SelectMany(static assembly => assembly.GetExportedTypes())
            .Where(static type => typeof(Exception).IsAssignableFrom(type));

        string[] undocumented = exceptionTypes
            .Select(static type => type.Name)
            .Distinct()
            .Where(name => !document.Contains(name, StringComparison.Ordinal))
            .ToArray();

        Assert.True(
            undocumented.Length == 0,
            $"Public exception types missing from ERROR-HANDLING-UI.md: {string.Join(", ", undocumented)}");
    }

    [Fact]
    public void TheReleaseGateRunsEveryTestSuite()
    {
        // The gate ran the browser suite only, so the foundation suite - the comparison rules - never stopped a
        // publish. A suite added later is held to the same rule by this test rather than by memory.
        string root = RepositoryRoot();
        string workflow = File.ReadAllText(Path.Combine(root, ".github", "workflows", "release-ui-browser.yml"));

        string[] ungated = Directory.GetFiles(Path.Combine(root, "UnitTests"), "*.Tests.csproj", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(root, path).Replace(Path.DirectorySeparatorChar, '/'))
            .Where(relative => !workflow.Contains(relative, StringComparison.Ordinal))
            .ToArray();

        Assert.True(ungated.Length == 0, $"Test suites the release gate does not run: {string.Join(", ", ungated)}");
    }

    [Fact]
    public void NoConfigurationRefusalNamesAMethodThatDoesNotExist()
    {
        // A refusal is copied, not read: the one a user hit when a site and an API shared a name told them to call
        // FromSite(...) and FromWebApi(...), which had been deleted with the bridge package. Every factory is called
        // here, and every Name( in its message must be a public member somewhere in the family it can reach.
        HashSet<string> known = [.. new[]
            {
                typeof(UiConfigurationException).Assembly,
                typeof(TestFramework.Core.Steps.RunContext).Assembly,
                typeof(TestFramework.Config.ConfigInstance).Assembly,
                typeof(TestFramework.Web.WebExt).Assembly,
            }
            .SelectMany(static assembly => assembly.GetExportedTypes())
            .SelectMany(static type => type.GetMembers(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Instance)
                .Select(static member => member.Name)
                .Append(type.Name))];

        List<string> invented = [];
        foreach (System.Reflection.MethodInfo factory in typeof(UiConfigurationException).GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(static method => method.ReturnType == typeof(UiConfigurationException)))
        {
            object?[] arguments = [.. factory.GetParameters().Select(static parameter => Sample(parameter.ParameterType))];
            string message = ((Exception)factory.Invoke(null, arguments)!).Message;

            invented.AddRange(Regex.Matches(message, @"\b([A-Z][A-Za-z]+)\(")
                .Select(static match => match.Groups[1].Value)
                .Where(name => !known.Contains(name))
                .Select(name => $"{factory.Name}: {name}(...)"));
        }

        Assert.True(invented.Count == 0, $"Refusals name members that do not exist: {string.Join("; ", invented)}");
    }

    private static object? Sample(Type type)
        => type == typeof(string) ? "shop"
        : typeof(IEnumerable<string>).IsAssignableFrom(type) ? new[] { "a site 'shop'", "a REST API 'shop'" }
        : type.IsValueType ? Activator.CreateInstance(type)
        : null;

    private static string[] ListedFiles(string skill, string elementName)
    {
        Match block = Regex.Match(skill, $"<{elementName}>(?<body>.*?)</{elementName}>", RegexOptions.Singleline);
        Assert.True(block.Success, $"The skill file has no <{elementName}> block.");

        return block.Groups["body"].Value
            .Split('\n')
            .Select(static line => line.Trim().TrimStart('-').Trim())
            .Where(static line => line.Length > 0 && line.Contains('.') && !line.EndsWith(':'))
            .ToArray();
    }

    private static string RepositoryRoot()
    {
        DirectoryInfo? directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "TestFramework.UI.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);

        return directory!.FullName;
    }
}
