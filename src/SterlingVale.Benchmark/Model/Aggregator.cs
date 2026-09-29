using SterlingVale.Benchmark.Scoring;

namespace SterlingVale.Benchmark.Model;

/// <summary>Aggregates raw trial results into per-mode summaries. Never removes failed or slow trials.</summary>
public static class Aggregator
{
    /// <summary>Summarizes all trials for a single mode.</summary>
    public static ModeSummary Summarize(string mode, IReadOnlyList<TrialResult> trials)
    {
        ArgumentNullException.ThrowIfNull(trials);
        var modeTrials = trials.Where(t => t.Mode == mode).ToList();

        var durations = modeTrials.Select(t => t.DurationMs).ToList();
        var duration = new DurationStats(Median(durations), MinOrZero(durations), MaxOrZero(durations));

        var tokenValues = modeTrials.Select(t => t.TotalTokens).ToList();
        bool complete = tokenValues.Count > 0 && tokenValues.All(v => v is not null);
        var present = tokenValues.Where(v => v is not null).Select(v => v!.Value).ToList();
        var tokens = new TokenStats(
            present.Count == 0 ? null : (long)Median(present.Select(v => (double)v).ToList()),
            present.Count == 0 ? null : present.Min(),
            present.Count == 0 ? null : present.Max(),
            complete);

        var costValues = modeTrials.Select(t => t.EstimatedCostUsd).ToList();
        bool costComplete = costValues.Count > 0 && costValues.All(v => v is not null);
        var presentCosts = costValues.Where(v => v is not null).Select(v => v!.Value).ToList();
        var cost = new CostStats(
            presentCosts.Count == 0 ? null : MedianDecimal(presentCosts),
            presentCosts.Count == 0 ? null : presentCosts.Min(),
            presentCosts.Count == 0 ? null : presentCosts.Max(),
            costComplete);

        var roundTrips = new RoundTripStats(
            Median(modeTrials.Select(t => (double)t.ToolCallCount).ToList()),
            Median(modeTrials.Select(t => (double)t.ModelTurnCount).ToList()),
            Median(modeTrials.Select(t => (double)t.ExecuteCodeCallCount).ToList()));

        var scores = modeTrials.Select(t => t.Score).ToList();
        var correctness = new CorrectnessStats(
            Mean(scores.Select(s => s.FlaggedPrecision)),
            Mean(scores.Select(s => s.FlaggedRecall)),
            Mean(scores.Select(s => s.BreachPrecision)),
            Mean(scores.Select(s => s.BreachRecall)),
            MeanDecimal(scores.Select(s => s.AllocationMae)),
            MeanDecimal(scores.Select(s => s.TradeNotionalMaeFraction)),
            scores.Count == 0 ? 0m : scores.Max(s => s.MaxAllocationError),
            scores.Count(s => s.MatchClass == CorrectnessScorer.Match.Exact),
            scores.Count(s => s.MatchClass == CorrectnessScorer.Match.WithinTolerance),
            scores.Count(s => s.MatchClass == CorrectnessScorer.Match.Partial),
            scores.Count(s => s.MatchClass is CorrectnessScorer.Match.Failed
                or CorrectnessScorer.Match.InvalidSchema
                or CorrectnessScorer.Match.TimedOut
                or CorrectnessScorer.Match.Capped));

        return new ModeSummary(mode, modeTrials.Count, duration, tokens, cost, roundTrips, correctness);
    }

    /// <summary>Computes CodeAct-relative-to-Classic deltas; ratios are null when a baseline is zero or a value is missing.</summary>
    public static ComparisonDelta ComputeDelta(ModeSummary classic, ModeSummary codeact)
    {
        ArgumentNullException.ThrowIfNull(classic);
        ArgumentNullException.ThrowIfNull(codeact);

        double? speedup = codeact.DurationMs.Median > 0
            ? classic.DurationMs.Median / codeact.DurationMs.Median
            : null;

        return new ComparisonDelta(
            Fraction(classic.DurationMs.Median, codeact.DurationMs.Median),
            speedup,
            Fraction(classic.TotalTokens.Median, codeact.TotalTokens.Median),
            Fraction((double?)classic.Cost?.Median, (double?)codeact.Cost?.Median),
            Fraction(classic.RoundTrips?.MedianToolCalls, codeact.RoundTrips?.MedianToolCalls),
            Fraction(classic.RoundTrips?.MedianModelTurns, codeact.RoundTrips?.MedianModelTurns),
            codeact.Correctness.ExactCount - classic.Correctness.ExactCount,
            codeact.Correctness.MeanAllocationMae - classic.Correctness.MeanAllocationMae);
    }

    // Fraction of change from baseline; null when the baseline is not a positive number or the value is missing.
    private static double? Fraction(double? baseline, double? value) =>
        baseline is > 0 && value is not null ? (value.Value - baseline.Value) / baseline.Value : null;

    private static double Median(IReadOnlyList<double> values)
    {
        if (values.Count == 0)
        {
            return 0d;
        }

        var sorted = values.OrderBy(v => v).ToList();
        int mid = sorted.Count / 2;
        return sorted.Count % 2 == 1 ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) / 2d;
    }

    private static decimal MedianDecimal(IReadOnlyList<decimal> values)
    {
        var sorted = values.OrderBy(v => v).ToList();
        int mid = sorted.Count / 2;
        return sorted.Count % 2 == 1 ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) / 2m;
    }

    private static double MinOrZero(IReadOnlyList<double> values) => values.Count == 0 ? 0d : values.Min();

    private static double MaxOrZero(IReadOnlyList<double> values) => values.Count == 0 ? 0d : values.Max();

    private static double Mean(IEnumerable<double> values)
    {
        var list = values.ToList();
        return list.Count == 0 ? 0d : list.Average();
    }

    private static decimal MeanDecimal(IEnumerable<decimal> values)
    {
        var list = values.ToList();
        return list.Count == 0 ? 0m : list.Average();
    }
}
