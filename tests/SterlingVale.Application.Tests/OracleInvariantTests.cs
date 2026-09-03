using SterlingVale.Application.Oracle;
using SterlingVale.Application.Tests.Support;
using SterlingVale.DataGenerator;
using SterlingVale.Domain;
using SterlingVale.Domain.Analysis;
using SterlingVale.Domain.Model;
using SterlingVale.Domain.ValueObjects;
using Xunit;

namespace SterlingVale.Application.Tests;

public sealed class OracleInvariantTests
{
    private static readonly DateTimeOffset AsOf = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static ExposureReport AnalyzeProfile(DatasetProfile profile)
    {
        var dataset = new SyntheticDataGenerator().Generate(profile);
        var snapshot = GeneratedSnapshotMapper.ToSnapshot(dataset);
        return new ExposureOracle().Analyze(snapshot, new AnalysisRequest(), "inv", AsOf);
    }

    public static IEnumerable<object[]> Profiles() =>
        DatasetProfile.All.Select(p => new object[] { p });

    [Theory]
    [MemberData(nameof(Profiles))]
    public void Allocations_sum_to_approximately_one(DatasetProfile profile)
    {
        var report = AnalyzeProfile(profile);
        foreach (var household in report.Households)
        {
            decimal sum = household.Drifts.Sum(d => d.Actual.Fraction);
            Assert.True(
                FinancialMath.ApproximatelyEqual(sum, 1m, FinancialMath.AllocationSumTolerance),
                $"{household.HouseholdId}: allocations summed to {sum}");
        }
    }

    [Theory]
    [MemberData(nameof(Profiles))]
    public void Proposed_notionals_net_to_approximately_zero(DatasetProfile profile)
    {
        var report = AnalyzeProfile(profile);
        foreach (var household in report.Households)
        {
            decimal net = household.ProposedTrades.Sum(t =>
                t.Direction == TradeDirection.Buy ? t.Notional.Amount : -t.Notional.Amount);
            decimal tolerance = FinancialMath.NetNotionalTolerance * household.TotalValue.Amount;
            Assert.True(
                Math.Abs(net) <= tolerance,
                $"{household.HouseholdId}: net notional {net} exceeded tolerance {tolerance}");
        }
    }

    [Theory]
    [MemberData(nameof(Profiles))]
    public void Every_household_has_positive_total(DatasetProfile profile)
    {
        var report = AnalyzeProfile(profile);
        Assert.All(report.Households, h => Assert.True(h.TotalValue.Amount > 0m));
    }

    [Fact]
    public void Deliberate_breaches_produce_flagged_households()
    {
        var report = AnalyzeProfile(DatasetProfile.Medium);
        Assert.Contains(report.Households, h => h.Flagged);
    }

    [Fact]
    public void Analysis_is_deterministic()
    {
        var first = AnalyzeProfile(DatasetProfile.Small);
        var second = AnalyzeProfile(DatasetProfile.Small);
        Assert.Equal(first.Households.Count, second.Households.Count);
        for (int i = 0; i < first.Households.Count; i++)
        {
            Assert.Equal(first.Households[i].TotalValue, second.Households[i].TotalValue);
            Assert.Equal(first.Households[i].Breaches.Count, second.Households[i].Breaches.Count);
        }
    }
}
