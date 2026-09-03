using System.Diagnostics;
using System.Diagnostics.Metrics;
using SterlingVale.Telemetry;
using SterlingVale.Tools;
using Xunit;

namespace SterlingVale.Tools.Tests;

/// <summary>
/// Guards the telemetry contract: tool instrumentation must emit only the stable low-cardinality
/// tags <c>tool.name</c> and <c>tool.status</c>. It must never leak tool arguments, results, or any
/// portfolio data into metric or trace attributes.
/// </summary>
public sealed class TelemetryRedactionTests
{
    private const string SensitiveArgument = "EQ_US_LARGE";
    private static readonly string[] AllowedTagKeys = ["tool.name", "tool.status"];

    [Fact]
    public void Tool_metrics_and_activity_expose_only_whitelisted_tags()
    {
        using var meter = new Meter("SterlingVale.Test.Redaction");
        var metricTags = new List<KeyValuePair<string, object?>>();
        var activityTags = new List<KeyValuePair<string, object?>>();

        using var meterListener = new MeterListener
        {
            InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter == meter)
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            },
        };
        meterListener.SetMeasurementEventCallback<long>((_, _, tags, _) => Capture(tags, metricTags));
        meterListener.SetMeasurementEventCallback<double>((_, _, tags, _) => Capture(tags, metricTags));
        meterListener.Start();

        using var activityListener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == SterlingValeTelemetry.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = activity => activityTags.AddRange(activity.TagObjects),
        };
        ActivitySource.AddActivityListener(activityListener);

        var service = new PortfolioToolService(TestSnapshot.Build(), new ToolMetrics(meter));
        _ = service.GetPrice(SensitiveArgument);

        Assert.NotEmpty(metricTags);
        Assert.NotEmpty(activityTags);

        Assert.All(metricTags, tag => Assert.Contains(tag.Key, AllowedTagKeys));
        Assert.All(activityTags, tag => Assert.Contains(tag.Key, AllowedTagKeys));

        foreach (var tag in metricTags.Concat(activityTags))
        {
            Assert.DoesNotContain(SensitiveArgument, tag.Value?.ToString() ?? string.Empty, StringComparison.Ordinal);
        }

        Assert.Contains(metricTags, tag => tag is { Key: "tool.name", Value: "get_price" });
        Assert.Contains(metricTags, tag => tag is { Key: "tool.status", Value: "ok" });
    }

    private static void Capture(
        ReadOnlySpan<KeyValuePair<string, object?>> tags,
        List<KeyValuePair<string, object?>> sink)
    {
        foreach (var tag in tags)
        {
            sink.Add(tag);
        }
    }
}
