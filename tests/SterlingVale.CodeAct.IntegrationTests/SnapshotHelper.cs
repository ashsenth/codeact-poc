using SterlingVale.Application.Portfolio;
using SterlingVale.Domain.Model;
using SterlingVale.Domain.ValueObjects;

namespace SterlingVale.CodeAct.IntegrationTests;

/// <summary>Builds a minimal snapshot for provider-construction tests.</summary>
internal static class SnapshotHelper
{
    private static readonly DateTimeOffset AsOf = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public static PortfolioSnapshot Build()
    {
        var hid = HouseholdId.Create("H0001");
        var households = new List<Household> { new(hid, "Household H0001", Currency.Usd) };
        var policies = new List<HouseholdPolicy>
        {
            new(
                hid,
                [new AllocationTarget(AssetClass.Equity, Percentage.FromFraction(1.0m))],
                Percentage.FromFraction(0.05m),
                Percentage.FromFraction(0.60m),
                Percentage.FromFraction(0.30m),
                Percentage.FromFraction(0.10m)),
        };
        var accounts = new List<Account>
        {
            new(AccountId.Create("H0001-A01"), hid, CustodianId.Create("CUST01"), "Account 01", Currency.Usd),
        };
        var positions = new List<Position>
        {
            new(PositionId.Create("P1"), AccountId.Create("H0001-A01"), Symbol.Create("EQ"), 10m, AssetClass.Equity, Currency.Usd),
        };
        var prices = new List<PriceQuote> { new(Symbol.Create("EQ"), 100m, Currency.Usd, AsOf) };
        var fx = new List<FxRate> { new(Currency.Eur, Currency.Usd, 1.1m, AsOf) };
        return new PortfolioSnapshot(households, accounts, positions, policies, prices, fx);
    }
}
