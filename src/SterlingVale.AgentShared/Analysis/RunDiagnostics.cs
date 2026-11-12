using System.Text.Json;
using SterlingVale.Domain.Analysis;
using SterlingVale.Telemetry;

namespace SterlingVale.AgentShared.Analysis;

public sealed record RunDiagnostics(
    string? FinishReason,
    long? FinalOutputTokens,
    string? FinalPreview,
    IReadOnlyDictionary<string, int> ToolCallsByName,
    long GeneratedCodeBytes,
    string? IncompleteReason)
{
    public static RunDiagnostics FromEvents(string mode, RunStatus status, IReadOnlyList<ExecutionEvent> events)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["list_households"] = 0, ["get_policy"] = 0, ["list_accounts"] = 0, ["list_positions"] = 0,
            ["get_price"] = 0, ["get_fx"] = 0, ["asset_class"] = 0, ["execute_code"] = 0,
        };
        string? finishReason = null;
        long? outputTokens = null;
        string? preview = null;
        long codeBytes = 0;
        foreach (var entry in events)
        {
            if (entry.Data is null) continue;
            var data = JsonSerializer.SerializeToElement(entry.Data);
            if (entry.EventType == "MODEL_RESPONSE_RECEIVED")
            {
                finishReason = data.TryGetProperty("finishReason", out var finish) ? finish.GetString() : null;
                preview = data.TryGetProperty("preview", out var text) ? text.GetString() : null;
                outputTokens = data.TryGetProperty("outputTokens", out var tokens) && tokens.ValueKind == JsonValueKind.Number &&
                    tokens.TryGetInt64(out long value) ? value : null;
            }
            if (entry.EventType == "MODEL_TOOL_CALL_STARTED" && data.TryGetProperty("toolName", out var tool))
            {
                string name = tool.GetString()!;
                counts[name] = counts.GetValueOrDefault(name) + 1;
            }
            if (entry.EventType == "TOOL_CALL_STARTED" && mode == "codeact" && data.TryGetProperty("toolName", out var callback))
            {
                string name = callback.GetString()!;
                counts[name] = counts.GetValueOrDefault(name) + 1;
            }
            if (entry.EventType == "CODE_GENERATED" && data.TryGetProperty("byteCount", out var bytes) &&
                bytes.ValueKind == JsonValueKind.Number && bytes.TryGetInt64(out long length))
                codeBytes += length;
        }
        string? incomplete = status == RunStatus.Failed && mode == "classic" && finishReason is not null
            ? counts["get_price"] == 0 ? "INCOMPLETE: model stopped before pricing" : "INCOMPLETE: no valid non-empty analysis report"
            : null;
        return new(finishReason, outputTokens, preview, counts, codeBytes, incomplete);
    }

    public string Summary => $"{IncompleteReason ?? "Model completion"}; finish_reason={FinishReason ?? "unavailable"}; " +
        $"tool calls by name: {string.Join(", ", ToolCallsByName.Select(tool => $"{tool.Key}={tool.Value}"))}";
}
