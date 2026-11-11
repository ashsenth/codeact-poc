using SterlingVale.Telemetry;
using Xunit;

namespace SterlingVale.Tools.Tests;

public sealed class ExecutionConsoleTests
{
    [Fact]
    public void Normal_prints_full_ids_once_then_short_run_prefix_with_tool_duration_and_warmup()
    {
        const string runId = "12345678abcdef0012345678abcdef00";
        var start = new ExecutionEvent("event", "1.0", 1, DateTimeOffset.UnixEpoch, "RUN_STARTED",
            "correlation-full", runId, "comparison-full", "trial-full", "trace-full", "span-full",
            "classic", "small", "started", null, null, null) { IsWarmup = true };
        var tool = start with { EventType = "MODEL_TOOL_CALL_COMPLETED", SequenceNumber = 2,
            DurationMs = 12.5, Data = new { toolName = "get_price" } };
        string full = ExecutionConsole.Format(start, ExecutionLogLevel.Normal)!;
        string compact = ExecutionConsole.Format(tool, ExecutionLogLevel.Normal)!;
        Assert.Contains(runId, full);
        Assert.Contains("correlation-full", full);
        Assert.StartsWith("[12345678]", compact);
        Assert.Contains("get_price", compact);
        Assert.Contains("12.5ms", compact);
        Assert.Contains("WARM-UP, not measured", compact);
        Assert.DoesNotContain(runId, compact);
        Assert.DoesNotContain("correlation-full", compact);
        Assert.Contains(runId, ExecutionConsole.Format(tool, ExecutionLogLevel.Verbose));
        Assert.Contains(runId, ExecutionConsole.Format(tool, ExecutionLogLevel.Debug));
    }

    [Theory]
    [InlineData(ExecutionLogLevel.Minimal, false)]
    [InlineData(ExecutionLogLevel.Normal, true)]
    [InlineData(ExecutionLogLevel.Verbose, true)]
    [InlineData(ExecutionLogLevel.Debug, true)]
    public void Levels_filter_the_same_structured_event(ExecutionLogLevel level, bool visible)
    {
        var entry = new ExecutionEvent("event", "1.0", 1, DateTimeOffset.UnixEpoch, "TOOL_CALL_STARTED",
            "correlation", "run", "comparison", "trial", "trace", "span", "classic", "small", "ok", null, null,
            new { toolName = "get_price", arguments = new { symbol = "SYNTH001" } });
        string? text = ExecutionConsole.Format(entry, level);
        Assert.Equal(visible, text is not null);
        if (level == ExecutionLogLevel.Normal)
        {
            Assert.Contains("get_price", text);
            Assert.DoesNotContain("SYNTH001", text);
        }
        else if (level >= ExecutionLogLevel.Verbose)
        {
            Assert.Contains("SYNTH001", text);
        }
    }
}