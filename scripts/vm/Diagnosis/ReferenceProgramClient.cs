using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.AI;
using SterlingVale.AgentShared.Analysis;
using SterlingVale.AgentShared.Fakes;

namespace SterlingVale.Diagnostics;

/// <summary>Schedules one supplied program and echoes its stdout verbatim; never calls a model or oracle.</summary>
public sealed class ReferenceProgramClientProvider(string program) : IChatClientProvider
{
    public bool IsLive => false;
    public string ModelId => "hand-authored-no-llm-control";

    public IChatClient CreateChatClient(ChatClientRequest request)
    {
        string prefix = $"CONTROL_SCOPE = {JsonSerializer.Serialize(request.Request.HouseholdIds ?? [])}\n" +
            $"CONTROL_RUN_ID = {JsonSerializer.Serialize(request.RunId)}\n" +
            $"CONTROL_GENERATED_AT = {JsonSerializer.Serialize(DateTimeOffset.UtcNow.ToString("O"))}\n";
        return new ReferenceProgramClient(prefix + program);
    }
}

public sealed class ReferenceProgramClient(string program) : DelegatingChatClient(
    new DeterministicFakeChatClient(new FakeChatPlan()
        .CallTool("execute_code", new Dictionary<string, object?> { ["code"] = program })
        .Respond("unreachable"), "hand-authored-no-llm-control", reportUsage: false))
{
    public override Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages,
        ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var history = messages.ToList();
        var results = history.SelectMany(message => message.Contents).OfType<FunctionResultContent>().ToArray();
        if (results.Length == 0)
            return base.GetResponseAsync(history, options, cancellationToken);
        if (results.Length != 1)
            throw new InvalidDataException("The control must execute exactly one program.");
        string text = results[0].Result is string value ? value : JsonSerializer.Serialize(results[0].Result);
        using var document = JsonDocument.Parse(text);
        if (!document.RootElement.GetProperty("success").GetBoolean())
            throw new InvalidDataException("Control execution failed: " + text);
        string stdout = document.RootElement.GetProperty("stdout").GetString()
            ?? throw new InvalidDataException("Control stdout is unavailable.");
        return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, stdout))
        {
            ModelId = "hand-authored-no-llm-control", ResponseId = "control-stdout", FinishReason = ChatFinishReason.Stop,
        });
    }

    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages,
        ChatOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var response = await GetResponseAsync(messages, options, cancellationToken).ConfigureAwait(false);
        foreach (var update in response.ToChatResponseUpdates())
            yield return update;
    }
}
