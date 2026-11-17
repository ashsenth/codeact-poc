using System.Text.Json;
using SterlingVale.AgentShared.Analysis;
using SterlingVale.AgentShared.Configuration;
using SterlingVale.Benchmark.Model;
using SterlingVale.Benchmark.Scoring;
using SterlingVale.Domain.Analysis;
using SterlingVale.Telemetry;

namespace SterlingVale.Benchmark.Reporting;

/// <summary>Derives new classifications from preserved run evidence without rewriting source files.</summary>
public static class ComparisonReclassifier
{
    public static ComparisonReport Reclassify(ComparisonReport source, string sourceRoot)
    {
        var warmups = new List<TrialResult>();
        var pricing = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string directory in Directory.EnumerateDirectories(Path.Combine(sourceRoot, "runs")))
        {
            var events = ReadEvents(directory);
            if (!events.Any(entry => entry.ComparisonId == (source.ReclassifiedFrom ?? source.ComparisonId))) continue;
            using var metadata = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "metadata.json")));
            var configuration = metadata.RootElement.GetProperty("configuration");
            string mode = configuration.GetProperty("mode").GetString()!;
            var price = metadata.RootElement.GetProperty("pricingConfiguration")
                .Deserialize<PricingConfiguration>(JsonSerializerOptions.Web)
                ?? throw new InvalidDataException("Missing pricing evidence.");
            string fingerprint = price.Fingerprint();
            if (pricing.TryGetValue(mode, out string? previous) && previous != fingerprint)
                throw new InvalidDataException("Numeric pricing differs within one mode.");
            pricing[mode] = fingerprint;
            if (!events.Any(entry => entry.IsWarmup)) continue;
            using var result = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "result.json")));
            var data = result.RootElement;
            var m = JsonSerializer.Deserialize<RunMetrics>(File.ReadAllText(Path.Combine(directory, "metrics.json")),
                JsonSerializerOptions.Web) ?? throw new InvalidDataException("Missing warm-up metrics.");
            string status = data.GetProperty("status").GetString()!;
            var failure = RunValidity.FindFailure(status, events);
            var original = source.WarmupTrials.FirstOrDefault(trial => trial.RunId == data.GetProperty("runId").GetString());
            if (original is not null)
            {
                warmups.Add(original with
                {
                    Failure = failure, ToolArgumentErrors = RunValidity.ToolArgumentErrors(events),
                    SandboxOutputOverflowErrors = events.Count(entry => entry.EventType == "SANDBOX_OUTPUT_OVERFLOW"),
                });
                continue;
            }
            warmups.Add(new(0, 0, mode, m.Duration.TotalMilliseconds, status,
                m.ToolCallCount, m.ModelTurnCount, m.ExecuteCodeCallCount, m.TotalTokens,
                m.EstimatedCostUsd, data.GetProperty("runId").GetString()!,
                RunScore.ForFailure(status, m.SchemaValid, failure?.Category == "InvalidModelOutput"
                    ? CorrectnessScorer.Match.InvalidSchema : "Unassessed"))
            {
                Failure = failure, ToolArgumentErrors = RunValidity.ToolArgumentErrors(events),
                TelemetryValid = m.TelemetryValid,
                SandboxOutputOverflowErrors = events.Count(entry => entry.EventType == "SANDBOX_OUTPUT_OVERFLOW"),
            });
        }
        var trials = source.RawTrials.Select(trial =>
        {
            var events = ReadEvents(Path.Combine(sourceRoot, "runs", trial.RunId));
            if (trial.ChildRunIds.Count > 0)
                throw new InvalidDataException("Reclassification of batched historical runs is not supported.");
            return trial with
            {
                Failure = RunValidity.FindFailure(trial.Status, events),
                ToolArgumentErrors = RunValidity.ToolArgumentErrors(events),
                SandboxOutputOverflowErrors = events.Count(entry => entry.EventType == "SANDBOX_OUTPUT_OVERFLOW"),
            };
        }).ToArray();
        var classicFp = new Dictionary<string, string>(source.Fairness.ClassicFingerprints) { ["pricing"] = pricing["classic"] };
        var codeactFp = new Dictionary<string, string>(source.Fairness.CodeActFingerprints) { ["pricing"] = pricing["codeact"] };
        var mismatches = source.Fairness.Mismatches.Where(key => key != "pricing")
            .Concat(classicFp.Keys.Union(codeactFp.Keys).Where(key => classicFp.GetValueOrDefault(key) != codeactFp.GetValueOrDefault(key)))
            .Distinct(StringComparer.Ordinal).ToArray();
        var classic = Aggregator.Summarize("classic", trials);
        var codeact = Aggregator.Summarize("codeact", trials);
        return (source with
        {
            ComparisonId = source.ComparisonId + "-reclassified", ReclassifiedFrom = source.ComparisonId,
            RawTrials = trials, WarmupTrials = warmups,
            WarmupFailures = warmups.Where(trial => trial.InfrastructureFailed || !trial.TelemetryValid)
                .Select(trial => trial.Failure ?? new("TelemetryReconciliationFailed", "Warm-up telemetry did not reconcile.")).ToArray(),
            Fairness = new(mismatches.Length == 0, mismatches, classicFp, codeactFp),
            Classic = classic, CodeAct = codeact, Delta = Aggregator.ComputeDelta(classic, codeact),
        }).ForPresentation();
    }

    private static ExecutionEvent[] ReadEvents(string directory) =>
        File.ReadLines(Path.Combine(directory, "events.jsonl")).Select(line =>
            JsonSerializer.Deserialize<ExecutionEvent>(line, JsonSerializerOptions.Web)
            ?? throw new InvalidDataException("Null execution event.")).ToArray();

}
