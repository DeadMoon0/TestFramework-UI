using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using TestFramework.Core.Debugger;
using TestFramework.Core.Steps;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Timelines;
using TestFramework.UI.Browser.Configuration;
using TestFramework.UI.Browser.Identifier;
using TestFramework.UI.Browser.Runtime;
using TestFramework.UI.Session;
using TestFramework.UI.Browser.Tests.Shared;
using Xunit;
using Xunit.Abstractions;

namespace TestFramework.UI.Browser.Tests.Browser;

/// <summary>
/// The browser answering someone who asked to see it.
/// </summary>
/// <remarks>
/// A run held at a breakpoint has a page nothing has photographed: the step that navigated there has
/// not finished, so the newest picture of it predates the navigation. This is what closes that gap —
/// the consumer asks, and the run answers with a picture of now.
/// </remarks>
[Collection(SampleAppCollection.Name)]
public class CaptureOnRequestTests(SampleAppFixture fixture, ITestOutputHelper output)
{
    [BrowserFact]
    public async Task AskingARunForALookPhotographsTheOpenPage()
    {
        TimelineRun run = await RunAsync(holdForInspection: false);

        run.EnsureRanToCompletion();

        // Named for the application rather than for the moment, so repeated asks read as versions of
        // one page in the order they were made.
        Assert.Contains("live-shop.png", Widgets());
    }

    [BrowserFact]
    public async Task ABrowserBeingHeldForAPersonIsNotPhotographed()
    {
        // A page stopped in Playwright's inspector does not answer a screenshot request, it waits —
        // and it is waiting for a person, so that wait has no bound. Refusing costs a picture;
        // asking would cost the run.
        string[] before = Widgets();

        TimelineRun run = await RunAsync(holdForInspection: true);

        run.EnsureRanToCompletion();

        Assert.Equal(before.Length, Widgets().Length);
    }

    [Fact]
    public void ThePackRegistersExactlyOneCaptureSource()
    {
        // Two would take two pictures of one page per request, which is also why the observer beside
        // it is registered the same way.
        Assert.Single(fixture.Services().GetServices<IWidgetCaptureSource>());
    }

    private async Task<TimelineRun> RunAsync(bool holdForInspection)
    {
        Timeline timeline = Timeline.Create()
            .Trigger(new AsksForALookStep("shop", holdForInspection)).Name("look")
            .Build();

        return await timeline.SetupRun(fixture.Services(), output).RunAsync();
    }

    /// <summary>Every widget any run in this output has written, by file name.</summary>
    private static string[] Widgets()
        => Directory.Exists(RunOutput.Root)
            ? [.. Directory.GetFiles(RunOutput.Root, "*", SearchOption.AllDirectories)
                .Where(path => string.Equals(Path.GetFileName(Path.GetDirectoryName(path)), "widgets", StringComparison.Ordinal))
                .Select(Path.GetFileName)!]
            : [];

    /// <summary>
    /// Opens a page, then asks the run to photograph itself the way the transport does.
    /// </summary>
    /// <remarks>
    /// A step rather than a live breakpoint, because what is under test is the source rather than the
    /// wire that reaches it: it is handed the run's context and has to find the open sessions in it,
    /// which is the same thing it does when a paused run is asked. It closes what it opened, since a
    /// step of this suite's own is not offered the cleanup step every browser step offers.
    /// </remarks>
    private sealed class AsksForALookStep(WebAppIdentifier app, bool holdForInspection) : Step<EmptyStepResultContext>
    {
        public override string Name => "Asks for a look";

        public override string Description => "Photographs the browser on request, the way a paused run is asked to.";

        public override bool DoesReturn => false;

        public override Step<EmptyStepResultContext> Clone() => new AsksForALookStep(app, holdForInspection).WithClonedOptions(this);

        public override StepInstance<Step<EmptyStepResultContext>, EmptyStepResultContext> GetInstance() => new(this);

        public override void DeclareIO(StepIOContract contract)
        {
        }

        public override async Task<EmptyStepResultContext?> Execute(RunContext context)
        {
            CancellationToken cancellationToken = context.Deadline.Token;
            WebAppConfig config = UiConfigResolver.ResolveEffective(context, app);
            UiRunState runState = UiRunState.For(context.Variables);

            UiSession session = await context.Services
                .GetUIComponentFactory()
                .SessionAsync(app, config, runState, cancellationToken)
                .ConfigureAwait(false);

            try
            {
                await session.Page.GotoAsync(new Uri(new Uri(config.BaseUrl!), "/products").ToString()).ConfigureAwait(false);

                using IDisposable? hold = holdForInspection ? runState.HoldForInspection() : null;

                await new UiWidgetCaptureSource().CaptureAsync(context).ConfigureAwait(false);
            }
            finally
            {
                foreach (UiSession open in runState.OpenSessions())
                    await open.DisposeAsync().ConfigureAwait(false);

                runState.Clear();
            }

            return EmptyStepResultContext.Instance;
        }
    }
}
