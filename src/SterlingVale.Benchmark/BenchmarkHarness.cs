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
                    record = await service.AnalyzeAsync(request, cancellationToken).ConfigureAwait(false);
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
            results);
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
