using SterlingVale.Application.Oracle;
using SterlingVale.Application.Portfolio;
using SterlingVale.Application.Pricing;
using SterlingVale.Application.Tests.Support;
using SterlingVale.Domain;
using SterlingVale.Domain.Analysis;
using SterlingVale.Domain.Model;
using SterlingVale.Domain.ValueObjects;
using Xunit;

namespace SterlingVale.Application.Tests;

public sealed class OracleTests
{
    private static readonly DateTimeOffset AsOf = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static HouseholdExposure Analyze(PortfolioSnapshot snapshot) =>
        new ExposureOracle().Analyze(snapshot, new AnalysisRequest(), "run", AsOf).Households.Single();

    /// <summary>A USD household allocated exactly to target: no drift, no breaches, no trades.</summary>
    private static PortfolioSnapshot BalancedHousehold() =>
        new PortfolioBuilder()
            .Household("H0001", Currency.Usd)
            .Account("H0001-A01", "H0001", Currency.Usd)
            .Price("EQ", 100m, Currency.Usd)
            .Price("FI", 100m, Currency.Usd)
            .Price("CASH", 1m, Currency.Usd)
            .Price("ALT", 10m, Currency.Usd)
            .Price("CRY", 30m, Currency.Usd)
            .Position("P1", "H0001-A01", "EQ", 5m, AssetClass.Equity, Currency.Usd)     // 500
            .Position("P2", "H0001-A01", "FI", 3m, AssetClass.FixedIncome, Currency.Usd) // 300
            .Position("P3", "H0001-A01", "CASH", 100m, AssetClass.Cash, Currency.Usd)    // 100
            .Position("P4", "H0001-A01", "ALT", 7m, AssetClass.Alternative, Currency.Usd) // 70
            .Position("P5", "H0001-A01", "CRY", 1m, AssetClass.Crypto, Currency.Usd)      // 30
            .Build();

    [Fact]
    public void Balanced_household_has_total_and_no_breaches()
    {
        var exposure = Analyze(BalancedHousehold());
        Assert.Equal(1000m, exposure.TotalValue.Amount);
        Assert.False(exposure.Flagged);
        Assert.Empty(exposure.Breaches);
    }

    [Fact]
    public void Balanced_household_allocations_match_targets()
    {
        var exposure = Analyze(BalancedHousehold());
        var equity = exposure.Drifts.Single(d => d.AssetClass == AssetClass.Equity);
        Assert.Equal(0.50m, equity.Actual.Fraction);
        Assert.Equal(0m, equity.Drift.Fraction);
        Assert.False(equity.Breached);
    }

    [Fact]
    public void Balanced_household_proposes_zero_notional_trades()
    {
        var exposure = Analyze(BalancedHousehold());
        Assert.All(exposure.ProposedTrades, t => Assert.Equal(0m, t.Notional.Amount));
    }

    [Fact]
    public void Crypto_overweight_triggers_crypto_breach()
    {
        var snapshot = new PortfolioBuilder()
            .Household("H1", Currency.Usd)
            .Account("H1-A", "H1", Currency.Usd)
            .Price("EQ", 100m, Currency.Usd)
            .Price("CRY", 100m, Currency.Usd)
            .Position("P1", "H1-A", "EQ", 5m, AssetClass.Equity, Currency.Usd)   // 500
            .Position("P2", "H1-A", "CRY", 2m, AssetClass.Crypto, Currency.Usd)  // 200 -> 28.6% > 10%
            .Build();

        var exposure = Analyze(snapshot);
        Assert.Contains(exposure.Breaches, b => b.Kind == RiskBreachKind.CryptoExposure);
        Assert.True(exposure.Flagged);
    }

    [Fact]
    public void Single_symbol_over_limit_triggers_concentration_breach()
    {
        var snapshot = new PortfolioBuilder()
            .Household("H1", Currency.Usd)
            .Account("H1-A", "H1", Currency.Usd)
            .Price("EQ", 100m, Currency.Usd)
            .Price("FI", 100m, Currency.Usd)
            .Position("P1", "H1-A", "EQ", 8m, AssetClass.Equity, Currency.Usd)      // 800 -> 80% > 60%
            .Position("P2", "H1-A", "FI", 2m, AssetClass.FixedIncome, Currency.Usd) // 200
            .Build();

        var exposure = Analyze(snapshot);
        Assert.Contains(exposure.Breaches, b => b.Kind == RiskBreachKind.Concentration);
    }

    [Fact]
    public void Foreign_currency_over_limit_triggers_fx_breach()
    {
        var snapshot = new PortfolioBuilder()
            .Household("H1", Currency.Usd)
            .Account("H1-USD", "H1", Currency.Usd)
            .Account("H1-EUR", "H1", Currency.Eur)
            .Price("EQ", 100m, Currency.Usd)
            .Price("EQ_EU", 100m, Currency.Eur)
            .Fx(Currency.Eur, Currency.Usd, 1.1m)
            .Position("P1", "H1-USD", "EQ", 4m, AssetClass.Equity, Currency.Usd)     // 400 USD
            .Position("P2", "H1-EUR", "EQ_EU", 5m, AssetClass.Equity, Currency.Eur)  // 500 EUR -> 550 USD
            .Build();

        var exposure = Analyze(snapshot);
        // non-base = 550 / 950 = 57.9% > 30%
        Assert.Contains(exposure.Breaches, b => b.Kind == RiskBreachKind.FxExposure);
    }

    [Fact]
    public void Missing_fx_rate_throws_during_analysis()
    {
        var snapshot = new PortfolioBuilder()
            .Household("H1", Currency.Usd)
            .Account("H1-A", "H1", Currency.Jpy)
            .Price("EQ_JP", 1000m, Currency.Jpy)
            .Position("P1", "H1-A", "EQ_JP", 10m, AssetClass.Equity, Currency.Jpy)
            .Build();

        Assert.Throws<FxRateNotFoundException>(() => Analyze(snapshot));
    }

    [Fact]
    public void Zero_value_household_yields_zero_total_and_no_trades()
    {
        var snapshot = new PortfolioBuilder()
            .Household("H1", Currency.Usd)
            .Account("H1-A", "H1", Currency.Usd)
            .Build();

        var exposure = Analyze(snapshot);
        Assert.Equal(0m, exposure.TotalValue.Amount);
        Assert.Empty(exposure.ProposedTrades);
    }

    [Fact]
    public void Proposed_notionals_net_to_approximately_zero()
    {
        var snapshot = new PortfolioBuilder()
            .Household("H1", Currency.Usd)
            .Account("H1-A", "H1", Currency.Usd)
            .Price("EQ", 100m, Currency.Usd)
            .Price("FI", 100m, Currency.Usd)
            .Position("P1", "H1-A", "EQ", 9m, AssetClass.Equity, Currency.Usd)      // 900 (overweight)
            .Position("P2", "H1-A", "FI", 1m, AssetClass.FixedIncome, Currency.Usd) // 100
            .Build();

        var exposure = Analyze(snapshot);
        decimal net = exposure.ProposedTrades.Sum(t =>
            t.Direction == TradeDirection.Buy ? t.Notional.Amount : -t.Notional.Amount);
        decimal tolerance = FinancialMath.NetNotionalTolerance * exposure.TotalValue.Amount;
        Assert.True(Math.Abs(net) <= tolerance, $"net notional {net} exceeded tolerance {tolerance}");
    }
}
