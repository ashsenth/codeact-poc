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

        // One unmeasured warm-up per mode.
        _ = await classicService.AnalyzeAsync(request, cancellationToken).ConfigureAwait(false);
        _ = await codeactService.AnalyzeAsync(request, cancellationToken).ConfigureAwait(false);

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
                var record = await service.AnalyzeAsync(request, cancellationToken).ConfigureAwait(false);
                stopwatch.Stop();
                composition.RunStore.Save(record);

                var actualDto = record.Response.Report is null ? null : ReportJson.ToDto(record.Response.Report);
                var score = CorrectnessScorer.Score(
                    actualDto, oracleDto, record.Response.Status.ToString(), record.Response.Metrics.SchemaValid);

                var m = record.Response.Metrics;
                results.Add(new TrialResult(
                    pair, orderIndex, mode, stopwatch.Elapsed.TotalMilliseconds, record.Response.Status.ToString(),
                    m.ToolCallCount, m.ExecuteCodeCallCount, m.TotalTokens, m.EstimatedCostUsd, record.Response.RunId, score));
            }
        }

        string comparisonId = $"{profile}-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..8]}";
        return new ComparisonReport(
            comparisonId,
            DateTimeOffset.UtcNow.ToString("O"),
            profile,
            datasetHash,
            seed,
            WarmupsPerMode: 1,
            trials,
            fairness,
            Aggregator.Summarize("classic", results),
            Aggregator.Summarize("codeact", results),
            results);
    }

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
