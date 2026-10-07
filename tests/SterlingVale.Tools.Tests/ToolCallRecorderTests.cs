using Microsoft.Extensions.AI;
using SterlingVale.Telemetry;
using SterlingVale.Tools;
using SterlingVale.Tools.Observability;
using Xunit;

namespace SterlingVale.Tools.Tests;

/// <summary>
/// Verifies the host-side tool-call capture: the recording decorator records every invocation to the
/// ambient recorder (the only place CodeAct's in-sandbox call_tool calls can be seen) while delegating
/// identity, so it is a transparent pass-through when no recorder is active.
/// </summary>
public sealed class ToolCallRecorderTests
{
    private static AIFunction ListHouseholds()
    {
        var catalog = new ToolCatalog();
        var service = new PortfolioToolService(TestSnapshot.Build(), new ToolMetrics());
        return catalog.CreateTools(service).First(t => t.Name == ToolNames.ListHouseholds);
    }

    [Fact]
    public async Task Active_recorder_captures_name_status_and_result()
    {
        var tool = ListHouseholds();
        var recorder = new ToolCallRecorder();

        using (ToolCallRecorder.Activate(recorder))
        {
            await tool.InvokeAsync();
        }

        var call = Assert.Single(recorder.Snapshot());
        Assert.Equal(ToolNames.ListHouseholds, call.Name);
        Assert.Equal("ok", call.Status);
        Assert.False(string.IsNullOrEmpty(call.ResultJson));
    }

    [Fact]
    public async Task Without_active_recorder_the_wrapper_is_a_passthrough()
    {
        var tool = ListHouseholds();
        var result = await tool.InvokeAsync();
        Assert.NotNull(result);
    }

    [Fact]
    public void Wrapper_delegates_identity_so_the_fingerprint_is_unchanged()
    {
        var tool = ListHouseholds();

        // Delegated name/description/schema keep the tool fingerprint identical to the unwrapped tool.
        Assert.Equal(ToolNames.ListHouseholds, tool.Name);
        Assert.False(string.IsNullOrEmpty(tool.Description));
    }
}
