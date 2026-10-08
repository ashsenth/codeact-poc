using System.ClientModel;
using System.Diagnostics;
using SterlingVale.AgentShared.Agents;
using SterlingVale.AgentShared.Analysis;
using SterlingVale.AgentShared.Contracts;
using SterlingVale.Application.Oracle;
using SterlingVale.Benchmark.Model;
using SterlingVale.Benchmark.Scoring;
using SterlingVale.CodeAct;
using SterlingVale.Domain.Analysis;
using SterlingVale.Domain.Model;
using SterlingVale.Tools;

namespace SterlingVale.Benchmark;

/// <summary>
/// Runs the paired A/B benchmark for one dataset profile: one unmeasured warm-up per mode, then N
/// measured paired trials with randomized mode order, scoring each run against the oracle and
/// checking fairness fingerprints. Raw results are retained; no trial is ever dropped.
/// </summary>
public sealed class BenchmarkHarness(Composition composition)
{
    private static readonly DateTimeOffset OracleAsOf = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>Executes the benchmark for a profile and returns the comparison report.</summary>
    public async Task<ComparisonReport> RunAsync(string profile, int trials, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profile);

        var snapshot = composition.Snapshots.GetSnapshot(profile);
        string datasetHash = composition.Snapshots.GetDatasetHash(profile);
        var request = new AnalysisRequest { DatasetProfile = profile };
        var householdIds = snapshot.Households.Select(h => h.Id.Value).ToList();

        var oracleDto = ReportJson.ToDto(new ExposureOracle().Analyze(snapshot, request, "oracle", OracleAsOf));

        var classicFactory = new ClassicAgentFactory();
        var codeactFactory = new CodeActAgentFactory(composition.Hyperlight);
        var classicService = composition.ServiceFor(classicFactory);
        var codeactService = composition.ServiceFor(codeactFactory);

        var toolService = new PortfolioToolService(snapshot, composition.ToolMetrics);
        var classicFingerprints = FairnessFingerprints.Create(
            classicFactory.ComputeToolFingerprint(toolService), datasetHash, composition.Model, composition.Pricing);
        var codeactFingerprints = FairnessFingerprints.Create(
            codeactFactory.ComputeToolFingerprint(toolService), datasetHash, composition.Model, composition.Pricing);
        var fairness = BuildFairness(classicFingerprints, codeactFingerprints);

        long seed = SeedFromHash(datasetHash);
        var random = new Random((int)(seed & 0x7FFFFFFF));

        // One unmeasured warm-up per mode; a warm-up failure is non-fatal (measured trials record it).
        await WarmUpAsync(classicService, request, cancellationToken).ConfigureAwait(false);
        await WarmUpAsync(codeactService, request, cancellationToken).ConfigureAwait(false);

        var results = new List<TrialResult>();
        for (int pair = 1; pair <= trials; pair++)
        {
            bool classicFirst = random.Next(2) == 0;
            (string Mode, AnalysisService Service)[] order = classicFirst
                ? [("classic", classicService), ("codeact", codeactService)]
                : [("codeact", codeactService), ("classic", classicService)];

            for (int orderIndex = 0; orderIndex < order.Length; orderIndex++)
            {
                var (mode, service) = order[orderIndex];
                var stopwatch = Stopwatch.StartNew();
                RunRecord record;
                try
                {
                    record = await RunModeAsync(mode, service, request, householdIds, cancellationToken).ConfigureAwait(false);
                }
                catch (ClientResultException ex)
                {
                    // A provider rejection (e.g. Classic exceeding the tool-call cap on larger datasets)
                    // is recorded as a failed trial so the paired comparison completes instead of aborting.
                    stopwatch.Stop();
                    Console.Error.WriteLine($"[{mode}] provider error on pair {pair}: {ex.Message}");
                    results.Add(FailedTrial(pair, orderIndex, mode, stopwatch.Elapsed.TotalMilliseconds));
                    continue;
                }

                stopwatch.Stop();
                composition.RunStore.Save(record);

                var actualDto = record.Response.Report is null ? null : ReportJson.ToDto(record.Response.Report);
                var score = CorrectnessScorer.Score(
                    actualDto, oracleDto, record.Response.Status.ToString(), record.Response.Metrics.SchemaValid);

                var m = record.Response.Metrics;
                results.Add(new TrialResult(
                    pair, orderIndex, mode, stopwatch.Elapsed.TotalMilliseconds, record.Response.Status.ToString(),
                    m.ToolCallCount, m.ModelTurnCount, m.ExecuteCodeCallCount, m.TotalTokens, m.EstimatedCostUsd, record.Response.RunId, score));
            }
        }

        string comparisonId = $"{profile}-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..8]}";
        var classicSummary = Aggregator.Summarize("classic", results);
        var codeactSummary = Aggregator.Summarize("codeact", results);
        return new ComparisonReport(
            comparisonId,
            DateTimeOffset.UtcNow.ToString("O"),
            profile,
            datasetHash,
            seed,
            WarmupsPerMode: 1,
            trials,
            snapshot.Households.Count,
            fairness,
            classicSummary,
            codeactSummary,
            Aggregator.ComputeDelta(classicSummary, codeactSummary),
            results,
            composition.ClassicBatchSize);
    }

    // Classic cannot emit a correct report over many households in one model run (it skips pricing and
    // returns an empty array at scale). When a batch size is set, run Classic once per batch of
    // households and merge, so direct tool-calling can complete; metrics are summed across the batches.
    private async Task<RunRecord> RunModeAsync(
        string mode, AnalysisService service, AnalysisRequest request, IReadOnlyList<string> householdIds, CancellationToken cancellationToken)
    {
        int batchSize = composition.ClassicBatchSize;
        if (mode != "classic" || batchSize <= 0 || householdIds.Count <= batchSize)
        {
            return await service.AnalyzeAsync(request, cancellationToken).ConfigureAwait(false);
        }

        var batches = new List<RunRecord>();
        for (int i = 0; i < householdIds.Count; i += batchSize)
        {
            var ids = householdIds.Skip(i).Take(batchSize).ToList();
            batches.Add(await service.AnalyzeAsync(request with { HouseholdIds = ids }, cancellationToken).ConfigureAwait(false));
        }

        return MergeBatches(batches);
    }

    private static RunRecord MergeBatches(IReadOnlyList<RunRecord> batches)
    {
        var first = batches[0];
        var households = new List<HouseholdExposure>();
        bool allCompleted = true;
        foreach (var b in batches)
        {
            if (b.Response.Report is { } report)
            {
                households.AddRange(report.Households);
            }

            if (b.Response.Status != RunStatus.Completed || b.Response.Report is null || b.Response.Report.Households.Count == 0)
            {
                allCompleted = false;
            }
        }

        var merged = households.Count > 0
            ? new ExposureReport($"classic-batched-{Guid.NewGuid().ToString("N")[..8]}", DateTimeOffset.UtcNow, households)
            : null;
        bool valid = allCompleted && merged is not null;
        var metrics = MergeMetrics(batches.Select(b => b.Response.Metrics).ToList(), valid);

        return new RunRecord
        {
            Response = new AnalysisResponse
            {
                RunId = first.Response.RunId,
                Mode = first.Mode,
                Status = valid ? RunStatus.Completed : RunStatus.Failed,
                Report = merged,
                Metrics = metrics,
            },
            Mode = first.Mode,
            RawModelOutput = merged is not null ? ReportJson.Serialize(merged, indented: true) : string.Empty,
            ToolCalls = batches.SelectMany(b => b.ToolCalls).ToList(),
            Transcript = batches.SelectMany(b => b.Transcript).ToList(),
            Fingerprints = first.Fingerprints,
        };
    }

    private static RunMetrics MergeMetrics(IReadOnlyList<RunMetrics> parts, bool valid)
    {
        var byName = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var part in parts)
        {
            foreach (var (name, count) in part.ToolCallsByName)
            {
                byName[name] = byName.GetValueOrDefault(name) + count;
            }
        }

        long? SumTokens(Func<RunMetrics, long?> select) =>
            parts.Any(p => select(p) is not null) ? parts.Sum(p => select(p) ?? 0L) : null;

        return new RunMetrics
        {
            Duration = TimeSpan.FromMilliseconds(parts.Sum(p => p.Duration.TotalMilliseconds)),
            ModelRequestCount = parts.Sum(p => p.ModelRequestCount),
            ModelTurnCount = parts.Sum(p => p.ModelTurnCount),
            ToolCallCount = parts.Sum(p => p.ToolCallCount),
            ToolCallsByName = byName,
            ExecuteCodeCallCount = 0,
            InputTokens = SumTokens(p => p.InputTokens),
            OutputTokens = SumTokens(p => p.OutputTokens),
            TotalTokens = SumTokens(p => p.TotalTokens),
            EstimatedCostUsd = parts.Any(p => p.EstimatedCostUsd is not null) ? parts.Sum(p => p.EstimatedCostUsd ?? 0m) : null,
            RetryCount = parts.Sum(p => p.RetryCount),
            SchemaValid = valid,
        };
    }

    private static async Task WarmUpAsync(AnalysisService service, AnalysisRequest request, CancellationToken cancellationToken)
    {
        try
        {
            _ = await service.AnalyzeAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (ClientResultException)
        {
            // Warm-up errors are non-fatal; measured trials capture provider failures.
        }
    }

    private static TrialResult FailedTrial(int pair, int orderIndex, string mode, double durationMs) =>
        new(pair, orderIndex, mode, durationMs, "Failed", 0, 0, 0, null, null, "failed",
            RunScore.ForFailure("Failed", schemaValid: false, CorrectnessScorer.Match.Failed));

    private static FairnessReport BuildFairness(FairnessFingerprints classic, FairnessFingerprints codeact)
    {
        var classicMap = classic.ToDictionary();
        var codeactMap = codeact.ToDictionary();
        var mismatches = classicMap
            .Where(kv => !codeactMap.TryGetValue(kv.Key, out var other) || other != kv.Value)
            .Select(kv => kv.Key)
            .OrderBy(k => k, StringComparer.Ordinal)
            .ToList();
        return new FairnessReport(mismatches.Count == 0, mismatches, classicMap, codeactMap);
    }

    private static long SeedFromHash(string datasetHash) =>
        long.TryParse(datasetHash.AsSpan(0, Math.Min(15, datasetHash.Length)),
            System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out var seed)
            ? seed
            : 1L;
}
