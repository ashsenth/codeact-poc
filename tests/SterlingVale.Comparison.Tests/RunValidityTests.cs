using SterlingVale.AgentShared.Analysis;
using SterlingVale.AgentShared.Contracts;
using SterlingVale.Benchmark;
using SterlingVale.Benchmark.Scoring;
using SterlingVale.Domain.Analysis;
using SterlingVale.Telemetry;
using Xunit;

namespace SterlingVale.Comparison.Tests;

public sealed class RunValidityTests
{
    private static ExecutionEvent Event(long sequence, string type, string? category = null, object? data = null) =>
        new("event-" + sequence, "1", sequence, DateTimeOffset.UnixEpoch, type, "correlation", "run",
            "comparison", "trial", "trace", "span", "classic", "small", "error", null, category, data);

    private static RunRecord Record(string mode, IReadOnlyList<ExecutionEvent> events) => new()
    {
        Mode = mode, RawModelOutput = "", Events = events,
        Response = new() { RunId = "run", Mode = mode, Status = RunStatus.Failed,
            Metrics = new() { Duration = TimeSpan.Zero, TelemetryValid = true, SchemaValid = false } },
        Fingerprints = new() { PromptHash = "p", SchemaHash = "s", ToolFingerprint = "t",
            ModelFingerprint = "m", PricingFingerprint = "c", DatasetHash = "d" },
    };

    [Theory]
    [InlineData("classic", "ToolArgumentException", false)]
    [InlineData("codeact", "ToolArgumentException", false)]
    [InlineData("classic", "ToolNotFoundException", false)]
    [InlineData("codeact", "ToolNotFoundException", false)]
    [InlineData("classic", "ToolArgumentException", true)]
    [InlineData("codeact", "ToolArgumentException", true)]
    [InlineData("classic", "ToolNotFoundException", true)]
    [InlineData("codeact", "ToolNotFoundException", true)]
    public void Returned_argument_errors_are_counted_once_and_do_not_invalidate_model_or_warmup_outcomes(string mode, string category, bool copiedToTerminal)
    {
        var record = Record(mode, [
            Event(1, "TOOL_CALL_FAILED", category, new { invocationId = "child", parentInvocationId = "model" }),
            Event(2, "MODEL_TOOL_CALL_FAILED", category, new { invocationId = "model" }),
            Event(3, "MODEL_REQUEST_STARTED"),
            Event(4, "JSON_VALIDATION_COMPLETED") with { Status = "failed" },
            Event(5, "RUN_FAILED", copiedToTerminal ? category : "InvalidModelOutput"),
        ]);
        Assert.Equal(1, RunValidity.ToolArgumentErrors(record.Events));
        Assert.Equal("InvalidModelOutput", BenchmarkHarness.FindFailure(record)!.Category);
        var warmup = BenchmarkHarness.AssessWarmup(record, new ExposureReportDto("o", "", []),
            new Dictionary<string, IReadOnlyList<string>>());
        Assert.False(warmup.InfrastructureFailed);
        Assert.Equal(CorrectnessScorer.Match.InvalidSchema, warmup.Score.MatchClass);
        Assert.Equal(1, warmup.ToolArgumentErrors);
    }

    [Theory]
    [InlineData("MODEL_REQUEST_FAILED", "ClientResultException")]
    [InlineData("MODEL_REQUEST_FAILED", "HttpRequestException")]
    [InlineData("WORKER_TERMINATED", "WorkerFailed")]
    [InlineData("CODE_EXECUTION_FAILED", "SandboxException")]
    [InlineData("TOOL_CALL_FAILED", "InvalidOperationException")]
    public void Infrastructure_is_not_hidden_by_model_errors_or_later_continuation(string type, string category)
    {
        foreach (string mode in new[] { "classic", "codeact" })
        {
            var record = Record(mode, [
                Event(1, "RUN_FAILED", "InvalidModelOutput"),
                Event(2, type, category),
                Event(3, "MODEL_REQUEST_STARTED"),
            ]);
            Assert.Equal(category, BenchmarkHarness.FindFailure(record)!.Category);
            Assert.Equal(0, RunValidity.ToolArgumentErrors(record.Events));
            Assert.True(BenchmarkHarness.AssessWarmup(record, new ExposureReportDto("o", "", []),
                new Dictionary<string, IReadOnlyList<string>>()).InfrastructureFailed);
        }
    }

    [Fact]
    public void Argument_exception_without_return_and_continuation_remains_infrastructure()
    {
        var events = new[] { Event(1, "TOOL_CALL_FAILED", "ToolArgumentException") };
        Assert.Equal(1, RunValidity.ToolArgumentErrors(events));
        Assert.Equal("ToolArgumentException", RunValidity.FindFailure("Failed", events)!.Category);
    }
}
