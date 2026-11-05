using Microsoft.Extensions.AI;
using SterlingVale.Telemetry;
using SterlingVale.Tools.Observability;
using Xunit;

namespace SterlingVale.Tools.Tests;

public sealed class ToolEventTests
{
    [Fact]
    public async Task Failed_tool_has_real_start_end_parent_linkage_and_redacted_arguments()
    {
        string root = Path.Combine(Path.GetTempPath(), "sv-tool-events-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var session = new ExecutionSession(root, "codeact", "small", payloads: new PayloadPolicy(PayloadCaptureMode.Full));
            using var recorder = ToolCallRecorder.Activate(new ToolCallRecorder());
            using var parent = ExecutionSession.BeginInvocation("execute-id");
            var tool = new RecordingAIFunction(AIFunctionFactory.Create((string apiKey) =>
                Task.FromException<string>(new InvalidOperationException("failed")), "failing_tool"));
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await tool.InvokeAsync(new AIFunctionArguments { ["apiKey"] = "private-key" }));
            var events = session.Snapshot();
            Assert.Equal(["TOOL_CALL_STARTED", "TOOL_CALL_FAILED"], events.Select(entry => entry.EventType));
            Assert.Equal("InvalidOperationException", events[1].ErrorCategory);
            string artifact = File.ReadAllText(Path.Combine(session.RunDirectory, "tools/invocations.jsonl"));
            Assert.Contains("execute-id", artifact);
            Assert.DoesNotContain("private-key", artifact);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}