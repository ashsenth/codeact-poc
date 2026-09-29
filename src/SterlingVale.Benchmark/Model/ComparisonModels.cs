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
    RunScore Score);

/// <summary>Median/min/max of a measured quantity (no trials removed).</summary>
public sealed record DurationStats(double Median, double Min, double Max);

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
    IReadOnlyList<TrialResult> RawTrials);
