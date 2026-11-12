using System.Diagnostics;
using Microsoft.Extensions.AI;
using SterlingVale.Telemetry;

namespace SterlingVale.AgentShared.Analysis;

public sealed class ObservedChatClient(IChatClient inner) : DelegatingChatClient(inner)
{
    private int _turn;

    public override async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages,
        ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        var execution = ExecutionSession.Current;
        if (execution is null)
        {
            return await base.GetResponseAsync(messages, options, cancellationToken).ConfigureAwait(false);
        }

        int modelTurn = Interlocked.Increment(ref _turn);
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
            var usage = new ModelUsage(modelTurn, response.Usage?.InputTokenCount, response.Usage?.OutputTokenCount, response.Usage?.TotalTokenCount);
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

    private static object ProjectMessages(IEnumerable<ChatMessage> messages) => messages.Select(message => new
    {
        role = message.Role.Value,
        text = string.Concat(message.Contents.OfType<TextContent>().Select(content => content.Text)),
        calls = message.Contents.OfType<FunctionCallContent>().Select(call => new { call.CallId, call.Name, call.Arguments }),
        results = message.Contents.OfType<FunctionResultContent>().Select(result => new { result.CallId, result.Result }),
    }).ToArray();
}