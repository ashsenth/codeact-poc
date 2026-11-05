using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.AI;
using SterlingVale.Telemetry;

namespace SterlingVale.Tools.Observability;

/// <summary>
/// Transparent decorator that records each invocation (name, argument JSON, result JSON, duration,
/// status) to the ambient <see cref="ToolCallRecorder"/>. It delegates Name/Description/JsonSchema to
/// the inner function, so the tool fingerprint is unchanged and fairness stays MATCHED. When no
/// recorder is active it is a straight pass-through.
/// </summary>
public sealed class RecordingAIFunction(AIFunction inner) : DelegatingAIFunction(inner)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    protected override async ValueTask<object?> InvokeCoreAsync(
        AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        var recorder = ToolCallRecorder.Current;
        if (recorder is null)
        {
            return await base.InvokeCoreAsync(arguments, cancellationToken).ConfigureAwait(false);
        }

        long start = Stopwatch.GetTimestamp();
        var execution = ExecutionSession.Current;
        string invocationId = Guid.NewGuid().ToString("N");
        var startTime = DateTimeOffset.UtcNow;
        var capturedArguments = execution?.Payloads.Capture(Serialize(arguments));
        execution?.Emit("TOOL_CALL_STARTED", data: new { invocationId, parentInvocationId = ExecutionSession.ParentInvocationId,
            toolName = Name, arguments = capturedArguments, startTime });
        string status = "ok";
        string? exceptionCategory = null;
        object? result = null;
        try
        {
            result = await base.InvokeCoreAsync(arguments, cancellationToken).ConfigureAwait(false);
            return result;
        }
        catch (Exception exception)
        {
            status = "error";
            exceptionCategory = exception.GetType().Name;
            throw;
        }
        finally
        {
            double ms = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            var capturedResponse = execution?.Payloads.Capture(Serialize(result));
            var data = new { invocationId, parentInvocationId = ExecutionSession.ParentInvocationId, toolName = Name,
                arguments = capturedArguments, response = capturedResponse, startTime, endTime = DateTimeOffset.UtcNow,
                durationMs = ms, status, exceptionCategory, correlationId = execution?.Context.CorrelationId };
            execution?.Emit(status == "ok" ? "TOOL_CALL_COMPLETED" : "TOOL_CALL_FAILED", status, ms, exceptionCategory, data);
            execution?.AppendArtifact("tools/invocations.jsonl", data);
            recorder.Add(Name, execution is null ? Serialize(arguments) : capturedArguments?.Content,
                execution is null ? Serialize(result) : capturedResponse?.Content, ms, status);
        }
    }

    private static string? Serialize(object? value)
    {
        if (value is null)
        {
            return null;
        }

        try
        {
            return value is AIFunctionArguments args
                ? JsonSerializer.Serialize(args.ToDictionary(kv => kv.Key, kv => kv.Value), Json)
                : JsonSerializer.Serialize(value, Json);
        }
        catch (NotSupportedException)
        {
            return value.ToString();
        }
    }
}
