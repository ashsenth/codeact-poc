using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using SterlingVale.AgentShared.Analysis;
using SterlingVale.AgentShared.Fakes;
using SterlingVale.AgentShared.Configuration;
using SterlingVale.Domain.Analysis;
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
    public async Task Failure_events_keep_safe_category_status_and_message_without_raw_exception_text()
    {
        using var session = new ExecutionSession(_root, "classic", "small");
        var failure = new FailureInfo("ClientResultException", "Model request failed: HTTP 401, error code Unauthorized.", 401, "Unauthorized");
        using var client = new ObservedChatClient(new ThrowingClient(new ModelRequestException(failure,
            new InvalidOperationException("secret-token https://private.invalid local-path"))));
        await Assert.ThrowsAsync<ModelRequestException>(() => client.GetResponseAsync([new ChatMessage(ChatRole.User, "analyze")]));
        var entry = Assert.Single(session.Snapshot(), item => item.EventType == "MODEL_REQUEST_FAILED");
        Assert.Equal("ClientResultException", entry.ErrorCategory);
        string json = System.Text.Json.JsonSerializer.Serialize(entry);
        Assert.Contains("401", json);
        Assert.Contains("Unauthorized", json);
        Assert.DoesNotContain("secret-token", json);
        Assert.DoesNotContain("private.invalid", json);
    }

    [Fact]
    public async Task Live_generation_only_saves_all_programs_without_invoking_a_tool_or_retrying_model()
    {
        using var session = new ExecutionSession(_root, "codeact", "small", payloads: new PayloadPolicy(PayloadCaptureMode.Full));
        session.ModelIsLive = true;
        session.SandboxRuntime = "generation-only";
        int invocations = 0;
        var tool = AIFunctionFactory.Create((string code) => { invocations++; return code; }, "execute_code");
        using var model = new GeneratedProgramsClient();
        using var client = new ObservedFunctionClient(new ObservedChatClient(model));
        var agent = client.AsAIAgent(new ChatClientAgentOptions
        {
            UseProvidedChatClientAsIs = true,
            ChatOptions = new ChatOptions { Tools = [tool] },
        });
        var modelConfiguration = new ModelConfiguration { DeploymentName = "live-model", Temperature = 0f, MaxTurns = 12, TimeoutSeconds = 30 };
        var pricing = new PricingConfiguration { InputPerMillion = 0m, OutputPerMillion = 0m };
        var record = await new AnalysisRunner().RunAsync(agent, new RunContext
        {
            RunId = session.Context.RunId, Mode = "codeact", UserMessage = "generate code",
            Model = modelConfiguration, Pricing = pricing,
            Fingerprints = FairnessFingerprints.Create("tools", "data", modelConfiguration, pricing),
        }, CancellationToken.None);
        Assert.Equal(RunStatus.NotExecuted, record.Response.Status);
        Assert.False(record.Response.Metrics.SchemaValid);
        Assert.True(record.Response.Metrics.TelemetryValid, string.Join("; ", record.Response.Metrics.TelemetryErrors));
        Assert.Equal(0, invocations);
        Assert.Equal(1, model.Requests);
        Assert.Equal("print('first')", File.ReadAllText(Path.Combine(session.RunDirectory, "codeact", "generated-code-001.py")));
        Assert.Equal("print('second')", File.ReadAllText(Path.Combine(session.RunDirectory, "codeact", "generated-code-002.py")));
        Assert.Equal(2, session.Snapshot().Count(entry => entry.EventType == "CODE_EXECUTION_NOT_EXECUTED" &&
            entry.Status == "not executed: no Hyperlight guest"));
        Assert.DoesNotContain(session.Snapshot(), entry => entry.EventType is "CODE_EXECUTION_COMPLETED" or "OFFLINE_CODE_TOOL_COMPLETED");
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

    private sealed class ThrowingClient(Exception exception) : DelegatingChatClient(new DeterministicFakeChatClient(new FakeChatPlan().Respond("unused")))
    {
        public override Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages,
            ChatOptions? options = null, CancellationToken cancellationToken = default) => throw exception;
    }

    private sealed class GeneratedProgramsClient() : DelegatingChatClient(new DeterministicFakeChatClient(new FakeChatPlan().Respond("unused")))
    {
        public int Requests { get; private set; }
        public override Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages,
            ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            Requests++;
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant,
            [
                new FunctionCallContent("first", "execute_code", new Dictionary<string, object?> { ["code"] = "print('first')" }),
                new FunctionCallContent("second", "execute_code", new Dictionary<string, object?> { ["code"] = "print('second')" }),
            ])));
        }
    }

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