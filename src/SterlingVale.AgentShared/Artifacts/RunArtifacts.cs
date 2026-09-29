using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using SterlingVale.AgentShared.Analysis;
using SterlingVale.AgentShared.Configuration;
using SterlingVale.AgentShared.Contracts;
using SterlingVale.AgentShared.Prompting;

namespace SterlingVale.AgentShared.Artifacts;

/// <summary>
/// Pure builder that turns a <see cref="RunRecord"/> into the set of per-run artifact files. Kept
/// IO-free so it can be unit tested; Infrastructure performs the actual file writes.
/// </summary>
public static class RunArtifacts
{
    /// <summary>Marker written to generated-code.txt when no generated program is available.</summary>
    public const string GeneratedCodeUnavailable =
        "UNAVAILABLE: no model-generated code was captured for this run " +
        "(Classic mode, or the SDK did not surface generated code).";

    /// <summary>Marker written to raw-output.txt when the run produced no model text (e.g. a timeout or provider error).</summary>
    public const string RawOutputUnavailable =
        "UNAVAILABLE: the run produced no model text output (e.g. timeout or provider error).";

    /// <summary>Canonical artifact file names.</summary>
    public static class Files
    {
        public const string Result = "result.json";
        public const string Metrics = "metrics.json";
        public const string Events = "events.jsonl";
        public const string Metadata = "metadata.json";
        public const string GeneratedCode = "generated-code.txt";
        public const string RawOutput = "raw-output.txt";
        public const string ToolResults = "tool-results.txt";
    }

    /// <summary>Marker written to tool-results.txt when no tool results were captured.</summary>
    public const string ToolResultsUnavailable =
        "UNAVAILABLE: no tool results were captured for this run (e.g. Classic mode or a timeout).";

    private static readonly JsonSerializerOptions Json = CreateJson();

    /// <summary>Builds the artifact file contents (filename → text) for a run.</summary>
    public static IReadOnlyDictionary<string, string> Build(
        RunRecord record, ModelConfiguration model, PricingConfiguration pricing)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(pricing);

        var response = record.Response;
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [Files.Result] = JsonSerializer.Serialize(RunRecordMapper.ToResponseDto(record), Json),
            [Files.Metrics] = JsonSerializer.Serialize(RunRecordMapper.ToMetricsDto(response.Metrics), Json),
            [Files.Events] = BuildEvents(record),
            [Files.Metadata] = JsonSerializer.Serialize(BuildMetadata(record, model, pricing), Json),
            [Files.GeneratedCode] = record.GeneratedCode ?? GeneratedCodeUnavailable,
            [Files.RawOutput] = string.IsNullOrEmpty(record.RawModelOutput) ? RawOutputUnavailable : record.RawModelOutput,
            [Files.ToolResults] = string.IsNullOrEmpty(record.ToolResults) ? ToolResultsUnavailable : record.ToolResults,
        };
    }

    private static RunMetadataDto BuildMetadata(RunRecord record, ModelConfiguration model, PricingConfiguration pricing)
    {
        var response = record.Response;
        return new RunMetadataDto(
            response.RunId,
            record.Mode,
            response.Status.ToString(),
            Prompt.PromptId,
            OutputSchema.SchemaId,
            model.DeploymentName,
            model.Temperature,
            model.MaxTurns,
            model.TimeoutSeconds,
            pricing.InputPerMillion,
            pricing.OutputPerMillion,
            response.Metrics.EstimatedCostUsd,
            record.Fingerprints.ToDictionary(),
            record.GeneratedCode is not null,
            DateTimeOffset.UtcNow.ToString("O"),
            RuntimeInformation.FrameworkDescription);
    }

    private static string BuildEvents(RunRecord record)
    {
        var response = record.Response;
        var metrics = response.Metrics;
        var builder = new StringBuilder();

        AppendEvent(builder, new { @event = "run_started", runId = response.RunId, mode = record.Mode });
        foreach (var (tool, count) in metrics.ToolCallsByName.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            AppendEvent(builder, new { @event = "tool_calls", tool, count });
        }

        AppendEvent(builder, new { @event = "model_turns", count = metrics.ModelTurnCount });
        AppendEvent(builder, new
        {
            @event = "run_completed",
            status = response.Status.ToString(),
            schemaValid = metrics.SchemaValid,
            totalTokens = metrics.TotalTokens,
            executeCodeCalls = metrics.ExecuteCodeCallCount,
        });

        return builder.ToString();
    }

    private static void AppendEvent(StringBuilder builder, object payload) =>
        builder.Append(JsonSerializer.Serialize(payload, CompactJson)).Append('\n');

    private static readonly JsonSerializerOptions CompactJson = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter() },
    };

    private static JsonSerializerOptions CreateJson()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
