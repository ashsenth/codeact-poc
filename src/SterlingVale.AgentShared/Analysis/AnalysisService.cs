using SterlingVale.AgentShared.Configuration;
using SterlingVale.AgentShared.Prompting;
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
    private static readonly System.Text.Json.JsonSerializerOptions Json = new(System.Text.Json.JsonSerializerDefaults.Web);

    /// <summary>Runs an analysis and returns the complete run record.</summary>
    public async Task<RunRecord> AnalyzeAsync(AnalysisRequest request, CancellationToken cancellationToken,
        string? comparisonId = null, string? trialId = null, bool isWarmup = false, string? runId = null)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var execution = new ExecutionSession(executionOptions?.ArtifactsRoot ?? Environment.GetEnvironmentVariable("ARTIFACTS_ROOT") ?? "artifacts",
            agentFactory.Mode, request.DatasetProfile, comparisonId, trialId, isWarmup: isWarmup, runId: runId);
        execution.ModelIsLive = chatClientProvider.IsLive;
        execution.Emit("RUN_STARTED");
        execution.Emit("EXECUTION_CONFIGURATION", data: new { model = chatClientProvider.IsLive ? "live" : "deterministic-fake",
            transport = "direct-aifunction", payloadCapture = execution.Payloads.Mode.ToString(), modelConfiguration = model });
        try
        {
        var snapshot = snapshots.GetSnapshot(request.DatasetProfile);
        string datasetHash = snapshots.GetDatasetHash(request.DatasetProfile);
        var toolService = new PortfolioToolService(snapshot, toolMetrics);
        string executionRunId = execution.Context.RunId;

        string toolFingerprint = agentFactory.ComputeToolFingerprint(toolService);
        var fingerprints = FairnessFingerprints.Create(toolFingerprint, datasetHash, model, pricing);

        using var chatClient = new ObservedFunctionClient(new ObservedChatClient(
            chatClientProvider.CreateChatClient(new ChatClientRequest(snapshot, request, executionRunId, agentFactory.Mode)), model.MaxTurns));
        using var bundle = agentFactory.Create(chatClient, toolService, model);

        var context = new RunContext
        {
            RunId = executionRunId,
            Mode = agentFactory.Mode,
            UserMessage = BuildUserMessage(request),
            Model = model,
            Pricing = pricing,
            Fingerprints = fingerprints,
        };

        var record = await runner.RunAsync(bundle.Agent, context, cancellationToken).ConfigureAwait(false);
        var diagnostics = RunDiagnostics.FromEvents(agentFactory.Mode, record.Response.Status, execution.Snapshot());
        record = record with { Diagnostics = diagnostics };
        execution.WriteArtifact("summary.json", System.Text.Json.JsonSerializer.Serialize(new
        {
            status = record.Response.Status.ToString(), diagnostics, metrics = record.Response.Metrics,
            reportStatus = record.Response.Report?.Status.ToString().ToLowerInvariant(),
            missingItems = execution.Payloads.Capture(record.Response.Report?.MissingItems ?? []),
            promptHash = context.Fingerprints.PromptHash, previousPromptHash = Prompt.PreviousSharedHash,
        }, Json));
        execution.WriteArtifact("metrics.json", System.Text.Json.JsonSerializer.Serialize(record.Response.Metrics));
        execution.WriteArtifact("result.json", System.Text.Json.JsonSerializer.Serialize(record.Response));
        if (agentFactory.Mode == "classic")
        {
            var captured = execution.Payloads.Capture(record.RawModelOutput);
            execution.WriteArtifact("classic/final-response.json", System.Text.Json.JsonSerializer.Serialize(new
            {
                source = "model", available = !string.IsNullOrEmpty(record.RawModelOutput), payload = captured, diagnostics,
            }));
            if (!string.IsNullOrEmpty(record.RawModelOutput))
            {
                execution.Emit("JSON_GENERATED", data: captured);
            }
        }

        execution.Emit("JSON_VALIDATION_COMPLETED", record.Response.Metrics.SchemaValid ? "ok" : "failed");
        var priorFailure = RunValidity.FindFailure(record.Response.Status.ToString(), execution.Snapshot());
        var fallbackFailure = record.Response.Status switch
        {
            RunStatus.NotExecuted => new FailureInfo("NoHyperlightGuest", "not executed: no Hyperlight guest"),
            RunStatus.TimedOut => new FailureInfo("TimedOut", "The per-run time limit expired."),
            RunStatus.Capped => new FailureInfo("TurnLimitReached", "The model request limit was reached."),
            RunStatus.Incomplete => new FailureInfo("IncompleteEvidence", "INCOMPLETE: model reported missing evidence; see missingItems."),
            _ => new FailureInfo("InvalidModelOutput", diagnostics.IncompleteReason is null
                ? "The model did not return a valid non-empty analysis report." : diagnostics.Summary),
        };
        execution.Emit(record.Response.Status == RunStatus.Completed ? "RUN_COMPLETED" : "RUN_FAILED",
            record.Response.Status.ToString(), record.Response.Metrics.Duration.TotalMilliseconds,
            errorCategory: record.Response.Status == RunStatus.Completed ? null : priorFailure?.Category ?? fallbackFailure.Category,
            data: new { diagnostics, reportStatus = record.Response.Report?.Status.ToString().ToLowerInvariant(),
                missingItems = execution.Payloads.Capture(record.Response.Report?.MissingItems ?? []),
                failure = record.Response.Status == RunStatus.Completed ? null :
                priorFailure ?? fallbackFailure });
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
