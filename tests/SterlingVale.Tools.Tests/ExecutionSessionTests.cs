using System.Diagnostics;
using System.Text.Json;
using SterlingVale.Telemetry;
using Xunit;

namespace SterlingVale.Tools.Tests;

public sealed class ExecutionSessionTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "sv-events-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void Events_are_persisted_before_disposal_and_linked_to_parent_trace()
    {
        using var parent = new Activity("request").SetIdFormat(ActivityIdFormat.W3C).Start();
        using var session = new ExecutionSession(_root, "classic", "small", "comparison", "trial");
        session.Emit("RUN_STARTED");
        using var stream = new FileStream(Path.Combine(session.RunDirectory, "events.jsonl"), FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);
        string line = Assert.IsType<string>(reader.ReadLine());
        Assert.Null(reader.ReadLine());
        using var parsed = JsonDocument.Parse(line);
        Assert.Equal(parent.TraceId.ToString(), parsed.RootElement.GetProperty("traceId").GetString());
        Assert.Equal("comparison", session.Context.ComparisonId);
        Assert.Equal("trial", session.Context.TrialId);
        Assert.Equal(1, parsed.RootElement.GetProperty("sequenceNumber").GetInt64());
        Assert.Same(session, ExecutionSession.Current);
    }

    [Fact]
    public async Task Concurrent_runs_and_sequences_are_isolated()
    {
        var contexts = await Task.WhenAll(Enumerable.Range(0, 4).Select(async index =>
        {
            await Task.Yield();
            using var session = new ExecutionSession(_root, "codeact", "small");
            await Task.WhenAll(Enumerable.Range(0, 10).Select(sequence => Task.Run(() =>
            {
                Assert.Same(session, ExecutionSession.Current);
                session.Emit("TOOL_CALL_STARTED", data: new { index, sequence });
            })));
            Assert.Equal(Enumerable.Range(1, 10).Select(value => (long)value), session.Snapshot().Select(entry => entry.SequenceNumber));
            return session.Context;
        }));
        Assert.Equal(4, contexts.Select(context => context.RunId).Distinct().Count());
        Assert.Equal(4, contexts.Select(context => context.CorrelationId).Distinct().Count());
        Assert.Null(ExecutionSession.Current);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);
}