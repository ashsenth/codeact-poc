using System.Collections.Concurrent;

namespace SterlingVale.Tools.Observability;

/// <summary>One captured host-side tool invocation (both Classic calls and CodeAct sandbox call_tool).</summary>
public sealed record ToolCallRecord(
    int Seq,
    string Name,
    string? ArgumentsJson,
    string? ResultJson,
    double DurationMs,
    string Status,
    DateTimeOffset Timestamp);

/// <summary>
/// Collects <see cref="ToolCallRecord"/>s for one run. The active recorder is ambient (AsyncLocal) so
/// the tool decorator can find it without threading it through the agent-construction call chain; the
/// runner activates one per run. CodeAct's in-sandbox tool calls never appear in the model messages,
/// so this host-side capture is the only place they are recorded.
/// </summary>
public sealed class ToolCallRecorder
{
    private static readonly AsyncLocal<ToolCallRecorder?> Ambient = new();

    private readonly ConcurrentQueue<ToolCallRecord> _records = new();
    private int _seq = -1;

    /// <summary>The recorder active on the current async flow, or null when none is active.</summary>
    public static ToolCallRecorder? Current => Ambient.Value;

    /// <summary>Makes <paramref name="recorder"/> the ambient recorder until the returned scope is disposed.</summary>
    public static IDisposable Activate(ToolCallRecorder recorder)
    {
        ArgumentNullException.ThrowIfNull(recorder);
        var previous = Ambient.Value;
        Ambient.Value = recorder;
        return new Scope(previous);
    }

    /// <summary>Records one completed tool invocation.</summary>
    public void Add(string name, string? argumentsJson, string? resultJson, double durationMs, string status) =>
        _records.Enqueue(new ToolCallRecord(
            Interlocked.Increment(ref _seq), name, argumentsJson, resultJson, durationMs, status, DateTimeOffset.UtcNow));

    /// <summary>Returns the captured calls in invocation order.</summary>
    public IReadOnlyList<ToolCallRecord> Snapshot() => _records.OrderBy(r => r.Seq).ToArray();

    private sealed class Scope(ToolCallRecorder? previous) : IDisposable
    {
        public void Dispose() => Ambient.Value = previous;
    }
}
