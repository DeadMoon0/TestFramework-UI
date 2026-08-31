using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TestFramework.Core.Debugger;
using TestFramework.Core.Timelines;
using TestFramework.UI.Browser.Tests.Shared;
using Xunit;
using Xunit.Abstractions;

namespace TestFramework.UI.Browser.Tests.Browser;

/// <summary>
/// What a run keeps of what the browser saw.
/// </summary>
/// <remarks>
/// All of it goes to the run's own widgets. What this replaced wrote the same files into a directory
/// named after a timestamp and a fresh identifier, sharing no key with the run that produced them — so
/// the evidence existed and nothing could find it.
/// </remarks>
[Collection(SampleAppCollection.Name)]
public class EvidenceTests(SampleAppFixture fixture, ITestOutputHelper output)
{
    [BrowserFact]
    public async Task AScreenshotAskedForByNameIsKeptUnderThatName()
    {
        // The one capture no policy may discard: the test said what it wanted recorded.
        Timeline timeline = Timeline.Create()
            .Trigger(BrowserExt.Session("shop")
                .Navigate("/products")
                .Screenshot("the-catalogue"))
                .Name("browse")
            .Build();

        TimelineRun run = await timeline.SetupRun(fixture.Services(), output).RunAsync();

        run.EnsureRanToCompletion();

        Assert.Contains("the-catalogue.png", WidgetNames());
    }

    [BrowserFact]
    public async Task AQuietRunKeepsNothingItWasNotAskedFor()
    {
        // The default, and the reason it is the default: a green suite that photographed every click
        // would leave a picture per action per run for a build to publish.
        string[] before = WidgetNames();

        Timeline timeline = Timeline.Create()
            .Trigger(BrowserExt.Session("shop")
                .Navigate("/products")
                .Click("Anvil"))
                .Name("quiet")
            .Build();

        TimelineRun run = await timeline.SetupRun(fixture.Services(), output).RunAsync();

        run.EnsureRanToCompletion();

        Assert.Equal(before.Length, WidgetNames().Length);
    }

    [BrowserFact]
    public async Task AWatchedRunKeepsWhatEachActionLeftBehind()
    {
        // What turns a step from "the button was not there" into the sequence that led to it. Numbered,
        // because the sequence is the thing being read.
        Timeline timeline = Timeline.Create()
            .Trigger(BrowserExt.Session("shop-watched")
                .Navigate("/products")
                .Click("Anvil"))
                .Name("watched")
            .Build();

        TimelineRun run = await timeline.SetupRun(fixture.Services(), output).RunAsync();

        run.EnsureRanToCompletion();

        string[] names = WidgetNames();

        Assert.Contains(names, name => name.StartsWith("01-", StringComparison.Ordinal));
        Assert.Contains(names, name => name.StartsWith("02-", StringComparison.Ordinal));
    }

    /// <summary>Every widget any run in this output has written, by file name.</summary>
    /// <remarks>
    /// Read off disk rather than from the run, because that is the promise being checked: the evidence
    /// is in the run's own output, where a person opens it and a build publishes it.
    /// </remarks>
    private static string[] WidgetNames()
        => Directory.Exists(RunOutput.Root)
            ? [.. Directory.GetFiles(RunOutput.Root, "*", SearchOption.AllDirectories)
                .Where(path => string.Equals(Path.GetFileName(Path.GetDirectoryName(path)), "widgets", StringComparison.Ordinal))
                .Select(Path.GetFileName)!]
            : [];
}
