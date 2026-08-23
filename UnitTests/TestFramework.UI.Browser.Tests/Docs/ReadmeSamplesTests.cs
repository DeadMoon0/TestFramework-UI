using System.Threading.Tasks;
using TestFramework.Core.Timelines;
using TestFramework.UI.Browser.Tests.Shared;
using Xunit.Abstractions;

namespace TestFramework.UI.Browser.Tests.Docs;

// README sync note: these tests mirror the public README samples for TestFramework-UI.
// If you update a test here, update the corresponding README sample as well.
[Collection(SampleAppCollection.Name)]
public class ReadmeSamplesTests(SampleAppFixture fixture, ITestOutputHelper output)
{
    // Mirrors the "What It Does" sample in the root README.md. The README shows "/products" against a
    // configured BaseUrl of the application root; the fixture's identifier points at the same page.
    [BrowserFact]
    public async Task RootReadme_WhatItDoes_RunsAsWritten()
    {
        Timeline timeline = Timeline.Create()
            .Trigger(BrowserExt.Session("shop")
                .Navigate("/products")
                .Click("Add Anvil to cart")
                .Expect("1 item in cart"))
                .Name("cart")
            .Build();

        TimelineRun run = await timeline.SetupRun(fixture.Services(), output).RunAsync();

        run.EnsureRanToCompletion();
        run.UiUrl("shop").Should().Contain("/products");
        run.UiLooseMatches("shop").Should().HaveNoItems();
    }
}
