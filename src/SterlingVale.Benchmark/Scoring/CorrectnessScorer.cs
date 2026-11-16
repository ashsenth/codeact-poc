using SterlingVale.AgentShared.Contracts;
using SterlingVale.Domain;

namespace SterlingVale.Benchmark.Scoring;

/// <summary>
/// Scores a run's structured output against the oracle report. Compares normalized structured data
/// (sorted by stable keys) — never prose. Produces flagged/breach precision &amp; recall, allocation
/// and trade-notional error, and a match classification.
/// </summary>
public static class CorrectnessScorer
{
    private const decimal Tolerance = FinancialMath.DefaultRatioTolerance;

    /// <summary>Match classifications, from best to worst.</summary>
    public static class Match
    {
        public const string Exact = "Exact";
        public const string WithinTolerance = "WithinTolerance";
        public const string Partial = "Partial";
        public const string UnearnedCompleteness = "UnearnedCompleteness";
        public const string InvalidSchema = "InvalidSchema";
        public const string Failed = "Failed";
        public const string TimedOut = "TimedOut";
        public const string Capped = "Capped";
    }

    /// <summary>Scores <paramref name="actual"/> against the <paramref name="oracle"/> report.</summary>
    public static RunScore Score(ExposureReportDto? actual, ExposureReportDto oracle, string status, bool schemaValid,
        IReadOnlyDictionary<string, IReadOnlyList<string>> policyTargetClasses)
    {
        ArgumentNullException.ThrowIfNull(oracle);
        ArgumentNullException.ThrowIfNull(policyTargetClasses);

        if (status is "TimedOut")
        {
            return RunScore.ForFailure(status, schemaValid, Match.TimedOut);
        }

        if (status is "Capped")
        {
            return RunScore.ForFailure(status, schemaValid, Match.Capped);
        }

        if (!schemaValid || actual is null)
        {
            return RunScore.ForFailure(status, schemaValid, schemaValid ? Match.Failed : Match.InvalidSchema);
        }

        var (flaggedP, flaggedR) = ScoreSet(
            actual.Households.Where(h => h.Flagged).Select(h => h.HouseholdId),
            oracle.Households.Where(h => h.Flagged).Select(h => h.HouseholdId));

        var (breachP, breachR) = ScoreSet(BreachKeys(actual), BreachKeys(oracle));

        var missing = MissingComponents(actual, oracle, policyTargetClasses);
        bool missingAllocations = missing.Values.SelectMany(items => items)
            .Any(item => item == "household" || item.StartsWith("allocation/drift:", StringComparison.Ordinal));
        bool missingTrades = missing.Values.SelectMany(items => items)
            .Any(item => item == "household" || item.StartsWith("trade:", StringComparison.Ordinal));
        (decimal? Mae, decimal? Max) allocationError = missingAllocations ? (null, null) : AllocationError(actual, oracle);
        var (allocMae, maxAllocError) = allocationError;
        decimal? tradeMae = missingTrades ? null : TradeNotionalError(actual, oracle);

        bool setsPerfect = flaggedP == 1d && flaggedR == 1d && breachP == 1d && breachR == 1d;
        bool scopeMatches = actual.Households.Count == oracle.Households.Count &&
            actual.Households.Select(h => h.HouseholdId).ToHashSet(StringComparer.Ordinal)
                .SetEquals(oracle.Households.Select(h => h.HouseholdId));
        string matchClass =
            actual.Status == "incomplete" ? Match.Partial :
            missing.Count > 0 ? Match.UnearnedCompleteness :
            !scopeMatches ? Match.Partial :
            setsPerfect && allocMae == 0m && tradeMae == 0m ? Match.Exact :
            setsPerfect && maxAllocError <= Tolerance ? Match.WithinTolerance :
            Match.Partial;

        return new RunScore(
            schemaValid,
            status,
            flaggedP,
            flaggedR,
            breachP,
            breachR,
            allocMae,
            tradeMae,
            maxAllocError,
            matchClass)
        {
            CompletenessAssessed = true,
            MissingComponents = missing,
        };
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> MissingComponents(
        ExposureReportDto actual, ExposureReportDto oracle,
        IReadOnlyDictionary<string, IReadOnlyList<string>> policyTargetClasses)
    {
        var result = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        foreach (var expected in oracle.Households)
        {
            var household = actual.Households.FirstOrDefault(h => h.HouseholdId == expected.HouseholdId);
            var missing = new List<string>();
            if (household is null)
            {
                result[expected.HouseholdId] = ["household"];
                continue;
            }
            foreach (string assetClass in policyTargetClasses[expected.HouseholdId])
                if (!household.Drifts.Any(drift => drift.AssetClass == assetClass))
                    missing.Add($"allocation/drift:{assetClass}");
            foreach (var breaches in expected.Breaches.GroupBy(breach => breach.Kind))
            {
                int present = household.Breaches.Count(breach => breach.Kind == breaches.Key);
                if (present < breaches.Count())
                    missing.Add($"breaches:{breaches.Key} ({present}/{breaches.Count()})");
            }
            foreach (var drift in expected.Drifts.Where(drift => drift.Breached))
                if (expected.ProposedTrades.Any(trade => trade.AssetClass == drift.AssetClass) &&
                    !household.ProposedTrades.Any(trade => trade.AssetClass == drift.AssetClass))
                    missing.Add($"trade:{drift.AssetClass}");
            if (household.Flagged != (household.Breaches.Count > 0))
                missing.Add("flagged:not derived from reported breaches");
            if (missing.Count > 0) result[expected.HouseholdId] = missing;
        }
        return result;
    }

    private static IEnumerable<string> BreachKeys(ExposureReportDto report) =>
        report.Households.SelectMany(h => h.Breaches.Select(b => $"{h.HouseholdId}|{b.Kind}"));

    private static (double Precision, double Recall) ScoreSet(IEnumerable<string> actual, IEnumerable<string> expected)
    {
        var a = actual.ToHashSet(StringComparer.Ordinal);
        var e = expected.ToHashSet(StringComparer.Ordinal);
        int tp = a.Count(e.Contains);
        int fp = a.Count - tp;
        int fn = e.Count - tp;
        double precision = tp + fp == 0 ? 1d : (double)tp / (tp + fp);
        double recall = tp + fn == 0 ? 1d : (double)tp / (tp + fn);
        return (precision, recall);
    }

    private static (decimal Mae, decimal Max) AllocationError(ExposureReportDto actual, ExposureReportDto oracle)
    {
        var oracleByHousehold = oracle.Households.ToDictionary(h => h.HouseholdId, StringComparer.Ordinal);
        decimal total = 0m;
        decimal max = 0m;
        int count = 0;

        foreach (var actualHousehold in actual.Households)
        {
            if (!oracleByHousehold.TryGetValue(actualHousehold.HouseholdId, out var oracleHousehold))
            {
                continue;
            }

            var oracleDrift = oracleHousehold.Drifts.ToDictionary(d => d.AssetClass, StringComparer.Ordinal);
            foreach (var drift in actualHousehold.Drifts)
            {
                if (!oracleDrift.TryGetValue(drift.AssetClass, out var od))
                {
                    continue;
                }

                decimal error = Math.Abs(drift.ActualFraction - od.ActualFraction);
                total += error;
                max = Math.Max(max, error);
                count++;
            }
        }

        return (count == 0 ? 0m : FinancialMath.RoundRatio(total / count), FinancialMath.RoundRatio(max));
    }

    private static decimal TradeNotionalError(ExposureReportDto actual, ExposureReportDto oracle)
    {
        var oracleByHousehold = oracle.Households.ToDictionary(h => h.HouseholdId, StringComparer.Ordinal);
        decimal total = 0m;
        int count = 0;

        foreach (var actualHousehold in actual.Households)
        {
            if (!oracleByHousehold.TryGetValue(actualHousehold.HouseholdId, out var oracleHousehold))
            {
                continue;
            }

            decimal denominator = oracleHousehold.TotalValue == 0m ? 1m : Math.Abs(oracleHousehold.TotalValue);
            var oracleTrades = oracleHousehold.ProposedTrades.ToDictionary(t => t.AssetClass, StringComparer.Ordinal);
            foreach (var trade in actualHousehold.ProposedTrades)
            {
                decimal actualSigned = Signed(trade.Direction, trade.Notional);
                decimal oracleSigned = oracleTrades.TryGetValue(trade.AssetClass, out var ot) ? Signed(ot.Direction, ot.Notional) : 0m;
                total += Math.Abs(actualSigned - oracleSigned) / denominator;
                count++;
            }
        }

        return count == 0 ? 0m : FinancialMath.RoundRatio(total / count);
    }

    private static decimal Signed(string direction, decimal notional) =>
        string.Equals(direction, "Sell", StringComparison.OrdinalIgnoreCase) ? -notional : notional;
}
