using SterlingVale.AgentShared.Contracts;
using SterlingVale.Benchmark.Scoring;
using Xunit;

namespace SterlingVale.Comparison.Tests;

public sealed class CorrectnessScorerTests
{
    private static ExposureReportDto Oracle() => new(
        "oracle",
        "2026-01-01T00:00:00+00:00",
        [
            new HouseholdExposureDto(
                "H1", "USD", 1000m, Flagged: true,
                [new AllocationDriftDto("Equity", 0.6m, 0.5m, 0.1m, true), new AllocationDriftDto("Cash", 0.4m, 0.5m, -0.1m, true)],
                [new RiskBreachDto("Concentration", "Symbol X over limit.", 0.8m, 0.6m)],
                [new ProposedTradeDto("Equity", "Sell", 100m, "USD"), new ProposedTradeDto("Cash", "Buy", 100m, "USD")]),
            new HouseholdExposureDto(
                "H2", "USD", 500m, Flagged: false,
                [new AllocationDriftDto("Equity", 0.5m, 0.5m, 0m, false)],
                [],
                []),
        ]);

    [Fact]
    public void Identical_report_is_exact()
    {
        var oracle = Oracle();
        var score = CorrectnessScorer.Score(oracle, oracle, "Completed", true);
        Assert.Equal(CorrectnessScorer.Match.Exact, score.MatchClass);
        Assert.Equal(1d, score.FlaggedPrecision);
        Assert.Equal(1d, score.FlaggedRecall);
        Assert.Equal(0m, score.AllocationMae);
    }

    [Fact]
    public void Invalid_schema_is_classified_invalid()
    {
        var score = CorrectnessScorer.Score(null, Oracle(), "Failed", schemaValid: false);
        Assert.Equal(CorrectnessScorer.Match.InvalidSchema, score.MatchClass);
    }

    [Fact]
    public void Timed_out_is_classified_timed_out()
    {
        var score = CorrectnessScorer.Score(null, Oracle(), "TimedOut", schemaValid: false);
        Assert.Equal(CorrectnessScorer.Match.TimedOut, score.MatchClass);
    }

    [Fact]
    public void Missing_flag_reduces_recall_and_is_partial()
    {
        var oracle = Oracle();
        // Actual: first household not flagged and its breach dropped.
        var actual = oracle with
        {
            Households =
            [
                oracle.Households[0] with { Flagged = false, Breaches = [] },
                oracle.Households[1],
            ],
        };

        var score = CorrectnessScorer.Score(actual, oracle, "Completed", true);
        Assert.Equal(CorrectnessScorer.Match.Partial, score.MatchClass);
        Assert.True(score.FlaggedRecall < 1d);
        Assert.True(score.BreachRecall < 1d);
    }

    [Fact]
    public void Allocation_difference_produces_nonzero_error()
    {
        var oracle = Oracle();
        var actual = oracle with
        {
            Households =
            [
                oracle.Households[0] with
                {
                    Drifts = [new AllocationDriftDto("Equity", 0.7m, 0.5m, 0.2m, true), new AllocationDriftDto("Cash", 0.3m, 0.5m, -0.2m, true)],
                },
                oracle.Households[1],
            ],
        };

        var score = CorrectnessScorer.Score(actual, oracle, "Completed", true);
        Assert.True(score.MaxAllocationError > 0m);
        Assert.Equal(CorrectnessScorer.Match.Partial, score.MatchClass);
    }
}
