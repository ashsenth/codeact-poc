using SterlingVale.Benchmark.Model;
using SterlingVale.Benchmark.Scoring;
using Xunit;

namespace SterlingVale.Comparison.Tests;

public sealed class AggregatorTests
{
    private static readonly RunScore Exact =
        new(true, "Completed", 1d, 1d, 1d, 1d, 0m, 0m, 0m, CorrectnessScorer.Match.Exact);

    private static TrialResult Trial(string mode, int pair, double ms, long? tokens) =>
        new(pair, 0, mode, ms, "Completed", 1, 2, mode == "codeact" ? 1 : 0, tokens, null, $"run-{mode}-{pair}", Exact);

    private static TrialResult DetailedTrial(
        string mode, int pair, double ms, int toolCalls, int modelTurns, int executeCode, long? tokens, decimal? cost) =>
        new(pair, 0, mode, ms, "Completed", toolCalls, modelTurns, executeCode, tokens, cost, $"run-{mode}-{pair}", Exact);

    [Fact]
    public void Durations_report_median_min_max()
    {
        var trials = new List<TrialResult>
        {
            Trial("classic", 1, 30, 300),
            Trial("classic", 2, 10, 100),
            Trial("classic", 3, 20, 200),
        };

        var summary = Aggregator.Summarize("classic", trials);
        Assert.Equal(3, summary.Trials);
        Assert.Equal(20d, summary.DurationMs.Median);
        Assert.Equal(10d, summary.DurationMs.Min);
        Assert.Equal(30d, summary.DurationMs.Max);
        Assert.Equal(3, summary.Correctness.ExactCount);
    }

    [Fact]
    public void Token_stats_are_complete_when_all_present()
    {
        var trials = new List<TrialResult> { Trial("classic", 1, 10, 100), Trial("classic", 2, 20, 300) };
        var summary = Aggregator.Summarize("classic", trials);
        Assert.True(summary.TotalTokens.Complete);
        Assert.Equal(100, summary.TotalTokens.Min);
        Assert.Equal(300, summary.TotalTokens.Max);
    }

    [Fact]
    public void Token_stats_are_incomplete_when_usage_missing()
    {
        var trials = new List<TrialResult> { Trial("classic", 1, 10, 100), Trial("classic", 2, 20, null) };
        var summary = Aggregator.Summarize("classic", trials);
        Assert.False(summary.TotalTokens.Complete);
        // Missing usage is never invented; the present value is still summarized.
        Assert.Equal(100, summary.TotalTokens.Min);
    }

    [Fact]
    public void Cost_stats_summarize_present_values_and_flag_completeness()
    {
        var trials = new List<TrialResult>
        {
            DetailedTrial("classic", 1, 10, 5, 6, 0, 100, 0.002m),
            DetailedTrial("classic", 2, 20, 7, 8, 0, 200, 0.004m),
        };

        var summary = Aggregator.Summarize("classic", trials);
        Assert.NotNull(summary.Cost);
        Assert.True(summary.Cost!.Complete);
        Assert.Equal(0.003m, summary.Cost.Median);
        Assert.Equal(0.002m, summary.Cost.Min);
        Assert.Equal(0.004m, summary.Cost.Max);
    }

    [Fact]
    public void Cost_stats_incomplete_when_estimate_missing()
    {
        var trials = new List<TrialResult>
        {
            DetailedTrial("classic", 1, 10, 5, 6, 0, 100, 0.002m),
            DetailedTrial("classic", 2, 20, 7, 8, 0, 200, null),
        };

        var summary = Aggregator.Summarize("classic", trials);
        Assert.False(summary.Cost!.Complete);
        Assert.Equal(0.002m, summary.Cost.Min);
    }

    [Fact]
    public void RoundTrip_medians_capture_tool_turn_and_code_counts()
    {
        var trials = new List<TrialResult>
        {
            DetailedTrial("classic", 1, 10, 8, 9, 0, 100, null),
            DetailedTrial("classic", 2, 20, 12, 13, 0, 200, null),
        };

        var summary = Aggregator.Summarize("classic", trials);
        Assert.NotNull(summary.RoundTrips);
        Assert.Equal(10d, summary.RoundTrips!.MedianToolCalls);
        Assert.Equal(11d, summary.RoundTrips.MedianModelTurns);
        Assert.Equal(0d, summary.RoundTrips.MedianExecuteCodeCalls);
    }

    [Fact]
    public void ComputeDelta_reports_codeact_reduction_relative_to_classic()
    {
        var classic = Aggregator.Summarize("classic",
            [DetailedTrial("classic", 1, 30, 10, 12, 0, 300, 0.006m)]);
        var codeact = Aggregator.Summarize("codeact",
            [DetailedTrial("codeact", 1, 10, 1, 2, 1, 150, 0.003m)]);

        var delta = Aggregator.ComputeDelta(classic, codeact);

        Assert.Equal(-2d / 3d, delta.DurationDeltaFraction!.Value, 6);
        Assert.Equal(3d, delta.DurationSpeedupFactor!.Value, 6);
        Assert.Equal(-0.5d, delta.TokenDeltaFraction!.Value, 6);
        Assert.Equal(-0.5d, delta.CostDeltaFraction!.Value, 6);
        Assert.Equal(-0.9d, delta.ToolCallDeltaFraction!.Value, 6);
        Assert.Equal(0, delta.ExactCountDelta);
    }

    [Fact]
    public void ComputeDelta_is_null_when_baseline_is_zero_or_missing()
    {
        var empty = Aggregator.Summarize("classic", new List<TrialResult>());
        var codeact = Aggregator.Summarize("codeact",
            [DetailedTrial("codeact", 1, 10, 1, 2, 1, 150, 0.003m)]);

        var delta = Aggregator.ComputeDelta(empty, codeact);

        Assert.Null(delta.DurationDeltaFraction);
        Assert.Null(delta.TokenDeltaFraction);
        Assert.Null(delta.ToolCallDeltaFraction);
    }
}
