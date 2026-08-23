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
                typeof(TestFramework.UI.UiText).Assembly,          // TestFramework.UI
                typeof(TestFramework.UI.Web.UiWebBridgeConfigExtension).Assembly, // TestFramework.UI.Web
            }
            .Distinct()
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
