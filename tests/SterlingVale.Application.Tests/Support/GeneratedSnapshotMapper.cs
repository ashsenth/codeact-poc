using SterlingVale.Application.Portfolio;
using SterlingVale.DataGenerator;
using SterlingVale.Domain.Model;
using SterlingVale.Domain.ValueObjects;

namespace SterlingVale.Application.Tests.Support;

/// <summary>
/// Maps a generator <see cref="GeneratedDataset"/> into a domain <see cref="PortfolioSnapshot"/>
/// for invariant tests over realistic data. (Production loading lives in Infrastructure later.)
/// </summary>
public static class GeneratedSnapshotMapper
{
    private static readonly DateTimeOffset AsOf = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public static PortfolioSnapshot ToSnapshot(GeneratedDataset dataset)
    {
        var households = dataset.Households
            .Select(h => new Household(HouseholdId.Create(h.Id), h.Name, Currency.Parse(h.BaseCurrency)))
            .ToList();

        var policies = dataset.Households.Select(h => new HouseholdPolicy(
            HouseholdId.Create(h.Id),
            h.Policy.Targets
                .Select(t => new AllocationTarget(AssetClasses.Parse(t.AssetClass), Percentage.FromFraction(t.TargetFraction)))
                .ToList(),
            Percentage.FromFraction(h.Policy.DriftToleranceFraction),
            Percentage.FromFraction(h.Policy.MaxConcentrationFraction),
            Percentage.FromFraction(h.Policy.MaxFxExposureFraction),
            Percentage.FromFraction(h.Policy.MaxCryptoExposureFraction))).ToList();

        var accounts = dataset.Accounts
            .Select(a => new Account(
                AccountId.Create(a.Id),
                HouseholdId.Create(a.HouseholdId),
                CustodianId.Create(a.CustodianId),
                a.Name,
                Currency.Parse(a.Currency)))
            .ToList();

        var positions = dataset.Positions
            .Select(p => new Position(
                PositionId.Create(p.Id),
                AccountId.Create(p.AccountId),
                Symbol.Create(p.Symbol),
                p.Quantity,
                AssetClasses.Parse(p.AssetClass),
                Currency.Parse(p.Currency)))
            .ToList();

        var prices = dataset.Prices
            .Select(p => new PriceQuote(Symbol.Create(p.Symbol), p.Price, Currency.Parse(p.Currency), AsOf))
            .ToList();

        var fx = dataset.FxRates
            .Select(r => new FxRate(Currency.Parse(r.From), Currency.Parse(r.To), r.Rate, AsOf))
            .ToList();

        return new PortfolioSnapshot(households, accounts, positions, policies, prices, fx);
    }
}
