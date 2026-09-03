using SterlingVale.Application.Portfolio;
using SterlingVale.Domain.Model;
using SterlingVale.Domain.ValueObjects;

namespace SterlingVale.Application.Tests.Support;

/// <summary>
/// Fluent builder for hand-crafted, fully deterministic portfolio snapshots used by oracle unit
/// tests. Keeps each test's inputs explicit and minimal.
/// </summary>
public sealed class PortfolioBuilder
{
    private static readonly DateTimeOffset AsOf = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly List<Household> _households = [];
    private readonly List<Account> _accounts = [];
    private readonly List<Position> _positions = [];
    private readonly List<HouseholdPolicy> _policies = [];
    private readonly Dictionary<string, PriceQuote> _prices = [];
    private readonly List<FxRate> _fx = [];

    public PortfolioBuilder Household(string id, Currency baseCurrency, HouseholdPolicy? policy = null)
    {
        var hid = HouseholdId.Create(id);
        _households.Add(new Household(hid, $"Household {id}", baseCurrency));
        _policies.Add(policy is null ? StandardPolicy(hid) : policy with { HouseholdId = hid });
        return this;
    }

    public PortfolioBuilder Account(string id, string householdId, Currency currency)
    {
        _accounts.Add(new Account(
            AccountId.Create(id),
            HouseholdId.Create(householdId),
            CustodianId.Create("CUST01"),
            $"Account {id}",
            currency));
        return this;
    }

    public PortfolioBuilder Position(string id, string accountId, string symbol, decimal quantity, AssetClass assetClass, Currency currency)
    {
        _positions.Add(new Position(
            PositionId.Create(id),
            AccountId.Create(accountId),
            Symbol.Create(symbol),
            quantity,
            assetClass,
            currency));
        return this;
    }

    public PortfolioBuilder Price(string symbol, decimal price, Currency currency)
    {
        _prices[symbol] = new PriceQuote(Symbol.Create(symbol), price, currency, AsOf);
        return this;
    }

    public PortfolioBuilder Fx(Currency from, Currency to, decimal rate)
    {
        _fx.Add(new FxRate(from, to, rate, AsOf));
        return this;
    }

    public PortfolioSnapshot Build() =>
        new(_households, _accounts, _positions, _policies, _prices.Values.ToList(), _fx);

    /// <summary>A standard 50/30/10/7/3 policy with 5% drift and 60/30/10% limits.</summary>
    public static HouseholdPolicy StandardPolicy(HouseholdId householdId) => new(
        householdId,
        [
            new AllocationTarget(AssetClass.Equity, Percentage.FromFraction(0.50m)),
            new AllocationTarget(AssetClass.FixedIncome, Percentage.FromFraction(0.30m)),
            new AllocationTarget(AssetClass.Cash, Percentage.FromFraction(0.10m)),
            new AllocationTarget(AssetClass.Alternative, Percentage.FromFraction(0.07m)),
            new AllocationTarget(AssetClass.Crypto, Percentage.FromFraction(0.03m)),
        ],
        DriftTolerance: Percentage.FromFraction(0.05m),
        MaxConcentration: Percentage.FromFraction(0.60m),
        MaxFxExposure: Percentage.FromFraction(0.30m),
        MaxCryptoExposure: Percentage.FromFraction(0.10m));
}
