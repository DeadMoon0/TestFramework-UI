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
using TestFramework.UI.Browser.Targeting;
using TestFramework.UI.Structure;

namespace TestFramework.UI.Browser.Steps.Inspection;

/// <summary>
/// Records where everything in a part of the page sits, without saying where it should.
/// </summary>
/// <remarks>
/// <para>
/// The layout counterpart of capturing a structure: the recorded geometry lands in a variable, and the
/// framework's value-change detection compares it against the last clean run - so a grid collapsing to
/// one column, or a button escaping its panel, shows up as a change to look at, with no pixel files, no
/// baseline service and nothing to approve.
/// </para>
/// <para>
/// What makes the capture comparable at all is what it leaves out: positions are relative to the scope
/// (so the page moving as a whole is not drift), snapped to a four-pixel grid (so antialiasing and
/// subpixel layout are not drift), and text is not captured (content has its own captures). Only a
/// change a person could point at survives into the diff.
/// </para>
/// </remarks>
internal sealed class CaptureLayoutStep : UiInspectionStep<UiCaptureResultContext>
{
    /// <summary>The grid the coordinates snap to.</summary>
    private const int GridPx = 4;

    /// <summary>How deep the capture descends.</summary>
    private const int MaxDepth = 6;

    /// <summary>How many elements it records at most.</summary>
    private const int MaxNodes = 400;

    private const string CaptureScript = """
        (element, options) => {
            const origin = element.getBoundingClientRect();
            const snap = value => Math.round(value / options.grid) * options.grid;
            let budget = options.maxNodes;
            const lines = [];

            const visible = node => {
                const style = getComputedStyle(node);

                return style.display !== 'none' && style.visibility !== 'hidden';
            };

            const walk = (node, depth) => {
                if (budget-- <= 0 || depth > options.maxDepth) {
                    return;
                }

                const rect = node.getBoundingClientRect();

                if (rect.width === 0 && rect.height === 0) {
                    return;
                }

                lines.push('  '.repeat(depth)
                    + node.tagName.toLowerCase()
                    + ' (' + snap(rect.x - origin.x) + ', ' + snap(rect.y - origin.y)
                    + ', ' + snap(rect.width) + 'x' + snap(rect.height) + ')');

                for (const child of node.children) {
                    if (visible(child)) {
                        walk(child, depth + 1);
                    }
                }
            };

            walk(element, 0);

            return lines.join('\n');
        }
        """;

    private readonly string captureName;

    public CaptureLayoutStep(WebAppIdentifier app, UiTarget scope, string captureName)
        : base(app, scope)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(captureName);

        this.captureName = captureName;
    }

    /// <inheritdoc />
    public override string Name => "UI Capture Layout";

    /// <inheritdoc />
    public override string Description
        => $"Records where everything in {this.Target.Describe()} on '{this.App}' sits, as '{this.captureName}'";

    /// <inheritdoc />
    protected override string ActionName => "CaptureLayout";

    /// <inheritdoc />
    public override Step<UiCaptureResultContext> Clone()
        => new CaptureLayoutStep(this.App, this.Target, this.captureName).WithClonedOptions(this);

    /// <inheritdoc />
    public override StepInstance<Step<UiCaptureResultContext>, UiCaptureResultContext> GetInstance()
        => new StepInstance<Step<UiCaptureResultContext>, UiCaptureResultContext>(this);

    /// <inheritdoc />
    protected override void DeclareOwnIO(StepIOContract contract)
        => contract.Outputs.Add(new StepIOEntry(this.captureName, StepIOKind.Variable, true, typeof(string)));

    /// <inheritdoc />
    protected override async Task<(UiCaptureResultContext Result, string? Detail)> InspectAsync(
        ILocator? locator,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(locator);

        cancellationToken.ThrowIfCancellationRequested();

        string rendered = await locator
            .EvaluateAsync<string>(CaptureScript, new { grid = GridPx, maxDepth = MaxDepth, maxNodes = MaxNodes })
            .ConfigureAwait(false) ?? string.Empty;

        int count = rendered.Length == 0 ? 0 : rendered.Split('\n').Length;

        UiCaptureResultContext result = new UiCaptureResultContext(
            this.App,
            locator.Page.Url,
            this.captureName,
            rendered,
            count);

        return (result, $"{count} element(s)");
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
            // An ordinary variable, which is what the run's value comparison sees.
            Publish(variableStore, this.captureName, result.Structure);
        }

        return result;
    }
}
