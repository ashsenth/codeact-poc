using SterlingVale.Telemetry;
using Xunit;

namespace SterlingVale.Tools.Tests;

public sealed class ExecutionConsoleTests
{
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