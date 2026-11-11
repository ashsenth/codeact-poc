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
    ToolMetrics toolMetrics,
    ExecutionOptions? executionOptions = null)
{
    /// <summary>Runs an analysis and returns the complete run record.</summary>
    public async Task<RunRecord> AnalyzeAsync(AnalysisRequest request, CancellationToken cancellationToken,
        string? comparisonId = null, string? trialId = null, bool isWarmup = false)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var execution = new ExecutionSession(executionOptions?.ArtifactsRoot ?? Environment.GetEnvironmentVariable("ARTIFACTS_ROOT") ?? "artifacts",
            agentFactory.Mode, request.DatasetProfile, comparisonId, trialId, isWarmup: isWarmup);
        execution.ModelIsLive = chatClientProvider.IsLive;
        execution.Emit("RUN_STARTED");
        execution.Emit("EXECUTION_CONFIGURATION", data: new { model = chatClientProvider.IsLive ? "live" : "deterministic-fake",
            transport = "direct-aifunction", payloadCapture = execution.Payloads.Mode.ToString() });
        try
        {
        var snapshot = snapshots.GetSnapshot(request.DatasetProfile);
        string datasetHash = snapshots.GetDatasetHash(request.DatasetProfile);
        var toolService = new PortfolioToolService(snapshot, toolMetrics);
        string runId = execution.Context.RunId;

        string toolFingerprint = agentFactory.ComputeToolFingerprint(toolService);
        var fingerprints = FairnessFingerprints.Create(toolFingerprint, datasetHash, model, pricing);

        using var chatClient = new ObservedFunctionClient(new ObservedChatClient(
            chatClientProvider.CreateChatClient(new ChatClientRequest(snapshot, request, runId, agentFactory.Mode))));
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

        var record = await runner.RunAsync(bundle.Agent, context, cancellationToken).ConfigureAwait(false);
        execution.WriteArtifact("metrics.json", System.Text.Json.JsonSerializer.Serialize(record.Response.Metrics));
        execution.WriteArtifact("result.json", System.Text.Json.JsonSerializer.Serialize(record.Response));
        if (agentFactory.Mode == "classic")
        {
            var captured = execution.Payloads.Capture(record.RawModelOutput);
            execution.WriteArtifact("classic/final-response.json", System.Text.Json.JsonSerializer.Serialize(new
            {
                source = "model", available = !string.IsNullOrEmpty(record.RawModelOutput), payload = captured,
            }));
            if (!string.IsNullOrEmpty(record.RawModelOutput))
            {
                execution.Emit("JSON_GENERATED", data: captured);
            }
        }

        execution.Emit("JSON_VALIDATION_COMPLETED", record.Response.Metrics.SchemaValid ? "ok" : "failed");
        var priorFailure = execution.Snapshot().FirstOrDefault(entry => entry.ErrorCategory is not null);
        var fallbackFailure = record.Response.Status switch
        {
            RunStatus.NotExecuted => new FailureInfo("NoHyperlightGuest", "not executed: no Hyperlight guest"),
            RunStatus.TimedOut => new FailureInfo("TimedOut", "The per-run time limit expired."),
            RunStatus.Capped => new FailureInfo("TurnLimit", "The model turn limit was exceeded."),
            _ => new FailureInfo("InvalidModelOutput", "The model did not return a valid non-empty analysis report."),
        };
        execution.Emit(record.Response.Status == RunStatus.Completed ? "RUN_COMPLETED" : "RUN_FAILED",
            record.Response.Status.ToString(), record.Response.Metrics.Duration.TotalMilliseconds,
            errorCategory: record.Response.Status == RunStatus.Completed ? null : priorFailure?.ErrorCategory ?? fallbackFailure.Category,
            data: record.Response.Status == RunStatus.Completed ? null : priorFailure?.Data ?? new { failure = fallbackFailure });
        return record with { Execution = execution.Context, Events = execution.Snapshot(), PayloadCaptureMode = execution.Payloads.Mode };
        }
        catch (Exception exception)
        {
            var failure = FailureInfo.FromException(exception);
            execution.Emit("RUN_FAILED", "error", errorCategory: failure.Category, data: new { failure });
            execution.WriteArtifact("result.json", System.Text.Json.JsonSerializer.Serialize(new
            {
                execution.Context, status = "Failed", failure,
                callerCancelled = cancellationToken.IsCancellationRequested,
            }));
            throw;
        }
    }

    private static string BuildUserMessage(AnalysisRequest request)
    {
        string scope = request.HouseholdIds is { Count: > 0 }
            ? $"households {string.Join(", ", request.HouseholdIds)}"
            : "all households";
        return $"Analyze {scope} in the '{request.DatasetProfile}' dataset for scenario {request.Scenario}.";
    }
}
