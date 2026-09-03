namespace SterlingVale.AgentShared.Contracts;

/// <summary>Wire DTO for run metrics.</summary>
public sealed record RunMetricsDto(
    double DurationMs,
    int ModelRequestCount,
    int ModelTurnCount,
    int ToolCallCount,
    IReadOnlyDictionary<string, int> ToolCallsByName,
    int ExecuteCodeCallCount,
    long? InputTokens,
    long? OutputTokens,
    long? TotalTokens,
    decimal? EstimatedCostUsd,
    int RetryCount,
    bool SchemaValid);

/// <summary>Wire DTO for the analysis response returned by both APIs.</summary>
public sealed record AnalysisResponseDto(
    string RunId,
    string Mode,
    string Status,
    ExposureReportDto? Report,
    RunMetricsDto Metrics,
    IReadOnlyDictionary<string, string> Fingerprints);

/// <summary>Wire DTO for run artifacts (raw output + fingerprints).</summary>
public sealed record ArtifactsDto(
    string RunId,
    string Mode,
    string RawModelOutput,
    IReadOnlyDictionary<string, string> Fingerprints);
