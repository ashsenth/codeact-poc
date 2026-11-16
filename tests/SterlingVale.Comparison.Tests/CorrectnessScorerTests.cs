using SterlingVale.AgentShared.Contracts;
using SterlingVale.Benchmark.Scoring;
using Xunit;

namespace SterlingVale.Comparison.Tests;

public sealed class CorrectnessScorerTests
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> PolicyTargets =
        new Dictionary<string, IReadOnlyList<string>> { ["H1"] = ["Equity", "Cash"], ["H2"] = ["Equity"] };

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
        var score = CorrectnessScorer.Score(oracle, oracle, "Completed", true, PolicyTargets);
        Assert.Equal(CorrectnessScorer.Match.Exact, score.MatchClass);
        Assert.Equal(1d, score.FlaggedPrecision);
        Assert.Equal(1d, score.FlaggedRecall);
        Assert.Equal(0m, score.AllocationMae);
    }

    [Fact]
    public void Self_reported_incomplete_is_partial_even_with_oracle_exact_values()
    {
        var oracle = Oracle();
        var actual = oracle with { Status = "incomplete", MissingItems = ["price for SYN001"] };
        var score = CorrectnessScorer.Score(actual, oracle, "Incomplete", true, PolicyTargets);
        Assert.Equal(CorrectnessScorer.Match.Partial, score.MatchClass);
        Assert.Equal(0m, score.AllocationMae);
    }

    [Fact]
    public void Invalid_schema_is_classified_invalid()
    {
        var score = CorrectnessScorer.Score(null, Oracle(), "Failed", schemaValid: false, PolicyTargets);
        Assert.Equal(CorrectnessScorer.Match.InvalidSchema, score.MatchClass);
    }

    [Fact]
    public void Timed_out_is_classified_timed_out()
    {
        var score = CorrectnessScorer.Score(null, Oracle(), "TimedOut", schemaValid: false, PolicyTargets);
        Assert.Equal(CorrectnessScorer.Match.TimedOut, score.MatchClass);
    }

    [Fact]
    public void Missing_breach_reduces_recall_and_is_unearned_completeness()
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

        var score = CorrectnessScorer.Score(actual, oracle, "Completed", true, PolicyTargets);
        Assert.Equal(CorrectnessScorer.Match.UnearnedCompleteness, score.MatchClass);
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

        var score = CorrectnessScorer.Score(actual, oracle, "Completed", true, PolicyTargets);
        Assert.True(score.MaxAllocationError > 0m);
        Assert.Equal(CorrectnessScorer.Match.Partial, score.MatchClass);
    }

    [Theory]
    [InlineData("complete", CorrectnessScorer.Match.UnearnedCompleteness)]
    [InlineData("incomplete", CorrectnessScorer.Match.Partial)]
    public void Missing_components_are_identified_per_household_and_errors_are_unknown(string reportStatus, string match)
    {
        var oracle = Oracle();
        var actual = oracle with
        {
            Status = reportStatus,
            Households = [oracle.Households[0] with { Drifts = [], ProposedTrades = [], Breaches = [], Flagged = false }],
        };
        var score = CorrectnessScorer.Score(actual, oracle, "Completed", true, PolicyTargets);
        Assert.Equal(match, score.MatchClass);
        Assert.True(score.CompletenessAssessed);
        Assert.Contains("allocation/drift:Equity", score.MissingComponents["H1"]);
        Assert.Contains("allocation/drift:Cash", score.MissingComponents["H1"]);
        Assert.Contains("breaches:Concentration (0/1)", score.MissingComponents["H1"]);
        Assert.Contains("trade:Cash", score.MissingComponents["H1"]);
        Assert.Equal(["household"], score.MissingComponents["H2"]);
        Assert.Null(score.AllocationMae);
        Assert.Null(score.MaxAllocationError);
        Assert.Null(score.TradeNotionalMaeFraction);
    }

    [Fact]
    public void Missing_only_a_required_trade_is_not_a_tolerance_pass()
    {
        var oracle = Oracle();
        var actual = oracle with { Households = [oracle.Households[0] with { ProposedTrades = [oracle.Households[0].ProposedTrades[0]] }, oracle.Households[1]] };
        var score = CorrectnessScorer.Score(actual, oracle, "Completed", true, PolicyTargets);
        Assert.Equal(CorrectnessScorer.Match.UnearnedCompleteness, score.MatchClass);
        Assert.Equal(0m, score.AllocationMae);
        Assert.Null(score.TradeNotionalMaeFraction);
        Assert.Equal(["trade:Cash"], score.MissingComponents["H1"]);
    }

    [Fact]
    public void Non_breached_class_trades_are_optional_and_only_policy_target_classes_are_required()
    {
        var oracle = Oracle();
        var actual = oracle with { Households = [oracle.Households[0], oracle.Households[1] with { ProposedTrades = [] }] };
        var score = CorrectnessScorer.Score(actual, oracle, "Completed", true, PolicyTargets);
        Assert.Equal(CorrectnessScorer.Match.Exact, score.MatchClass);
        Assert.Empty(score.MissingComponents);
    }

    [Fact]
    public void Subset_completeness_uses_only_oracle_scope_and_rejects_extra_households()
    {
        var full = Oracle();
        var scoped = full with { Households = [full.Households[0]] };
        var score = CorrectnessScorer.Score(scoped, scoped, "Completed", true, PolicyTargets);
        Assert.Equal(CorrectnessScorer.Match.Exact, score.MatchClass);
        Assert.Empty(score.MissingComponents);

        score = CorrectnessScorer.Score(full, scoped, "Completed", true, PolicyTargets);
        Assert.Equal(CorrectnessScorer.Match.Partial, score.MatchClass);
        Assert.Empty(score.MissingComponents);
    }

    [Fact]
    public void Breach_coverage_counts_each_required_breach_and_flag_must_follow_reported_breaches()
    {
        var oracle = Oracle();
        var h1 = oracle.Households[0] with
        {
            Breaches = [new("AllocationDrift", "Equity", .1m, .05m), new("AllocationDrift", "Cash", .1m, .05m),
                new("FxExposure", "FX", .4m, .3m), new("CryptoExposure", "Crypto", .2m, .1m)],
        };
        oracle = oracle with { Households = [h1, oracle.Households[1]] };
        var actual = oracle with { Households = [h1 with { Breaches = [h1.Breaches[0]], Flagged = false }, oracle.Households[1]] };
        var score = CorrectnessScorer.Score(actual, oracle, "Completed", true, PolicyTargets);
        Assert.Contains("breaches:AllocationDrift (1/2)", score.MissingComponents["H1"]);
        Assert.Contains("breaches:FxExposure (0/1)", score.MissingComponents["H1"]);
        Assert.Contains("breaches:CryptoExposure (0/1)", score.MissingComponents["H1"]);
        Assert.Contains("flagged:not derived from reported breaches", score.MissingComponents["H1"]);
    }
}
