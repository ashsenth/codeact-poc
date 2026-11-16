using System.Text.Json.Serialization;
using SterlingVale.AgentShared.Analysis;
using SterlingVale.Benchmark.Scoring;

namespace SterlingVale.Benchmark.Model;

/// <summary>One measured trial of one mode within a pair.</summary>
public sealed record TrialResult(
    int PairId,
    int OrderInPair,
    string Mode,
    double DurationMs,
    string Status,
    int ToolCallCount,
    int ModelTurnCount,
    int ExecuteCodeCallCount,
    long? TotalTokens,
    decimal? EstimatedCostUsd,
    string RunId,
    RunScore Score)
{
    public string? TrialId { get; init; }
    public string? CorrelationId { get; init; }
    public bool TelemetryValid { get; init; }
    public int ModelFacingToolCalls { get; init; }
    public int GranularToolCalls { get; init; }
    public int SandboxCallbacks { get; init; }
    public IReadOnlyList<string> ChildRunIds { get; init; } = [];
    public FailureInfo? Failure { get; init; }
    public RunDiagnostics? Diagnostics { get; init; }
    public long? InputTokens { get; init; }
    public long? OutputTokens { get; init; }
    public string? ReportStatus { get; init; }
    public IReadOnlyList<string> MissingItems { get; init; } = [];
    public int? PricedPositionCount { get; init; }
    public IReadOnlyList<string> UnpricedSymbols { get; init; } = [];
    public double? AnalysisDurationMs { get; init; }

    public bool InfrastructureFailed => Failure is { HttpStatus: not null } ||
        Failure is { Category: not ("InvalidModelOutput" or "IncompleteEvidence" or "TurnLimit" or "TimedOut") } ||
        Status is not ("Completed" or "Incomplete" or "Capped" or "TimedOut" or "Failed") ||
        Status == "Failed" && Score.MatchClass != CorrectnessScorer.Match.InvalidSchema;
}

/// <summary>Median/min/max of a measured quantity (no trials removed).</summary>
public sealed record DurationStats(double? Median, double? Min, double? Max);

/// <summary>Token aggregates; <see cref="Complete"/> is false when any trial lacked usage metadata.</summary>
public sealed record TokenStats(long? Median, long? Min, long? Max, bool Complete);

/// <summary>Cost aggregates (USD); <see cref="Complete"/> is false when any trial lacked a cost estimate.</summary>
public sealed record CostStats(decimal? Median, decimal? Min, decimal? Max, bool Complete);

/// <summary>Median round-trip counts per mode: the mechanism behind any compute reduction.</summary>
public sealed record RoundTripStats(double MedianToolCalls, double MedianModelTurns, double MedianExecuteCodeCalls);

/// <summary>Aggregated correctness across a mode's trials.</summary>
public sealed record CorrectnessStats(
    double MeanFlaggedPrecision,
    double MeanFlaggedRecall,
    double MeanBreachPrecision,
    double MeanBreachRecall,
    decimal MeanAllocationMae,
    decimal MeanTradeNotionalMae,
    decimal MaxAllocationError,
    int ExactCount,
    int WithinToleranceCount,
    int PartialCount,
    int FailedCount);

/// <summary>Per-mode summary over the measured trials.</summary>
public sealed record ModeSummary(
    string Mode,
    int Trials,
    DurationStats DurationMs,
    TokenStats TotalTokens,
    CostStats? Cost,
    RoundTripStats? RoundTrips,
    CorrectnessStats Correctness);

/// <summary>Fairness verdict: fingerprints of both modes and any mismatches.</summary>
public sealed record FairnessReport(
    bool Matched,
    IReadOnlyList<string> Mismatches,
    IReadOnlyDictionary<string, string> ClassicFingerprints,
    IReadOnlyDictionary<string, string> CodeActFingerprints);

/// <summary>
/// CodeAct measured relative to Classic (the baseline). Cost-like deltas are fractions where a
/// negative value means CodeAct used less; a delta is null when the baseline is zero or a value is
/// unavailable. <see cref="DurationSpeedupFactor"/> is Classic median / CodeAct median. Correctness
/// is a plain parity delta (CodeAct minus Classic; higher is better).
/// </summary>
public sealed record ComparisonDelta(
    double? DurationDeltaFraction,
    double? DurationSpeedupFactor,
    double? TokenDeltaFraction,
    double? CostDeltaFraction,
    double? ToolCallDeltaFraction,
    double? ModelTurnDeltaFraction,
    int ExactCountDelta,
    decimal AllocationMaeDelta);

/// <summary>The full comparison output (serialized to JSON and rendered to Markdown).</summary>
public sealed record ComparisonReport(
    string ComparisonId,
    string GeneratedAtUtc,
    string Profile,
    string DatasetHash,
    long RandomizationSeed,
    int WarmupsPerMode,
    int MeasuredTrials,
    int? DatasetHouseholdCount,
    FairnessReport Fairness,
    ModeSummary Classic,
    ModeSummary CodeAct,
    ComparisonDelta? Delta,
    IReadOnlyList<TrialResult> RawTrials,
    int ClassicBatchSize = 0)
{
    [JsonPropertyOrder(-10)]
    public ComparisonOutcome Outcome => new(
        RawTrials.Count(trial => trial.Status == "Completed"), RawTrials.Count,
        RawTrials.Count(trial => trial.Status != "Completed"),
        RawTrials.Count(trial => trial.Score.MatchClass is CorrectnessScorer.Match.Exact or CorrectnessScorer.Match.WithinTolerance),
        RawTrials.Where(trial => trial.Status != "Completed")
            .Select(trial => trial.Failure?.Category ?? trial.Status).Distinct(StringComparer.Ordinal).ToArray(),
        Fairness.Matched && RawTrials.All(trial => trial.TelemetryValid),
        InfrastructureFailures());

    public ComparisonReport ForPresentation() => !Outcome.IsValid ? this with
    {
        Classic = SuppressStatistics(Classic), CodeAct = SuppressStatistics(CodeAct), Delta = null,
    } : Outcome.CorrectRuns == Outcome.TotalRuns ? this : this with { Delta = null };

    private IReadOnlyList<string> InfrastructureFailures()
    {
        var failures = RawTrials.Where(trial => trial.InfrastructureFailed)
            .Select(trial => trial.Failure?.Category ?? trial.Status).Concat(WarmupFailures.Select(failure => failure.Category)).ToList();
        if (!Fairness.Matched) failures.Add("FairnessMismatch: " + string.Join(", ", Fairness.Mismatches));
        if (RawTrials.Any(trial => !trial.TelemetryValid)) failures.Add("TelemetryReconciliationFailed");
        if (RawTrials.Count != MeasuredTrials * 2 ||
            RawTrials.GroupBy(trial => trial.PairId).Any(pair => pair.Count() != 2 ||
                pair.Count(trial => trial.Mode == "classic") != 1 || pair.Count(trial => trial.Mode == "codeact") != 1))
            failures.Add("MissingOrUnpairedTrials");
        return failures.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static ModeSummary SuppressStatistics(ModeSummary summary) => summary with
    {
        DurationMs = new(null, null, null),
        TotalTokens = new(null, null, null, false),
        Cost = null, RoundTrips = null,
    };

    public string ExecutionKind { get; init; } = "unavailable";
    public int? DatasetAccountCount { get; init; }
    public int? DatasetPositionCount { get; init; }
    public int? ScopedHouseholdCount { get; init; }
    public int? ScopedPositionCount { get; init; }
    public int? ScopedSymbolCount { get; init; }
    public string? PromptHash => Fairness.ClassicFingerprints.GetValueOrDefault("prompt");
    public string? ModelFingerprint => Fairness.ClassicFingerprints.GetValueOrDefault("model");
    public string? PreviousPromptHash { get; init; }
    public string? PromptRevision { get; init; }
    public IReadOnlyList<FailureInfo> WarmupFailures { get; init; } = [];
}

public sealed record ComparisonOutcome(int CompletedRuns, int TotalRuns, int FailedRuns, int CorrectRuns,
    IReadOnlyList<string> FailureCategories, bool EvidenceValid, IReadOnlyList<string> InfrastructureFailureCategories)
{
    public bool IsValid => TotalRuns > 0 && EvidenceValid && InfrastructureFailureCategories.Count == 0;
    public string Message => IsValid
        ? $"COMPARISON VALID: model task outcomes recorded; oracle correct {CorrectRuns}/{TotalRuns}"
        : $"COMPARISON INVALID: infrastructure/harness failure ({string.Join(", ", InfrastructureFailureCategories)})";
}
