using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using SterlingVale.AgentShared.Analysis;
using SterlingVale.AgentShared.Fakes;
using SterlingVale.Telemetry;
using Xunit;

namespace SterlingVale.AgentShared.Tests;

public sealed class ObservedClientTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "sv-model-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Programs_are_separate_and_retained_when_a_later_model_request_fails()
    {
        using var session = new ExecutionSession(_root, "codeact", "small", payloads: new PayloadPolicy(PayloadCaptureMode.Full));
        var plan = new FakeChatPlan().CallTool("execute_code", new Dictionary<string, object?> { ["code"] = "print('first')" })
            .CallTool("execute_code", new Dictionary<string, object?> { ["code"] = "print('second')" });
        using var client = new ObservedFunctionClient(new ObservedChatClient(new FailAfterTwoResponses(new DeterministicFakeChatClient(plan))));
        var agent = client.AsAIAgent(new ChatClientAgentOptions
        {
            UseProvidedChatClientAsIs = true,
            ChatOptions = new ChatOptions { Tools = [FakeExecuteCodeFunction.Create()] },
        });
        await Assert.ThrowsAnyAsync<Exception>(() => agent.RunAsync("test"));
        Assert.Equal("print('first')", File.ReadAllText(Path.Combine(session.RunDirectory, "codeact/generated-code-001.py")));
        Assert.Equal("print('second')", File.ReadAllText(Path.Combine(session.RunDirectory, "codeact/generated-code-002.py")));
        Assert.Equal(2, session.Snapshot().Count(entry => entry.EventType == "OFFLINE_CODE_TOOL_STARTED"));
        Assert.Equal(2, session.Snapshot().Count(entry => entry.EventType == "OFFLINE_CODE_TOOL_STDOUT"));
        Assert.Contains(session.Snapshot(), entry => entry.EventType == "MODEL_REQUEST_FAILED");
        using var execution = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(session.RunDirectory, "codeact/execution-001.json")));
        Assert.Equal(0, execution.RootElement.GetProperty("exitCode").GetInt32());
        Assert.True(execution.RootElement.GetProperty("stdoutAvailable").GetBoolean());
        Assert.True(execution.RootElement.GetProperty("stderrAvailable").GetBoolean());
    }

    [Fact]
    public async Task Classic_turn_and_usage_are_captured_at_the_model_boundary()
    {
        using var session = new ExecutionSession(_root, "classic", "small", payloads: new PayloadPolicy(PayloadCaptureMode.Full));
        using var client = new ObservedChatClient(new DeterministicFakeChatClient(new FakeChatPlan().Respond("{\"households\":[]}")));
        await client.GetResponseAsync([new ChatMessage(ChatRole.User, "analyze")]);
        Assert.Single(session.UsageSnapshot());
        Assert.True(File.Exists(Path.Combine(session.RunDirectory, "classic/model-turn-001.json")));
        Assert.Equal(["MODEL_REQUEST_STARTED", "MODEL_RESPONSE_RECEIVED", "MODEL_USAGE_RECORDED"], session.Snapshot().Select(entry => entry.EventType));
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private sealed class FailAfterTwoResponses(IChatClient inner) : DelegatingChatClient(inner)
    {
        private int _requests;

        public override Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages,
            ChatOptions? options = null, CancellationToken cancellationToken = default) =>
            Interlocked.Increment(ref _requests) > 2
                ? throw new InvalidOperationException("Deterministic provider failure")
                : base.GetResponseAsync(messages, options, cancellationToken);
    }
}