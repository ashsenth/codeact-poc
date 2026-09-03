using SterlingVale.AgentShared.Analysis;
using SterlingVale.Domain.Analysis;

namespace SterlingVale.AgentShared.Contracts;

/// <summary>Maps internal run records to the shared wire DTOs used by both APIs.</summary>
public static class RunRecordMapper
{
    /// <summary>Maps a run record to the analysis response DTO.</summary>
    public static AnalysisResponseDto ToResponseDto(RunRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        var response = record.Response;
        return new AnalysisResponseDto(
            response.RunId,
            response.Mode,
            response.Status.ToString(),
            response.Report is null ? null : ReportJson.ToDto(response.Report),
            ToMetricsDto(response.Metrics),
            record.Fingerprints.ToDictionary());
    }

    /// <summary>Maps a run record to the artifacts DTO.</summary>
    public static ArtifactsDto ToArtifactsDto(RunRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return new ArtifactsDto(
            record.Response.RunId,
            record.Mode,
            record.RawModelOutput,
            record.Fingerprints.ToDictionary());
    }

    /// <summary>Maps run metrics to their wire DTO.</summary>
    public static RunMetricsDto ToMetricsDto(RunMetrics metrics)
    {
        ArgumentNullException.ThrowIfNull(metrics);
        return new RunMetricsDto(
            metrics.Duration.TotalMilliseconds,
            metrics.ModelRequestCount,
            metrics.ModelTurnCount,
            metrics.ToolCallCount,
            metrics.ToolCallsByName,
            metrics.ExecuteCodeCallCount,
            metrics.InputTokens,
            metrics.OutputTokens,
            metrics.TotalTokens,
            metrics.EstimatedCostUsd,
            metrics.RetryCount,
            metrics.SchemaValid);
    }
}
