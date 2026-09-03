using SterlingVale.Domain.ValueObjects;

namespace SterlingVale.Domain.Analysis;

/// <summary>
/// The analysis scenario requested. A single scenario is defined for the benchmark so both modes
/// solve an identical problem; the enum leaves room for future scenarios.
/// </summary>
public enum AnalysisScenario
{
    /// <summary>
    /// Full exposure &amp; rebalancing analysis: value all households, compute allocations, evaluate
    /// drift and risk limits, and propose asset-class rebalancing notionals.
    /// </summary>
    ExposureAndRebalance = 0,
}

/// <summary>
/// The request accepted by both APIs. Identical shape and semantics across Classic and CodeAct so
/// the only difference is orchestration.
/// </summary>
public sealed record AnalysisRequest
{
    /// <summary>The scenario to run.</summary>
    public AnalysisScenario Scenario { get; init; } = AnalysisScenario.ExposureAndRebalance;

    /// <summary>
    /// Optional subset of household ids to analyze. When null or empty, all households in the
    /// active dataset snapshot are analyzed.
    /// </summary>
    public IReadOnlyList<string>? HouseholdIds { get; init; }

    /// <summary>
    /// The dataset profile to analyze ("small", "medium", "large"). The server validates this
    /// against the loaded snapshot and records its manifest hash.
    /// </summary>
    public string DatasetProfile { get; init; } = "small";
}

/// <summary>Completion status of an analysis run.</summary>
public enum RunStatus
{
    /// <summary>The run produced a schema-valid result.</summary>
    Completed = 0,

    /// <summary>The run hit the maximum turn/iteration cap before completing.</summary>
    Capped = 1,

    /// <summary>The run exceeded its timeout.</summary>
    TimedOut = 2,

    /// <summary>The run failed (error or invalid output).</summary>
    Failed = 3,
}

/// <summary>
/// The response returned by both APIs. Contains the run identifier, status, the exposure report
/// (when available), and the collected metrics.
/// </summary>
public sealed record AnalysisResponse
{
    /// <summary>Unique run identifier.</summary>
    public required string RunId { get; init; }

    /// <summary>The orchestration mode that produced this response ("classic" or "codeact").</summary>
    public required string Mode { get; init; }

    /// <summary>Completion status.</summary>
    public required RunStatus Status { get; init; }

    /// <summary>The exposure report, when the run produced schema-valid output; otherwise null.</summary>
    public Model.ExposureReport? Report { get; init; }

    /// <summary>Collected run metrics.</summary>
    public required RunMetrics Metrics { get; init; }
}
