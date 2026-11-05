using System.Text.Json;

namespace SterlingVale.Telemetry;

public enum ExecutionLogLevel { Minimal, Normal, Verbose, Debug }

public static class ExecutionConsole
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static string? Format(ExecutionEvent entry, ExecutionLogLevel level)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (level == ExecutionLogLevel.Debug)
        {
            return JsonSerializer.Serialize(entry, Json);
        }

        bool runEvent = entry.EventType is "RUN_STARTED" or "RUN_COMPLETED" or "RUN_FAILED";
        if (level == ExecutionLogLevel.Minimal && !runEvent)
        {
            return null;
        }

        string prefix = $"[{entry.Timestamp:HH:mm:ss.fff}] [{entry.AgentMode.ToUpperInvariant()}] " +
            $"run={entry.RunId} correlation={entry.CorrelationId} comparison={entry.ComparisonId} trial={entry.TrialId} " +
            $"trace={entry.TraceId} span={entry.SpanId} seq={entry.SequenceNumber} ";
        string label = entry.EventType switch
        {
            "CODE_GENERATED" => "MODEL OUTPUT: GENERATED PYTHON",
            "MODEL_RESPONSE_RECEIVED" => "MODEL OUTPUT",
            "TOOL_CALL_STARTED" => "TOOL REQUEST",
            "TOOL_CALL_COMPLETED" => "TOOL RESPONSE",
            "JSON_GENERATED" => "FINAL RESPONSE",
            _ => entry.EventType.Replace('_', ' '),
        };
        string data = entry.Data is null ? string.Empty : JsonSerializer.Serialize(entry.Data, Json);
        if (level != ExecutionLogLevel.Verbose && data.Length > 0)
        {
            using var document = JsonDocument.Parse(data);
            data = document.RootElement.ValueKind == JsonValueKind.Object && document.RootElement.TryGetProperty("toolName", out var name)
                ? name.GetString() ?? string.Empty : string.Empty;
        }

        return $"{prefix}{label} {entry.Status} {entry.DurationMs:0.###}ms {data}".TrimEnd();
    }
}