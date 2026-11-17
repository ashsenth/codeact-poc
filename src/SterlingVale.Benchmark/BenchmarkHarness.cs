using System.ClientModel;
using System.Diagnostics;
using System.Text.Json;
using SterlingVale.AgentShared.Agents;
using SterlingVale.AgentShared.Analysis;
using SterlingVale.AgentShared.Contracts;
using SterlingVale.AgentShared.Prompting;
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
    public async Task<ComparisonReport> RunAsync(string profile, int trials, CancellationToken cancellationToken, int? households = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profile);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(trials);
        if (households is <= 0) throw new InvalidOperationException("--households must be a positive integer.");

        string comparisonId = $"{profile}-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}";
        var snapshot = composition.Snapshots.GetSnapshot(profile);
        string datasetHash = composition.Snapshots.GetDatasetHash(profile);
        if (households > snapshot.Households.Count)
            throw new InvalidOperationException("--households exceeds the number of households in the selected dataset.");
        var householdIds = snapshot.Households.Select(h => h.Id.Value).Take(households ?? snapshot.Households.Count).ToList();
        var request = new AnalysisRequest { DatasetProfile = profile, HouseholdIds = households is null ? null : householdIds };
        var scopedAccounts = snapshot.Accounts.Where(account => householdIds.Contains(account.HouseholdId.Value))
            .Select(account => account.Id).ToHashSet();
        var scopedPositions = snapshot.Positions.Where(position => scopedAccounts.Contains(position.AccountId)).ToArray();

        var oracleDto = ReportJson.ToDto(new ExposureOracle().Analyze(snapshot, request, "oracle", OracleAsOf));
        var policyTargetClasses = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        foreach (var household in snapshot.Households.Where(h => householdIds.Contains(h.Id.Value)))
            policyTargetClasses[household.Id.Value] = snapshot.Policy(household.Id).Targets
                .Select(target => target.AssetClass.ToString()).ToArray();

        var classicFactory = new ClassicAgentFactory();
        var codeactFactory = new CodeActAgentFactory(composition.Hyperlight);

        var toolService = new PortfolioToolService(snapshot, composition.ToolMetrics);
        var classicFingerprints = FairnessFingerprints.Create(
            classicFactory.ComputeToolFingerprint(toolService), datasetHash, composition.Model, composition.Pricing);
        var codeactFingerprints = FairnessFingerprints.Create(
            codeactFactory.ComputeToolFingerprint(toolService), datasetHash, composition.Model, composition.Pricing);
        var fairness = BuildFairness(classicFingerprints, codeactFingerprints);

        long seed = SeedFromHash(datasetHash);
        var random = new Random((int)(seed & 0x7FFFFFFF));

        var warmupFailures = new List<FailureInfo>();
        var warmupTrials = new List<TrialResult>();
        foreach (string mode in new[] { "classic", "codeact" })
        {
            var warmup = await WarmUpAsync(mode, request, comparisonId, oracleDto, policyTargetClasses,
                mode == "classic" ? classicFingerprints : codeactFingerprints, cancellationToken).ConfigureAwait(false);
            warmupTrials.Add(warmup);
            if (warmup.InfrastructureFailed || !warmup.TelemetryValid)
            {
                warmupFailures.Add(warmup.Failure ?? new("TelemetryReconciliationFailed", "Warm-up telemetry did not reconcile."));
                break;
            }
        }

        var results = new List<TrialResult>();
        bool infrastructureFailed = warmupFailures.Count > 0;
        for (int pair = 1; pair <= trials && !infrastructureFailed; pair++)
        {
            string trialId = Guid.NewGuid().ToString("N");
            bool classicFirst = random.Next(2) == 0;
            string[] order = classicFirst ? ["classic", "codeact"] : ["codeact", "classic"];

            for (int orderIndex = 0; orderIndex < order.Length; orderIndex++)
            {
                string mode = order[orderIndex];
                var stopwatch = Stopwatch.StartNew();
                RunRecord record;
                try
                {
                    record = await RunModeAsync(mode, request, householdIds, comparisonId, trialId, cancellationToken).ConfigureAwait(false);
                }
                catch (ClientResultException ex)
                {
                    stopwatch.Stop();
                    var failure = FailureInfo.FromException(ex);
                    Console.Error.WriteLine($"[{mode}] provider error on pair {pair}: {failure.Message}");
                    results.Add(FailedTrial(pair, orderIndex, mode, stopwatch.Elapsed.TotalMilliseconds) with { Failure = failure });
                    infrastructureFailed = true;
                    break;
                }

                stopwatch.Stop();
                composition.RunStore.Save(record);
                var expected = mode == "classic" ? classicFingerprints : codeactFingerprints;
                var runFingerprints = record.Fingerprints.ToDictionary();
                var runMismatches = expected.ToDictionary().Where(item =>
                    runFingerprints.GetValueOrDefault(item.Key) != item.Value)
                    .Select(item => $"{mode}:{item.Key}").ToArray();
                if (runMismatches.Length > 0)
                    fairness = fairness with { Matched = false, Mismatches = fairness.Mismatches.Concat(runMismatches).ToArray() };

                var actualDto = record.Response.Report is null ? null : ReportJson.ToDto(record.Response.Report);
                var score = CorrectnessScorer.Score(
                    actualDto, oracleDto, record.Response.Status.ToString(), record.Response.Metrics.SchemaValid, policyTargetClasses);

                var m = record.Response.Metrics;
                var priceCalls = (record.ChildRuns.Count == 0 ? record.ToolCalls : record.ChildRuns.SelectMany(child => child.ToolCalls))
                    .Where(call => call.Name == "get_price" && call.Status == "ok").ToArray();
                bool priceEvidenceAvailable = priceCalls.All(call => call.ArgumentsJson is not null);
                var pricedSymbols = priceCalls.Select(call => call.ArgumentsJson is { } arguments
                        ? JsonSerializer.Deserialize<JsonElement>(arguments).GetProperty("symbol").GetString() : null)
                    .OfType<string>().ToHashSet(StringComparer.Ordinal);
                results.Add(new TrialResult(
                    pair, orderIndex, mode, stopwatch.Elapsed.TotalMilliseconds, record.Response.Status.ToString(),
                    m.ToolCallCount, m.ModelTurnCount, m.ExecuteCodeCallCount, m.TotalTokens, m.EstimatedCostUsd, record.Response.RunId, score)
                {
                    TrialId = trialId, CorrelationId = record.Execution?.CorrelationId,
                    TelemetryValid = m.TelemetryValid, ModelFacingToolCalls = m.ModelFacingToolCallCount,
                    GranularToolCalls = m.GranularToolCallCount, SandboxCallbacks = m.SandboxCallbackCount,
                    ChildRunIds = record.ChildRuns.Select(child => child.Response.RunId).ToArray(),
                    Failure = FindFailure(record),
                    ToolArgumentErrors = ToolArgumentErrors(record),
                    Diagnostics = record.Diagnostics,
                    InputTokens = m.InputTokens, OutputTokens = m.OutputTokens,
                    CachedInputTokens = m.CachedInputTokens, ReasoningTokens = m.ReasoningTokens,
                    VisibleOutputTokens = m.VisibleOutputTokens, CacheWriteTokens = m.CacheWriteTokens,
                    Http429Count = m.Http429Count, RetryWaitMs = m.RetryWaitMs,
                    SandboxOutputOverflowErrors = m.SandboxOutputOverflowErrors, TurnLimitReached = m.TurnLimitReached,
                    AnalysisDurationMs = m.Duration.TotalMilliseconds,
                    ReportStatus = actualDto?.Status, MissingItems = actualDto?.MissingItems ?? [],
                    PricedPositionCount = priceEvidenceAvailable
                        ? scopedPositions.Count(position => pricedSymbols.Contains(position.Symbol.Value)) : null,
                    UnpricedSymbols = priceEvidenceAvailable ? scopedPositions.Select(position => position.Symbol.Value).Distinct(StringComparer.Ordinal)
                        .Where(symbol => !pricedSymbols.Contains(symbol)).Order(StringComparer.Ordinal).ToArray() : [],
                });
                if (results[^1].InfrastructureFailed || !m.TelemetryValid || !fairness.Matched)
                {
                    infrastructureFailed = true;
                    break;
                }
            }
        }

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
            composition.ClassicBatchSize)
        {
            ExecutionKind = !composition.Live ? "offline-fake-control" : composition.Hyperlight.HasGuest ? "live-model-hyperlight" : "live-model-code-generation-only",
            DatasetAccountCount = snapshot.Accounts.Count,
            DatasetPositionCount = snapshot.Positions.Count,
            ScopedHouseholdCount = householdIds.Count,
            ScopedPositionCount = scopedPositions.Length,
            ScopedSymbolCount = scopedPositions.Select(position => position.Symbol).Distinct().Count(),
            PromptRevision = Prompt.PromptId, PreviousPromptHash = Prompt.PreviousSharedHash,
            WarmupFailures = warmupFailures,
            WarmupTrials = warmupTrials,
        }.ForPresentation();
    }

    // Classic cannot emit a correct report over many households in one model run (it skips pricing and
    // returns an empty array at scale). When a batch size is set, run Classic once per batch of
    // households and merge, so direct tool-calling can complete; metrics are summed across the batches.
    private async Task<RunRecord> RunModeAsync(
        string mode, AnalysisRequest request, IReadOnlyList<string> householdIds,
        string comparisonId, string trialId, CancellationToken cancellationToken)
    {
        int batchSize = composition.ClassicBatchSize;
        if (mode != "classic" || batchSize <= 0 || householdIds.Count <= batchSize)
        {
            return await BenchmarkWorker.RunAsync(composition, mode, request, comparisonId, trialId, cancellationToken).ConfigureAwait(false);
        }

        var batches = new List<RunRecord>();
        for (int i = 0; i < householdIds.Count; i += batchSize)
        {
            var ids = householdIds.Skip(i).Take(batchSize).ToList();
            batches.Add(await BenchmarkWorker.RunAsync(composition, mode, request with { HouseholdIds = ids },
                comparisonId, trialId, cancellationToken).ConfigureAwait(false));
        }

        return MergeBatches(batches);
    }

    private RunRecord MergeBatches(IReadOnlyList<RunRecord> batches)
    {
        var first = batches[0];
        var households = new List<HouseholdExposure>();
        var missingItems = batches.Where(batch => batch.Response.Report?.Status == ReportStatus.Incomplete)
            .SelectMany(batch => batch.Response.Report!.MissingItems).Distinct(StringComparer.Ordinal).ToArray();
        bool anyIncomplete = batches.Any(batch => batch.Response.Report?.Status == ReportStatus.Incomplete);
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

        var merged = households.Count > 0 || anyIncomplete
            ? new ExposureReport($"classic-batched-{Guid.NewGuid().ToString("N")[..8]}", DateTimeOffset.UtcNow, households)
            { Status = anyIncomplete ? ReportStatus.Incomplete : ReportStatus.Complete, MissingItems = missingItems }
            : null;
        bool valid = allCompleted && merged is not null;
        bool onlyIncomplete = batches.All(batch => batch.Response.Status is RunStatus.Completed or RunStatus.Incomplete) && anyIncomplete;
        var mergedStatus = valid ? RunStatus.Completed : onlyIncomplete ? RunStatus.Incomplete : RunStatus.Failed;
        var metrics = MergeMetrics(batches.Select(b => b.Response.Metrics).ToList(), valid || onlyIncomplete);

        using var aggregate = new SterlingVale.Telemetry.ExecutionSession(composition.ArtifactsRoot, "classic", "aggregate",
            first.Execution?.ComparisonId, first.Execution?.TrialId);
        aggregate.Emit("BATCH_AGGREGATION_STARTED", data: new { childRunIds = batches.Select(batch => batch.Response.RunId).ToArray() });
        aggregate.Emit("BATCH_AGGREGATION_COMPLETED", valid ? "ok" : "failed");
        var aggregateDiagnostics = RunDiagnostics.FromEvents("classic",
            mergedStatus, batches.SelectMany(batch => batch.Events).ToArray());
        return new RunRecord
        {
            Response = new AnalysisResponse
            {
                RunId = aggregate.Context.RunId,
                Mode = first.Mode,
                Status = mergedStatus,
                Report = merged,
                Metrics = metrics,
            },
            Mode = first.Mode,
            RawModelOutput = string.Empty,
            Execution = aggregate.Context,
            Events = aggregate.Snapshot(),
            ChildRuns = batches,
            Fingerprints = first.Fingerprints,
            Diagnostics = aggregateDiagnostics,
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
            parts.All(p => select(p) is not null) ? parts.Sum(p => select(p)!.Value) : null;

        return new RunMetrics
        {
            Duration = TimeSpan.FromMilliseconds(parts.Sum(p => p.Duration.TotalMilliseconds)),
            ModelRequestCount = parts.Sum(p => p.ModelRequestCount),
            ModelTurnCount = parts.Sum(p => p.ModelTurnCount),
            ToolCallCount = parts.Sum(p => p.ToolCallCount),
            ModelFacingToolCallCount = parts.Sum(p => p.ModelFacingToolCallCount),
            GranularToolCallCount = parts.Sum(p => p.GranularToolCallCount),
            TelemetryValid = parts.All(p => p.TelemetryValid),
            TelemetryErrors = parts.SelectMany(p => p.TelemetryErrors).ToArray(),
            ToolCallsByName = byName,
            ExecuteCodeCallCount = 0,
            InputTokens = SumTokens(p => p.InputTokens),
            OutputTokens = SumTokens(p => p.OutputTokens),
            TotalTokens = SumTokens(p => p.TotalTokens),
            CachedInputTokens = SumTokens(p => p.CachedInputTokens), ReasoningTokens = SumTokens(p => p.ReasoningTokens),
            VisibleOutputTokens = SumTokens(p => p.VisibleOutputTokens), CacheWriteTokens = parts.Sum(p => p.CacheWriteTokens),
            Http429Count = parts.Sum(p => p.Http429Count), RetryWaitMs = parts.Sum(p => p.RetryWaitMs),
            SandboxOutputOverflowErrors = parts.Sum(p => p.SandboxOutputOverflowErrors),
            TurnLimitReached = parts.Sum(p => p.TurnLimitReached),
            EstimatedCostUsd = parts.All(p => p.EstimatedCostUsd is not null) ? parts.Sum(p => p.EstimatedCostUsd!.Value) : null,
            RetryCount = parts.Sum(p => p.RetryCount),
            SchemaValid = valid,
        };
    }

    private async Task<TrialResult> WarmUpAsync(string mode, AnalysisRequest request, string comparisonId,
        ExposureReportDto oracle, IReadOnlyDictionary<string, IReadOnlyList<string>> policyTargetClasses,
        FairnessFingerprints expected, CancellationToken cancellationToken)
    {
        try
        {
            var record = await BenchmarkWorker.RunAsync(composition, mode, request, comparisonId, "warmup",
                cancellationToken, isWarmup: true).ConfigureAwait(false);
            composition.RunStore.Save(record);
            var trial = AssessWarmup(record, oracle, policyTargetClasses);
            if (!expected.ToDictionary().OrderBy(item => item.Key).SequenceEqual(
                    record.Fingerprints.ToDictionary().OrderBy(item => item.Key)))
                trial = trial with { Failure = new("FairnessMismatch", "Warm-up configuration differs from the comparison.") };
            return trial;
        }
        catch (ClientResultException exception)
        {
            return FailedTrial(0, 0, mode, 0) with { Failure = FailureInfo.FromException(exception) };
        }
    }

    internal static FailureInfo? FindFailure(RunRecord record)
    {
        var failures = record.ChildRuns.Select(FindFailure).Prepend(
            RunValidity.FindFailure(record.Response.Status.ToString(), record.Events)).OfType<FailureInfo>().ToArray();
        if (failures.Length > 0)
            return failures.FirstOrDefault(failure => !RunValidity.IsModelOutcome(failure)) ?? failures[0];
        if (record.Response.Status == RunStatus.NotExecuted)
            return new FailureInfo("NoHyperlightGuest", "not executed: no Hyperlight guest");
        return null;
    }

    private static int ToolArgumentErrors(RunRecord record) => RunValidity.ToolArgumentErrors(record.Events) +
        record.ChildRuns.Sum(ToolArgumentErrors);

    internal static TrialResult AssessWarmup(RunRecord record, ExposureReportDto oracle,
        IReadOnlyDictionary<string, IReadOnlyList<string>> policyTargetClasses)
    {
        var m = record.Response.Metrics;
        var actual = record.Response.Report is null ? null : ReportJson.ToDto(record.Response.Report);
        return new(0, 0, record.Mode, m.Duration.TotalMilliseconds, record.Response.Status.ToString(),
            m.ToolCallCount, m.ModelTurnCount, m.ExecuteCodeCallCount, m.TotalTokens, m.EstimatedCostUsd,
            record.Response.RunId, CorrectnessScorer.Score(actual, oracle, record.Response.Status.ToString(),
                m.SchemaValid, policyTargetClasses))
        {
            Failure = FindFailure(record), TelemetryValid = m.TelemetryValid,
            ToolArgumentErrors = ToolArgumentErrors(record), Diagnostics = record.Diagnostics,
            InputTokens = m.InputTokens, OutputTokens = m.OutputTokens, AnalysisDurationMs = m.Duration.TotalMilliseconds,
            CachedInputTokens = m.CachedInputTokens, ReasoningTokens = m.ReasoningTokens,
            VisibleOutputTokens = m.VisibleOutputTokens, CacheWriteTokens = m.CacheWriteTokens,
            Http429Count = m.Http429Count, RetryWaitMs = m.RetryWaitMs,
            SandboxOutputOverflowErrors = m.SandboxOutputOverflowErrors, TurnLimitReached = m.TurnLimitReached,
            SandboxCallbacks = m.SandboxCallbackCount, ReportStatus = actual?.Status,
            MissingItems = actual?.MissingItems ?? [],
        };
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
