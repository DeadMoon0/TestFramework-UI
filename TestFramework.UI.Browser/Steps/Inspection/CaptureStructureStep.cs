using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Playwright;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Logging;
using TestFramework.Core.Steps;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Variables;
using TestFramework.UI.Browser.Identifier;
using TestFramework.UI.Browser.Structure;
using TestFramework.UI.Browser.Targeting;
using TestFramework.UI.Structure;

namespace TestFramework.UI.Browser.Steps.Inspection;

/// <summary>
/// Records what a part of the page is built like, without saying what it should be.
/// </summary>
/// <remarks>
/// <para>
/// For the case no expectation covers: nobody wants to write down the whole shape of a page, but everybody
/// wants to know when it changes. The captured structure lands in a variable, and the framework's existing
/// value-change detection compares it against the last clean run of the same test - so a page quietly
/// losing a column, or gaining one, shows up as a change to look at rather than as nothing at all.
/// </para>
/// <para>
/// It is a change report, not an assertion. A run does not fail because a page changed; it fails because a
/// test said something that stopped being true. That distinction is what keeps a captured structure from
/// becoming the snapshot nobody reads and everybody regenerates.
/// </para>
/// </remarks>
internal sealed class CaptureStructureStep : UiInspectionStep<UiCaptureResultContext>
{
    private readonly string captureName;

    public CaptureStructureStep(WebAppIdentifier app, UiTarget scope, string captureName)
        : base(app, scope)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(captureName);

        this.captureName = captureName;
    }

    /// <inheritdoc />
    public override string Name => "UI Capture Structure";

    /// <inheritdoc />
    public override string Description
        => $"Records the structure of {this.Target.Describe()} on '{this.App}' as '{this.captureName}'";

    /// <inheritdoc />
    protected override string ActionName => "CaptureStructure";

    /// <inheritdoc />
    public override Step<UiCaptureResultContext> Clone()
        => new CaptureStructureStep(this.App, this.Target, this.captureName).WithClonedOptions(this);

    /// <inheritdoc />
    public override StepInstance<Step<UiCaptureResultContext>, UiCaptureResultContext> GetInstance()
        => new StepInstance<Step<UiCaptureResultContext>, UiCaptureResultContext>(this);

    /// <inheritdoc />
    protected override void DeclareOwnIO(StepIOContract contract)
        => contract.Outputs.Add(new StepIOEntry(this.captureName, StepIOKind.Variable, true, typeof(string)));

    /// <inheritdoc />
    protected override async Task<(UiCaptureResultContext Result, string? Detail)> InspectAsync(
        ILocator locator,
        CancellationToken cancellationToken)
    {
        UiElementSnapshot snapshot = await DomProjector.ProjectAsync(locator, cancellationToken).ConfigureAwait(false);
        string rendered = DomProjector.Render(snapshot);

        UiCaptureResultContext result = new UiCaptureResultContext(
            this.App,
            locator.Page.Url,
            this.captureName,
            rendered,
            snapshot.Size());

        return (result, $"{snapshot.Size()} element(s)");
    }

    /// <inheritdoc />
    public override async Task<UiCaptureResultContext?> Execute(
        IServiceProvider serviceProvider,
        VariableStore variableStore,
        ArtifactStore artifactStore,
        ScopedLogger logger,
        CancellationToken cancellationToken)
    {
        UiCaptureResultContext? result = await base
            .Execute(serviceProvider, variableStore, artifactStore, logger, cancellationToken)
            .ConfigureAwait(false);

        if (result is not null)
        {
            // The variable is the whole point: it is what the run's value comparison sees, and what a later
            // step could assert on if it wanted to.
            Publish(variableStore, this.captureName, result.Structure);
        }

        return result;
    }
}
