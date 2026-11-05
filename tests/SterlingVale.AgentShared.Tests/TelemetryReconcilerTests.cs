using SterlingVale.AgentShared.Analysis;
using SterlingVale.Telemetry;
using Xunit;

namespace SterlingVale.AgentShared.Tests;

public sealed class TelemetryReconcilerTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "sv-reconcile-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void Count_and_token_disagreements_fail_validity()
    {
        using var session = new ExecutionSession(_root, "classic", "small");
        session.Emit("MODEL_REQUEST_STARTED");
        session.Emit("MODEL_RESPONSE_RECEIVED");
        session.RecordUsage(new ModelUsage(1, 10, 5, 15));
        var correct = TelemetryReconciler.FromEvidence(session, TimeSpan.Zero, true, null);
        Assert.True(correct.TelemetryValid);
        var incorrect = TelemetryReconciler.Reconcile(correct with { TotalTokens = 16, ModelRequestCount = 2 }, session);
        Assert.False(incorrect.TelemetryValid);
        Assert.Contains("Total token total mismatch", incorrect.TelemetryErrors);
        Assert.Contains("Model request count mismatch", incorrect.TelemetryErrors);
    }

    [Fact]
    public void Missing_usage_remains_unavailable_and_unfinished_calls_fail()
    {
        using var session = new ExecutionSession(_root, "codeact", "small");
        session.Emit("MODEL_REQUEST_STARTED");
        session.Emit("MODEL_RESPONSE_RECEIVED");
        session.RecordUsage(new ModelUsage(1, null, null, null));
        session.Emit("EXECUTE_CODE_CALL_STARTED");
        var metrics = TelemetryReconciler.FromEvidence(session, TimeSpan.Zero, false, null);
        Assert.Null(metrics.TotalTokens);
        Assert.False(metrics.TelemetryValid);
        Assert.Contains("Unfinished code execution", metrics.TelemetryErrors);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);
}