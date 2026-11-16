using System.Diagnostics;
using System.Text.Json;
using SterlingVale.AgentShared.Agents;
using SterlingVale.AgentShared.Analysis;
using SterlingVale.AgentShared.Contracts;
using SterlingVale.CodeAct;
using SterlingVale.Domain.Analysis;
using SterlingVale.Telemetry;
using SterlingVale.Tools;
using SterlingVale.Tools.Observability;

namespace SterlingVale.Benchmark;

/// <summary>Contains each benchmark analysis in a killable process, identically for both modes.</summary>
public static class BenchmarkWorker
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public sealed record Request(string Mode, AnalysisRequest Analysis, string RunId,
        string ComparisonId, string TrialId, bool IsWarmup);

    public sealed record Result(RunRecord Record, ExposureReportDto? Report);

    public static async Task<int> ExecuteAsync(string requestPath, string modelMode)
    {
        var request = JsonSerializer.Deserialize<Request>(await File.ReadAllTextAsync(requestPath).ConfigureAwait(false), Json)
            ?? throw new InvalidDataException("Missing benchmark worker request.");
        using var composition = new Composition(modelMode);
        IAgentFactory factory = request.Mode switch
        {
            "classic" => new ClassicAgentFactory(),
            "codeact" => new CodeActAgentFactory(composition.Hyperlight),
            _ => throw new InvalidDataException("Invalid benchmark worker mode."),
        };
        var record = await composition.ServiceFor(factory).AnalyzeAsync(request.Analysis, CancellationToken.None,
            request.ComparisonId, request.TrialId, request.IsWarmup, request.RunId).ConfigureAwait(false);
        var result = new Result(record with { Response = record.Response with { Report = null } },
            record.Response.Report is null ? null : ReportJson.ToDto(record.Response.Report));
        string resultPath = requestPath + ".result";
        await File.WriteAllTextAsync(resultPath + ".tmp", JsonSerializer.Serialize(result, Json)).ConfigureAwait(false);
        File.Move(resultPath + ".tmp", resultPath);
        return 0;
    }

    public static async Task<RunRecord> RunAsync(Composition composition, string mode, AnalysisRequest analysis,
        string comparisonId, string trialId, CancellationToken cancellationToken, bool isWarmup = false)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string runId = Guid.NewGuid().ToString("N");
        string directory = Path.Combine(Path.GetFullPath(composition.ArtifactsRoot), "runs", runId);
        Directory.CreateDirectory(directory);
        string requestPath = Path.Combine(directory, "worker-request.tmp");
        await File.WriteAllTextAsync(requestPath, JsonSerializer.Serialize(
            new Request(mode, analysis, runId, comparisonId, trialId, isWarmup), Json), cancellationToken).ConfigureAwait(false);
        var startInfo = new ProcessStartInfo("dotnet") { UseShellExecute = false };
        startInfo.ArgumentList.Add(typeof(BenchmarkWorker).Assembly.Location);
        startInfo.ArgumentList.Add("analysis-worker");
        startInfo.ArgumentList.Add("--request");
        startInfo.ArgumentList.Add(requestPath);
        startInfo.ArgumentList.Add("--model");
        startInfo.ArgumentList.Add(composition.Selection.Mode);
        var clock = Stopwatch.StartNew();
        try
        {
            using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Benchmark worker did not start.");
            bool exited;
            try
            {
                exited = await WaitForExitAsync(process, composition.Model.Timeout, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                var cancelled = Recover(composition, mode, analysis, runId, comparisonId, trialId, isWarmup,
                    clock.Elapsed, RunStatus.Failed, new FailureInfo("Cancelled", "Benchmark worker terminated after caller cancellation."));
                composition.RunStore.Save(cancelled);
                throw;
            }
            if (!exited || process.ExitCode != 0)
            {
                return Recover(composition, mode, analysis, runId, comparisonId, trialId, isWarmup, clock.Elapsed,
                    exited ? RunStatus.Failed : RunStatus.TimedOut,
                    exited ? new FailureInfo("WorkerFailed", $"Benchmark worker exited with code {process.ExitCode}.")
                        : new FailureInfo("TimedOut", "INCOMPLETE: per-run deadline expired; benchmark worker process tree terminated."));
            }
            var result = JsonSerializer.Deserialize<Result>(await File.ReadAllTextAsync(requestPath + ".result", cancellationToken)
                .ConfigureAwait(false), Json) ?? throw new InvalidDataException("Missing benchmark worker result.");
            return result.Record with
            {
                Response = result.Record.Response with { Report = result.Report is null ? null : ReportJson.ToDomain(result.Report) },
            };
        }
        finally
        {
            File.Delete(requestPath);
            File.Delete(requestPath + ".result");
            File.Delete(requestPath + ".result.tmp");
        }
    }

    /// <summary>Returns false at the deadline, after terminating and reaping the worker process tree.</summary>
    public static async Task<bool> WaitForExitAsync(Process process, TimeSpan timeout, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(process);
        try
        {
            await process.WaitForExitAsync(cancellationToken).WaitAsync(timeout, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(CancellationToken.None)
                    .WaitAsync(TimeSpan.FromSeconds(5), CancellationToken.None).ConfigureAwait(false);
            }
        }
    }

    internal static RunRecord Recover(Composition composition, string mode, AnalysisRequest analysis, string runId,
        string comparisonId, string trialId, bool isWarmup, TimeSpan duration, RunStatus status, FailureInfo failure)
    {
        string directory = Path.Combine(composition.ArtifactsRoot, "runs", runId);
        using var session = new ExecutionSession(composition.ArtifactsRoot, mode, analysis.DatasetProfile,
            comparisonId, trialId, isWarmup: isWarmup, runId: runId,
            resume: File.Exists(Path.Combine(directory, "events.jsonl")));
        session.Emit("WORKER_TERMINATED", status.ToString(), duration.TotalMilliseconds, failure.Category,
            new { failure, processTreeTerminated = true });
        var events = session.Snapshot();
        foreach (string prefix in new[] { "MODEL_REQUEST", "MODEL_TOOL_CALL", "EXECUTE_CODE_CALL", "CODE_EXECUTION", "TOOL_CALL" })
        {
            string key = prefix == "MODEL_REQUEST" ? "requestId" : "invocationId";
            foreach (var started in events.Where(entry => entry.EventType == prefix + "_STARTED"))
            {
                var data = JsonSerializer.SerializeToElement(started.Data, Json);
                string? id = data.GetProperty(key).GetString();
                if (events.Any(entry => (entry.EventType == prefix + "_COMPLETED" ||
                        entry.EventType == prefix + "_FAILED" || prefix == "MODEL_REQUEST" && entry.EventType == "MODEL_RESPONSE_RECEIVED") &&
                    JsonSerializer.SerializeToElement(entry.Data, Json).GetProperty(key).GetString() == id))
                    continue;
                var interrupted = new Dictionary<string, object?> { [key] = id, ["failure"] = failure,
                    ["terminatedBySupervisor"] = true };
                session.Emit(prefix + "_FAILED", status.ToString(), errorCategory: failure.Category, data: interrupted);
                if (prefix == "CODE_EXECUTION")
                {
                    int number = events.Where(entry => entry.EventType == "CODE_EXECUTION_STARTED")
                        .TakeWhile(entry => entry != started).Count() + 1;
                    session.WriteArtifact($"codeact/execution-{number:D3}.json", JsonSerializer.Serialize(interrupted, Json));
                }
            }
        }
        var metrics = TelemetryReconciler.FromEvidence(session, duration, false, null);
        var diagnostics = RunDiagnostics.FromEvents(mode, status, session.Snapshot()) with { IncompleteReason = failure.Message };
        var byName = events.Where(entry => entry.EventType == "MODEL_TOOL_CALL_STARTED")
            .Select(entry => JsonSerializer.SerializeToElement(entry.Data, Json).GetProperty("toolName").GetString()!)
            .GroupBy(name => name, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        metrics = metrics with { ToolCallsByName = byName,
            EstimatedCostUsd = composition.Pricing.EstimateCost(metrics.InputTokens, metrics.OutputTokens) };
        session.Emit("RUN_FAILED", status.ToString(), duration.TotalMilliseconds, failure.Category, new { failure, diagnostics });
        session.WriteArtifact("summary.json", JsonSerializer.Serialize(new { status = status.ToString(), diagnostics, metrics }, Json));
        var tools = new List<ToolCallRecord>();
        foreach (var entry in events.Where(entry => entry.EventType is "TOOL_CALL_COMPLETED" or "TOOL_CALL_FAILED"))
        {
            var tool = JsonSerializer.SerializeToElement(entry.Data, Json);
            tools.Add(new ToolCallRecord(tools.Count, tool.GetProperty("toolName").GetString()!,
                tool.GetProperty("arguments").Deserialize<CapturedPayload>(Json)?.Content,
                tool.GetProperty("response").Deserialize<CapturedPayload>(Json)?.Content,
                tool.GetProperty("durationMs").GetDouble(), tool.GetProperty("status").GetString()!,
                tool.GetProperty("endTime").GetDateTimeOffset()));
        }
        IAgentFactory factory = mode == "classic" ? new ClassicAgentFactory() : new CodeActAgentFactory(composition.Hyperlight);
        var toolService = new PortfolioToolService(composition.Snapshots.GetSnapshot(analysis.DatasetProfile));
        return new RunRecord
        {
            Response = new AnalysisResponse { RunId = runId, Mode = mode, Status = status, Metrics = metrics },
            Mode = mode, RawModelOutput = string.Empty, Execution = session.Context, Events = session.Snapshot(),
            Diagnostics = diagnostics, PayloadCaptureMode = session.Payloads.Mode, ToolCalls = tools,
            Fingerprints = FairnessFingerprints.Create(factory.ComputeToolFingerprint(toolService),
                composition.Snapshots.GetDatasetHash(analysis.DatasetProfile), composition.Model, composition.Pricing),
        };
    }
}
