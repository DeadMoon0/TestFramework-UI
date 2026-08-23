using System;
using System.IO;
using System.Linq;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Variables;
using TestFramework.UI.Browser.Scripting;
using TestFramework.UI.Browser.Steps;
using TestFramework.UI.Browser.Targeting;

namespace TestFramework.UI.Browser.Tests.Scripting;

/// <summary>
/// The script value object, and what a flow declares for it.
/// </summary>
public class JsScriptTests
{
    [Fact]
    public void AnUnnamedScriptFallsBackToItsFirstLine()
    {
        JsScript script = Js.Inline("() => window.scrollTo(0, 0)");

        Assert.Equal("() => window.scrollTo(0, 0)", script.Name);
    }

    [Fact]
    public void ALongFirstLineIsShortenedForTheTrace()
    {
        string source = "() => " + new string('x', 200);
        JsScript script = Js.Inline(source);

        Assert.True(script.Name.Length < 70, $"'{script.Name}' should be short enough for a trace line");
        Assert.EndsWith("...", script.Name, StringComparison.Ordinal);
    }

    [Fact]
    public void NamingAScriptIsWhatTheTraceShows()
    {
        JsScript script = Js.Inline("() => localStorage.clear()").Named("wipe the storage");

        Assert.Equal("wipe the storage", script.Name);
        Assert.Equal("wipe the storage", script.ToString());
    }

    [Fact]
    public void AMissingScriptFileFailsWhileTheTimelineIsBuilt()
    {
        // Before any browser starts, and with the path as it was resolved - a typo in a file name should
        // cost seconds, not a browser launch.
        FileNotFoundException failure = Assert.Throws<FileNotFoundException>(
            () => Js.FromFile("scripts/does-not-exist.js"));

        Assert.Contains("does-not-exist.js", failure.Message, StringComparison.Ordinal);
        Assert.Contains("resolved to", failure.Message, StringComparison.Ordinal);
        Assert.Contains("working directory", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AScriptFileIsReadImmediatelyAndNamedAfterItself()
    {
        string path = Path.Combine(Path.GetTempPath(), $"tf-ui-{Guid.NewGuid():N}.js");
        File.WriteAllText(path, "() => document.title");

        try
        {
            JsScript script = Js.FromFile(path);

            Assert.Equal("() => document.title", script.Source);
            Assert.Equal(Path.GetFileName(path), script.Name);

            // Read eagerly: deleting the file afterwards changes nothing about the script.
            File.Delete(path);
            Assert.Equal("() => document.title", script.Source);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void ArgumentsAreDeclaredAsTheStepsInputs()
    {
        // A script's dependencies are visible to the planner like any other verb's, so a typo in a
        // variable name fails before a browser starts.
        UiBrowserFlow flow = BrowserExt.Session("shop")
            .Execute(Js.Inline("args => localStorage.setItem('cart', args.cart)")
                .Named("seed the cart")
                .WithArgument("cart", Var.Ref<string>("seededCart")));

        StepIOContract contract = new StepIOContract();
        flow.DeclareIO(contract);

        Assert.Contains(contract.Inputs, static entry => entry.Key == "seededCart");
    }

    [Fact]
    public void AnEvaluationDeclaresItsVariableInTheTypeItWasAskedFor()
    {
        UiBrowserFlow flow = BrowserExt.Session("shop")
            .Evaluate<int>(Js.Inline("() => 1"), "one")
            .Evaluate<string>(Js.Inline("() => document.title"), "title");

        StepIOContract contract = new StepIOContract();
        flow.DeclareIO(contract);

        Assert.Equal(typeof(int), contract.Outputs.Single(static entry => entry.Key == "one").DeclaredType);
        Assert.Equal(typeof(string), contract.Outputs.Single(static entry => entry.Key == "title").DeclaredType);
    }

    [Fact]
    public void AScriptActionSaysWhatItRunsAndWhere()
    {
        UiBrowserFlow page = BrowserExt.Session("shop").Execute(Js.Inline("() => 1").Named("poke"));
        UiBrowserFlow element = BrowserExt.Session("shop")
            .Evaluate<int>(Target.Section("Orders All"), Js.Inline("el => 1").Named("count rows"), "rows");

        Assert.Equal("Execute script 'poke'", Assert.Single(page.ActionsForTesting).Describe());
        Assert.Equal(
            "Evaluate script 'count rows' on section 'Orders All'",
            Assert.Single(element.ActionsForTesting).Describe());
    }

    [Fact]
    public void AddingAnArgumentLeavesTheOriginalScriptAlone()
    {
        JsScript bare = Js.Inline("args => args.a").Named("shared");
        JsScript extended = bare.WithArgument("a", Var.Ref<string>("x"));

        Assert.Empty(bare.Arguments);
        Assert.Single(extended.Arguments);
    }
}
