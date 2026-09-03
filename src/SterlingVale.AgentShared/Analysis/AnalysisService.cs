using SterlingVale.AgentShared.Configuration;
using SterlingVale.Domain.Analysis;
using SterlingVale.Telemetry;
using SterlingVale.Tools;

namespace SterlingVale.AgentShared.Analysis;

/// <summary>
/// The single, mode-agnostic orchestration service used by both APIs. It resolves the snapshot,
/// builds the shared tool service, computes fairness fingerprints, creates the mode-specific agent
/// via the injected <see cref="IAgentFactory"/>, and runs it through the shared
/// <see cref="AnalysisRunner"/>. Keeping this shared guarantees the only variable is the agent
/// factory (orchestration).
/// </summary>
public sealed class AnalysisService(
    ISnapshotProvider snapshots,
    IChatClientProvider chatClientProvider,
    IAgentFactory agentFactory,
    AnalysisRunner runner,
    ModelConfiguration model,
    PricingConfiguration pricing,
    ToolMetrics toolMetrics)
{
    /// <summary>Runs an analysis and returns the complete run record.</summary>
    public async Task<RunRecord> AnalyzeAsync(AnalysisRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var snapshot = snapshots.GetSnapshot(request.DatasetProfile);
        string datasetHash = snapshots.GetDatasetHash(request.DatasetProfile);
        var toolService = new PortfolioToolService(snapshot, toolMetrics);
        string runId = Guid.NewGuid().ToString("N");

        string toolFingerprint = agentFactory.ComputeToolFingerprint(toolService);
        var fingerprints = FairnessFingerprints.Create(toolFingerprint, datasetHash, model, pricing);

        using var chatClient = chatClientProvider.CreateChatClient(new ChatClientRequest(snapshot, request, runId, agentFactory.Mode));
        using var bundle = agentFactory.Create(chatClient, toolService, model);

        var context = new RunContext
        {
            RunId = runId,
            Mode = agentFactory.Mode,
            UserMessage = BuildUserMessage(request),
            Model = model,
            Pricing = pricing,
            Fingerprints = fingerprints,
        };

        return await runner.RunAsync(bundle.Agent, context, cancellationToken).ConfigureAwait(false);
    }

    private static string BuildUserMessage(AnalysisRequest request)
    {
        string scope = request.HouseholdIds is { Count: > 0 }
            ? $"households {string.Join(", ", request.HouseholdIds)}"
            : "all households";
        return $"Analyze {scope} in the '{request.DatasetProfile}' dataset for scenario {request.Scenario}.";
    }
}
