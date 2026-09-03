using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace SterlingVale.Telemetry;

/// <summary>
/// Instruments tool invocations: a duration histogram and a call counter, tagged only by the
/// stable tool name. Never records arguments, results, or any portfolio data as attributes.
/// </summary>
public sealed class ToolMetrics
{
    private const string ToolNameTag = "tool.name";
    private const string StatusTag = "tool.status";

    private readonly Histogram<double> _duration;
    private readonly Counter<long> _calls;

    /// <summary>Creates tool metrics on the given meter (defaults to the shared meter).</summary>
    public ToolMetrics(Meter? meter = null)
    {
        var m = meter ?? SterlingValeTelemetry.Meter;
        _duration = m.CreateHistogram<double>(
            "sterlingvale.tool.duration",
            unit: "ms",
            description: "Duration of a granular tool invocation.");
        _calls = m.CreateCounter<long>(
            "sterlingvale.tool.calls",
            unit: "{call}",
            description: "Count of granular tool invocations.");
    }

    /// <summary>Begins a measured scope for a tool call. Dispose to record duration and count.</summary>
    public ToolCallScope Measure(string toolName) => new(this, toolName);

    private void Record(string toolName, double milliseconds, string status)
    {
        var toolTag = new KeyValuePair<string, object?>(ToolNameTag, toolName);
        var statusTag = new KeyValuePair<string, object?>(StatusTag, status);
        _duration.Record(milliseconds, toolTag, statusTag);
        _calls.Add(1, toolTag, statusTag);
    }

    /// <summary>A disposable measurement scope that records duration and an Activity on dispose.</summary>
    public readonly struct ToolCallScope : IDisposable
    {
        private readonly ToolMetrics _owner;
        private readonly string _toolName;
        private readonly long _startTimestamp;
        private readonly Activity? _activity;

        internal ToolCallScope(ToolMetrics owner, string toolName)
        {
            _owner = owner;
            _toolName = toolName;
            _startTimestamp = Stopwatch.GetTimestamp();
            _activity = SterlingValeTelemetry.ActivitySource.StartActivity(
                $"tool/{toolName}", ActivityKind.Internal);
            _activity?.SetTag(ToolNameTag, toolName);
        }

        /// <summary>Marks the scope as failed; the recorded status becomes "error".</summary>
        public void MarkError() => _activity?.SetStatus(ActivityStatusCode.Error);

        /// <inheritdoc />
        public void Dispose()
        {
            double ms = Stopwatch.GetElapsedTime(_startTimestamp).TotalMilliseconds;
            string status = _activity?.Status == ActivityStatusCode.Error ? "error" : "ok";
            _owner.Record(_toolName, ms, status);
            _activity?.Dispose();
        }
    }
}
