using System.Diagnostics;
using Microsoft.Extensions.AI;
using SterlingVale.Telemetry;

namespace SterlingVale.AgentShared.Analysis;

public sealed class ObservedChatClient(IChatClient inner, int maxTurns = int.MaxValue) : DelegatingChatClient(inner)
{
    private readonly int _maxTurns = maxTurns > 0 ? maxTurns : throw new ArgumentOutOfRangeException(nameof(maxTurns));
    private int _turn;

    public override async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages,
        ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var execution = ExecutionSession.Current;
        int modelTurn = ReserveRequest(execution);
        if (execution is null)
        {
            return await base.GetResponseAsync(messages, options, cancellationToken).ConfigureAwait(false);
        }

        string requestId = Guid.NewGuid().ToString("N");
        using var activity = new Activity("model.request").SetIdFormat(ActivityIdFormat.W3C).Start();
        var input = messages.ToList();
        var request = new { requestId, modelTurn, execution.Context, messages = execution.Payloads.Capture(ProjectMessages(input)) };
        execution.AppendArtifact("model/requests.jsonl", request);
        execution.Emit("MODEL_REQUEST_STARTED", data: request);
        long start = Stopwatch.GetTimestamp();
        try
        {
            var response = await base.GetResponseAsync(input, options, cancellationToken).ConfigureAwait(false);
            var usage = new ModelUsage(modelTurn, response.Usage?.InputTokenCount, response.Usage?.OutputTokenCount, response.Usage?.TotalTokenCount)
            {
                CachedInputTokens = response.Usage?.CachedInputTokenCount,
                ReasoningTokens = response.Usage?.ReasoningTokenCount,
                CacheWriteTokens = response.Usage?.AdditionalCounts?.GetValueOrDefault("CacheWriteTokenCount") ?? 0,
            };
            execution.RecordUsage(usage);
            string safeText = PayloadPolicy.Redact(response.Text ?? string.Empty);
            string preview = safeText.Length > 240 ? safeText[..240] : safeText;
            var captured = new { requestId, modelTurn, execution.Context, usage,
                finishReason = response.FinishReason?.ToString(), outputTokens = response.Usage?.OutputTokenCount, preview,
                messages = execution.Payloads.Capture(ProjectMessages(response.Messages)) };
            execution.AppendArtifact("model/responses.jsonl", captured);
            execution.Emit("MODEL_RESPONSE_RECEIVED", durationMs: Stopwatch.GetElapsedTime(start).TotalMilliseconds, data: captured);
            execution.Emit("MODEL_USAGE_RECORDED", usage.TotalTokens is null ? "unavailable" : "ok", data: usage);
            if (execution.Context.AgentMode == "classic")
            {
                execution.WriteArtifact($"classic/model-turn-{modelTurn:D3}.json", System.Text.Json.JsonSerializer.Serialize(captured));
            }

            return response;
        }
        catch (Exception exception)
        {
            var failure = FailureInfo.FromException(exception);
            execution.Emit("MODEL_REQUEST_FAILED", "error", Stopwatch.GetElapsedTime(start).TotalMilliseconds, failure.Category,
                new { requestId, modelTurn, failure });
            throw;
        }
    }

    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ReserveRequest(ExecutionSession.Current);
        await foreach (var update in base.GetStreamingResponseAsync(messages, options, cancellationToken).ConfigureAwait(false))
            yield return update;
    }

    private int ReserveRequest(ExecutionSession? execution)
    {
        int modelTurn = Interlocked.Increment(ref _turn);
        if (modelTurn > _maxTurns)
        {
            var failure = new FailureInfo("TurnLimitReached", $"The model request limit of {_maxTurns} was reached.");
            execution?.Emit("TURN_LIMIT_REACHED", "Capped", errorCategory: failure.Category,
                data: new { maxTurns = _maxTurns, blockedRequest = modelTurn, failure });
            throw new TurnLimitReachedException(_maxTurns);
        }
        return modelTurn;
    }

    private static object ProjectMessages(IEnumerable<ChatMessage> messages) => messages.Select(message => new
    {
        role = message.Role.Value,
        text = string.Concat(message.Contents.OfType<TextContent>().Select(content => content.Text)),
        calls = message.Contents.OfType<FunctionCallContent>().Select(call => new { call.CallId, call.Name, call.Arguments }),
        results = message.Contents.OfType<FunctionResultContent>().Select(result => new { result.CallId, result.Result }),
    }).ToArray();
}