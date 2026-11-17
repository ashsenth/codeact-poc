using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using SterlingVale.AgentShared.Analysis;
using SterlingVale.AgentShared.Configuration;
using SterlingVale.AgentShared.Fakes;
using SterlingVale.CodeAct;
using SterlingVale.Telemetry;
using SterlingVale.Tools;
using SterlingVale.Tools.Observability;
using Xunit;
using Xunit.Abstractions;

namespace SterlingVale.CodeAct.IntegrationTests;

[Trait("Category", "Hyperlight")]
public sealed class HyperlightObservabilityTests(ITestOutputHelper output) : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "sv-real-guest-" + Guid.NewGuid().ToString("N"));

    [SkippableFact]
    public async Task Real_python_stdout_and_host_callbacks_are_captured()
    {
        string guest = RequireGuest();
        using var session = new ExecutionSession(_root, "codeact", "small", payloads: new PayloadPolicy(PayloadCaptureMode.Full));
        using var recorder = ToolCallRecorder.Activate(new ToolCallRecorder());
        const string program = "print('real-guest-probe')\nprint(call_tool('list_households'))";
        using var client = Client(program);
        using var bundle = Bundle(client, guest);
        await bundle.Agent.RunAsync("Execute the supplied program");
        Assert.Equal(program, File.ReadAllText(Path.Combine(session.RunDirectory, "codeact/generated-code-001.py")));
        Assert.Contains(session.Snapshot(), entry => entry.EventType == "CODE_EXECUTION_COMPLETED");
        Assert.Contains(session.Snapshot(), entry => entry.EventType == "TOOL_CALL_COMPLETED");
        Assert.Contains("real-guest-probe", File.ReadAllText(Path.Combine(session.RunDirectory, "codeact/stdout.log")));
    }

    [SkippableFact]
    public async Task Real_python_exception_preserves_stderr_and_failed_execution()
    {
        string guest = RequireGuest();
        using var session = new ExecutionSession(_root, "codeact", "small", payloads: new PayloadPolicy(PayloadCaptureMode.Full));
        using var client = Client("raise RuntimeError('real-guest-failure')");
        using var bundle = Bundle(client, guest);
        await bundle.Agent.RunAsync("Execute the supplied program");
        Assert.Contains(session.Snapshot(), entry => entry.EventType == "CODE_EXECUTION_FAILED");
        Assert.Contains("real-guest-failure", File.ReadAllText(Path.Combine(session.RunDirectory, "codeact/stderr.log")));
        Assert.True(File.Exists(Path.Combine(session.RunDirectory, "codeact/execution-001.json")));
    }

    [SkippableFact]
    public async Task Oversized_stdout_returns_explicit_error_to_client_without_truncating_or_killing_host()
    {
        string guest = RequireGuest();
        using var session = new ExecutionSession(_root, "codeact", "small", payloads: new PayloadPolicy(PayloadCaptureMode.Full));
        using var inner = new OverflowObserver();
        using var client = new ObservedFunctionClient(new ObservedChatClient(inner, maxTurns: 3));
        using var bundle = Bundle(client, guest);
        var response = await bundle.Agent.RunAsync("Run the offline overflow probe");
        Assert.Equal("overflow-observed", response.Text);
        string result = Assert.IsType<string>(inner.ToolResult);
        using var document = System.Text.Json.JsonDocument.Parse(result);
        var value = document.RootElement;
        Assert.False(value.GetProperty("success").GetBoolean());
        Assert.Equal(-1, value.GetProperty("exit_code").GetInt32());
        Assert.Equal("", value.GetProperty("stdout").GetString());
        Assert.Contains("Not enough space in shared output buffer", value.GetProperty("stderr").GetString());
        Assert.Contains("Available: 16376", value.GetProperty("stderr").GetString());
        Assert.Single(session.Snapshot(), e => e.EventType == "CODE_EXECUTION_FAILED");
        Assert.Single(session.Snapshot(), e => e.EventType == "SANDBOX_OUTPUT_OVERFLOW");
        Assert.Equal(1, TelemetryReconciler.FromEvidence(session, TimeSpan.FromSeconds(1), false, null).SandboxOutputOverflowErrors);
        Assert.False(RunValidity.IsModelOutcome(Assert.IsType<FailureInfo>(RunValidity.FindFailure("Completed", session.Snapshot()))));
        Assert.DoesNotContain(session.Snapshot(), e => e.EventType == "MODEL_TOOL_CALL_FAILED");
        output.WriteLine("Returned to scripted client, which continued to final response: " + result);
    }

    [SkippableFact]
    [Trait("Category", "HyperlightKnownIssue")]
    public async Task Real_running_program_observes_cancellation_and_retains_source()
    {
        string guest = RequireGuest();
        using var session = new ExecutionSession(_root, "codeact", "small", payloads: new PayloadPolicy(PayloadCaptureMode.Full));
        using var client = Client("while True:\n    pass");
        using var bundle = Bundle(client, guest);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => bundle.Agent.RunAsync("Execute the supplied program", cancellationToken: cancellation.Token));
        Assert.True(File.Exists(Path.Combine(session.RunDirectory, "codeact/generated-code-001.py")));
        Assert.Contains(session.Snapshot(), entry => entry.EventType == "CODE_EXECUTION_FAILED");
    }

    private static string RequireGuest()
    {
        string? guest = Environment.GetEnvironmentVariable("HYPERLIGHT_PYTHON_GUEST_PATH");
        Skip.If(string.IsNullOrWhiteSpace(guest), "Real Hyperlight guest not configured; no simulated sandbox execution is substituted.");
        Assert.True(File.Exists(guest));
        return guest!;
    }

    private static ObservedFunctionClient Client(string program) => new(new ObservedChatClient(new DeterministicFakeChatClient(
        new FakeChatPlan().CallTool("execute_code", new Dictionary<string, object?> { ["code"] = program }).Respond("{}"))));

    private static AgentBundle Bundle(IChatClient client, string guest) => new CodeActAgentFactory(new HyperlightOptions { GuestPath = guest })
        .Create(client, new PortfolioToolService(SnapshotHelper.Build()), new ModelConfiguration { DeploymentName = "scripted-model", Temperature = 0, MaxTurns = 4, TimeoutSeconds = 15 });

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private sealed class OverflowObserver() : DelegatingChatClient(new DeterministicFakeChatClient(new FakeChatPlan()
        .CallTool("execute_code", new Dictionary<string, object?> { ["code"] = "print('x' * 32768)" })
        .Respond("overflow-observed")))
    {
        public object? ToolResult { get; private set; }

        public override Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages,
            ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            var history = messages.ToArray();
            ToolResult = history.SelectMany(m => m.Contents).OfType<FunctionResultContent>().LastOrDefault()?.Result;
            return base.GetResponseAsync(history, options, cancellationToken);
        }
    }
}