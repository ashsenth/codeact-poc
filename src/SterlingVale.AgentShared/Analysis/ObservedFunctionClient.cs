using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.AI;
using SterlingVale.Telemetry;

namespace SterlingVale.AgentShared.Analysis;

public sealed class ObservedFunctionClient(IChatClient inner) : FunctionInvokingChatClient(inner)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private int _executionNumber;

    protected override async ValueTask<object?> InvokeFunctionAsync(FunctionInvocationContext context, CancellationToken cancellationToken)
    {
        var session = ExecutionSession.Current;
        if (session is null)
        {
            return await base.InvokeFunctionAsync(context, cancellationToken).ConfigureAwait(false);
        }

        string invocationId = context.CallContent.CallId;
        using var invocationScope = ExecutionSession.BeginInvocation(invocationId);
        string name = context.Function.Name;
        bool code = name == "execute_code";
        string executionEventPrefix = session.SandboxRuntime == "hyperlight" ? "CODE_EXECUTION" : "OFFLINE_CODE_TOOL";
        int number = code ? Interlocked.Increment(ref _executionNumber) : 0;
        string? reference = code ? $"codeact/generated-code-{number:D3}.py" : null;
        var startTime = DateTimeOffset.UtcNow;
        var request = new { invocationId, toolName = name, modelTurn = context.Iteration + 1,
            arguments = session.Payloads.Capture(context.Arguments), generatedCodeReference = reference, startTime };
        session.Emit("MODEL_TOOL_CALL_STARTED", data: request);
        if (code)
        {
            context.Arguments.TryGetValue("code", out var program);
            var captured = session.Payloads.Capture(program?.ToString() ?? string.Empty);
            if (captured.Content is not null && !captured.Truncated && !captured.Redacted)
            {
                session.WriteArtifact(reference!, captured.Content);
            }
            else
            {
                session.WriteArtifact($"codeact/generated-code-{number:D3}.capture.json", JsonSerializer.Serialize(captured, Json));
            }

            session.Emit("CODE_GENERATED", captured.Content is null || captured.Truncated || captured.Redacted ? "exact-capture-unavailable" : "ok",
                data: new { invocationId, modelTurn = context.Iteration + 1, reference, program = captured });
            session.Emit("EXECUTE_CODE_CALL_STARTED", data: request);
            if (session.SandboxRuntime != "generation-only")
                session.Emit(executionEventPrefix + "_STARTED", data: request);
        }

        using var activity = new Activity(code ? "code.execute" : "model.tool").SetIdFormat(ActivityIdFormat.W3C).Start();
        long start = Stopwatch.GetTimestamp();
        try
        {
            bool notExecuted = code && session.SandboxRuntime == "generation-only";
            object? result;
            if (notExecuted)
            {
                context.Terminate = context.FunctionCallIndex + 1 >= context.FunctionCount;
                result = new { success = false, status = "not executed: no Hyperlight guest" };
                session.Emit("CODE_EXECUTION_NOT_EXECUTED", "not executed: no Hyperlight guest",
                    data: new { invocationId, reference, toolName = name });
            }
            else
            {
                result = await base.InvokeFunctionAsync(context, cancellationToken).ConfigureAwait(false);
            }
            var payload = session.Payloads.Capture(result);
            bool? success = null;
            int? exitCode = null;
            bool stdoutAvailable = false;
            bool stderrAvailable = false;
            if (code)
            {
                var resultJson = result is string text ? text : JsonSerializer.Serialize(result, Json);
                try
                {
                    using var document = JsonDocument.Parse(resultJson);
                    if (document.RootElement.ValueKind == JsonValueKind.Object)
                    {
                        if (document.RootElement.TryGetProperty("success", out var succeeded) && succeeded.ValueKind is JsonValueKind.True or JsonValueKind.False)
                        {
                            success = succeeded.GetBoolean();
                        }

                        if ((document.RootElement.TryGetProperty("exit_code", out var exit) || document.RootElement.TryGetProperty("exitCode", out exit)) &&
                            exit.ValueKind == JsonValueKind.Number && exit.TryGetInt32(out int parsedExit))
                        {
                            exitCode = parsedExit;
                        }

                        stdoutAvailable = document.RootElement.TryGetProperty("stdout", out _);
                        stderrAvailable = document.RootElement.TryGetProperty("stderr", out _);

                        CaptureStream(session, document.RootElement, "stdout", invocationId, executionEventPrefix);
                        CaptureStream(session, document.RootElement, "stderr", invocationId, executionEventPrefix);
                    }
                }
                catch (JsonException)
                {
                }
            }

            var completed = new { invocationId, toolName = name, modelTurn = context.Iteration + 1,
                reference, startTime, endTime = DateTimeOffset.UtcNow, durationMs = Stopwatch.GetElapsedTime(start).TotalMilliseconds,
                status = notExecuted ? "not executed: no Hyperlight guest" : success == false ? "failed" : "returned", success, exitCode, stdoutAvailable, stderrAvailable, result = payload,
                runtime = session.SandboxRuntime,
                internalInitialization = "unavailable", internalCleanup = "unavailable" };
            session.Emit("MODEL_TOOL_CALL_COMPLETED", data: completed);
            if (code)
            {
                session.WriteArtifact($"codeact/execution-{number:D3}.json", JsonSerializer.Serialize(completed));
                session.Emit("EXECUTE_CODE_CALL_COMPLETED", data: completed);
                if (!notExecuted)
                {
                    session.Emit(executionEventPrefix + (success == false ? "_FAILED" : "_COMPLETED"),
                        success == false ? "failed" : "returned", completed.durationMs, data: completed);
                }
            }

            return result;
        }
        catch (Exception exception)
        {
            var failure = FailureInfo.FromException(exception);
            var failed = new { invocationId, toolName = name, reference, startTime, endTime = DateTimeOffset.UtcNow,
                status = "error", failure,
                timedOutOrCancelled = exception is OperationCanceledException };
            session.Emit("MODEL_TOOL_CALL_FAILED", "error", Stopwatch.GetElapsedTime(start).TotalMilliseconds, failure.Category, failed);
            if (code)
            {
                session.WriteArtifact($"codeact/execution-{number:D3}.json", JsonSerializer.Serialize(failed));
                session.Emit("EXECUTE_CODE_CALL_FAILED", "error", errorCategory: exception.GetType().Name, data: failed);
                session.Emit(executionEventPrefix + "_FAILED", "error", Stopwatch.GetElapsedTime(start).TotalMilliseconds, exception.GetType().Name, failed);
            }

            throw;
        }
    }

    private static void CaptureStream(ExecutionSession session, JsonElement result, string stream, string invocationId, string executionEventPrefix)
    {
        if (result.TryGetProperty(stream, out var value))
        {
            var payload = session.Payloads.Capture(value.ValueKind == JsonValueKind.String ? value.GetString() : value.GetRawText());
            session.Emit(executionEventPrefix + "_" + stream.ToUpperInvariant(), data: new { invocationId, payload });
            session.AppendArtifact($"codeact/{stream}.log", new { invocationId, payload });
        }
    }
}