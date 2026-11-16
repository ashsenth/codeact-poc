using System.Text.Json;
using SterlingVale.Telemetry;

namespace SterlingVale.AgentShared.Analysis;

public static class RunValidity
{
    public static bool IsModelOutcome(FailureInfo failure) => failure.HttpStatus is null &&
        failure.Category is "InvalidModelOutput" or "IncompleteEvidence" or "TurnLimit" or "TimedOut";

    public static int ToolArgumentErrors(IReadOnlyList<ExecutionEvent> events) =>
        events.Count(entry => IsArgumentError(entry) &&
            (entry.EventType == "TOOL_CALL_FAILED" || !events.Any(child =>
                child.EventType == "TOOL_CALL_FAILED" &&
                Field(child, "parentInvocationId") is { } parent && parent == Field(entry, "invocationId"))));

    public static FailureInfo? FindFailure(string status, IReadOnlyList<ExecutionEvent> events)
    {
        var failures = events.Where(entry => entry.ErrorCategory is not null &&
                !IsRecoveredArgumentError(entry, events) &&
                !(entry.EventType == "RUN_FAILED" && events.Any(prior =>
                    prior.ErrorCategory == entry.ErrorCategory && IsRecoveredArgumentError(prior, events))) &&
                !(status == "TimedOut" && entry.ErrorCategory is "Cancelled" or "OperationCanceledException"))
            .Select(Failure).ToArray();
        return failures.FirstOrDefault(failure => !IsModelOutcome(failure)) ?? failures.FirstOrDefault() ??
            (status == "Failed" && events.Any(entry => entry.EventType == "JSON_VALIDATION_COMPLETED" && entry.Status == "failed")
                ? new("InvalidModelOutput", "The model did not return a valid non-empty analysis report.") : null);
    }

    private static bool IsArgumentError(ExecutionEvent entry) =>
        entry.EventType is "TOOL_CALL_FAILED" or "MODEL_TOOL_CALL_FAILED" &&
        entry.ErrorCategory is "ToolArgumentException" or "ToolNotFoundException";

    private static bool IsRecoveredArgumentError(ExecutionEvent entry, IReadOnlyList<ExecutionEvent> events) =>
        IsArgumentError(entry) &&
        events.Any(next => next.SequenceNumber > entry.SequenceNumber && next.EventType == "MODEL_REQUEST_STARTED");

    private static FailureInfo Failure(ExecutionEvent entry)
    {
        var data = JsonSerializer.SerializeToElement(entry.Data, JsonSerializerOptions.Web);
        if (data.ValueKind == JsonValueKind.Object && data.TryGetProperty("failure", out var failure))
            return failure.Deserialize<FailureInfo>(JsonSerializerOptions.Web)
                ?? throw new InvalidDataException("Null failure evidence.");
        return new(entry.ErrorCategory!, "See the run timeline for failure evidence.");
    }

    private static string? Field(ExecutionEvent entry, string name)
    {
        var data = JsonSerializer.SerializeToElement(entry.Data, JsonSerializerOptions.Web);
        return data.ValueKind == JsonValueKind.Object && data.TryGetProperty(name, out var field)
            ? field.GetString() : null;
    }
}
