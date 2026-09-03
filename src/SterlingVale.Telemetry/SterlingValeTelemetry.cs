using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace SterlingVale.Telemetry;

/// <summary>
/// Stable, centrally-owned OpenTelemetry sources. All Activities and Metrics in Sterling Vale are
/// emitted through these named sources so exporters can subscribe by a single stable name.
/// </summary>
public static class SterlingValeTelemetry
{
    /// <summary>The telemetry schema/source version.</summary>
    public const string Version = "1.0.0";

    /// <summary>The single activity source name for tracing.</summary>
    public const string ActivitySourceName = "SterlingVale";

    /// <summary>The single meter name for metrics.</summary>
    public const string MeterName = "SterlingVale";

    /// <summary>The shared activity source.</summary>
    public static ActivitySource ActivitySource { get; } = new(ActivitySourceName, Version);

    /// <summary>The shared meter.</summary>
    public static Meter Meter { get; } = new(MeterName, Version);
}
