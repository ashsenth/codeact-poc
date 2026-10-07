using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.AI;

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
        string status = "ok";
        object? result = null;
        try
        {
            result = await base.InvokeCoreAsync(arguments, cancellationToken).ConfigureAwait(false);
            return result;
        }
        catch
        {
            status = "error";
            throw;
        }
        finally
        {
            double ms = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            recorder.Add(Name, Serialize(arguments), Serialize(result), ms, status);
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
