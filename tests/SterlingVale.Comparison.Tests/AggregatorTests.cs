using SterlingVale.Benchmark.Model;
using SterlingVale.Benchmark.Scoring;
using Xunit;

namespace SterlingVale.Comparison.Tests;

public sealed class AggregatorTests
{
    private static readonly RunScore Exact =
        new(true, "Completed", 1d, 1d, 1d, 1d, 0m, 0m, 0m, CorrectnessScorer.Match.Exact);

    private static TrialResult Trial(string mode, int pair, double ms, long? tokens) =>
        new(pair, 0, mode, ms, "Completed", 1, mode == "codeact" ? 1 : 0, tokens, null, $"run-{mode}-{pair}", Exact);

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
}
