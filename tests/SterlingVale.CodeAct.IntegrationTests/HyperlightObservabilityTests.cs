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

namespace SterlingVale.CodeAct.IntegrationTests;

[Trait("Category", "Hyperlight")]
public sealed class HyperlightObservabilityTests : IDisposable
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
}