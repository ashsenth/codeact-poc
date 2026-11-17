using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SterlingVale.AgentShared.Analysis;
using SterlingVale.AgentShared.Contracts;
using SterlingVale.Application.Oracle;
using SterlingVale.Benchmark;
using SterlingVale.Benchmark.Scoring;
using SterlingVale.Domain.Analysis;
using SterlingVale.Telemetry;

namespace SterlingVale.Diagnostics;

public static class FeasibilityControl
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length != 2)
            throw new ArgumentException("Usage: control <reference-program.py> <new-control-artifacts-directory>");
        string program = Path.GetFullPath(args[0]);
        string root = Path.GetFullPath(args[1]);
        if (!File.Exists(program) || Directory.Exists(root))
            throw new InvalidOperationException("Supply an existing program and a new isolated control directory.");
        Environment.SetEnvironmentVariable("FEASIBILITY_PROGRAM_PATH", program);
        Environment.SetEnvironmentVariable("ARTIFACTS_ROOT", root);
        Environment.SetEnvironmentVariable("BENCHMARK_TIMEOUT_SECONDS", "120");
        Environment.SetEnvironmentVariable("BENCHMARK_MAX_TURNS", "20");
        Environment.SetEnvironmentVariable("EXECUTION_PAYLOAD_CAPTURE", "full");
        using var composition = new Composition("fake");
        if (!composition.Hyperlight.HasGuest)
            throw new InvalidOperationException("A real HYPERLIGHT_PYTHON_GUEST_PATH is required; no stand-in allowed.");
        Directory.CreateDirectory(root);
        var snapshot = composition.Snapshots.GetSnapshot("small");
        var rows = new List<object>();
        bool passed = true;
        foreach (int count in new[] { 1, 3, 10 })
        {
            var request = new AnalysisRequest
            {
                DatasetProfile = "small",
                HouseholdIds = count == 10 ? null : snapshot.Households.Take(count).Select(h => h.Id.Value).ToArray(),
            };
            string scope = count == 10 ? "full-small" : $"n{count}";
            var clock = Stopwatch.StartNew();
            var record = await BenchmarkWorker.RunAsync(composition, "codeact", request,
                "control-no-llm-" + scope, "control-" + scope, CancellationToken.None,
                workerAssemblyPath: typeof(FeasibilityControl).Assembly.Location).ConfigureAwait(false);
            clock.Stop();
            composition.RunStore.Save(record);
            var oracle = ReportJson.ToDto(new ExposureOracle().Analyze(snapshot, request, "oracle", DateTimeOffset.UtcNow));
            var targets = snapshot.Households.ToDictionary(h => h.Id.Value,
                h => (IReadOnlyList<string>)snapshot.Policy(h.Id).Targets.Select(t => t.AssetClass.ToString()).ToArray());
            var score = CorrectnessScorer.Score(record.Response.Report is null ? null : ReportJson.ToDto(record.Response.Report),
                oracle, record.Response.Status.ToString(), record.Response.Metrics.SchemaValid, targets);
            var stdout = record.Events.Where(e => e.EventType == "CODE_EXECUTION_STDOUT")
                .Select(e => JsonSerializer.SerializeToElement(e.Data).GetProperty("payload").Deserialize<CapturedPayload>(Json)!).ToArray();
            var results = record.Events.Where(e => e.EventType == "CODE_EXECUTION_COMPLETED")
                .Select(e => JsonSerializer.SerializeToElement(e.Data).GetProperty("result").Deserialize<CapturedPayload>(Json)!).ToArray();
            bool echoed = results.Length == 1 && stdout.Length == 1 && stdout[0].Content == record.RawModelOutput;
            bool correct = score.MatchClass is CorrectnessScorer.Match.Exact or CorrectnessScorer.Match.WithinTolerance;
            bool callbacksAllowed = record.ToolCalls.All(call => call.Name is
                "list_households" or "get_policy" or "list_accounts" or "list_positions" or "get_price" or "get_fx");
            passed &= correct && echoed && callbacksAllowed && record.Response.Metrics.TelemetryValid;
            rows.Add(new
            {
                scope, runId = record.Response.RunId, status = record.Response.Status.ToString(), score,
                analysisMs = record.Response.Metrics.Duration.TotalMilliseconds, workerWallMs = clock.Elapsed.TotalMilliseconds,
                executionMs = record.Events.Where(e => e.EventType == "CODE_EXECUTION_COMPLETED").Sum(e => e.DurationMs),
                callbackCount = record.ToolCalls.Count, callbacksAllowed, stdoutEchoedVerbatim = echoed,
                outputBytes = Encoding.UTF8.GetByteCount(record.RawModelOutput),
                stdoutCharacters = stdout.Select(p => p.OriginalLength).ToArray(),
                stdoutBytes = stdout.Select(p => p.Content is null ? (int?)null : Encoding.UTF8.GetByteCount(p.Content)).ToArray(),
                stdoutCaptureTruncated = stdout.Any(p => p.Truncated),
                resultCharacters = results.Select(p => p.OriginalLength).ToArray(),
                resultCaptureTruncated = results.Any(p => p.Truncated),
                telemetryValid = record.Response.Metrics.TelemetryValid,
            });
            Console.WriteLine($"CONTROL ONLY {scope}: {score.MatchClass}; {record.ToolCalls.Count} callbacks; {record.Response.Metrics.Duration.TotalMilliseconds:F3} ms");
        }
        await File.WriteAllTextAsync(Path.Combine(root, "control-results.json"), JsonSerializer.Serialize(new
        {
            kind = "hand-authored-no-llm-feasibility-control", modelCalls = 0, timeoutSeconds = 120,
            payloadCaptureLimitCharacters = new PayloadPolicy().MaximumCharacters,
            programSha256 = Convert.ToHexStringLower(SHA256.HashData(await File.ReadAllBytesAsync(program).ConfigureAwait(false))),
            datasetHash = composition.Snapshots.GetDatasetHash("small"), passed, rows,
        }, Json)).ConfigureAwait(false);
        return passed ? 0 : 1;
    }
}
