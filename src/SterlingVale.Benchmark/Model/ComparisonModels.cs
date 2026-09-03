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
    int ExecuteCodeCallCount,
    long? TotalTokens,
    decimal? EstimatedCostUsd,
    string RunId,
    RunScore Score);

/// <summary>Median/min/max of a measured quantity (no trials removed).</summary>
public sealed record DurationStats(double Median, double Min, double Max);

/// <summary>Token aggregates; <see cref="Complete"/> is false when any trial lacked usage metadata.</summary>
public sealed record TokenStats(long? Median, long? Min, long? Max, bool Complete);

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
    CorrectnessStats Correctness);

/// <summary>Fairness verdict: fingerprints of both modes and any mismatches.</summary>
public sealed record FairnessReport(
    bool Matched,
    IReadOnlyList<string> Mismatches,
    IReadOnlyDictionary<string, string> ClassicFingerprints,
    IReadOnlyDictionary<string, string> CodeActFingerprints);

/// <summary>The full comparison output (serialized to JSON and rendered to Markdown).</summary>
public sealed record ComparisonReport(
    string ComparisonId,
    string GeneratedAtUtc,
    string Profile,
    string DatasetHash,
    long RandomizationSeed,
    int WarmupsPerMode,
    int MeasuredTrials,
    FairnessReport Fairness,
    ModeSummary Classic,
    ModeSummary CodeAct,
    IReadOnlyList<TrialResult> RawTrials);
