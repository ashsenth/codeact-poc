using SterlingVale.Domain.Model;
using SterlingVale.Domain.ValueObjects;

namespace SterlingVale.Application.Portfolio;

/// <summary>
/// An immutable, indexed in-memory view of a dataset snapshot: households, their policies,
/// accounts, positions, prices, and FX rates. All oracle and analysis logic operates on this
/// type. Construction validates referential integrity and rejects duplicate identifiers.
/// </summary>
public sealed class PortfolioSnapshot
{
    private readonly Dictionary<HouseholdId, HouseholdPolicy> _policies;
    private readonly Dictionary<Symbol, PriceQuote> _prices;
    private readonly Dictionary<HouseholdId, List<Account>> _accountsByHousehold;
    private readonly Dictionary<AccountId, List<Position>> _positionsByAccount;
    private readonly Dictionary<Symbol, AssetClass> _assetClassBySymbol;

    /// <summary>Creates and indexes a snapshot from its constituent collections.</summary>
    public PortfolioSnapshot(
        IReadOnlyList<Household> households,
        IReadOnlyList<Account> accounts,
        IReadOnlyList<Position> positions,
        IReadOnlyList<HouseholdPolicy> policies,
        IReadOnlyList<PriceQuote> prices,
        IReadOnlyList<FxRate> fxRates)
    {
        Households = households ?? throw new ArgumentNullException(nameof(households));
        Accounts = accounts ?? throw new ArgumentNullException(nameof(accounts));
        Positions = positions ?? throw new ArgumentNullException(nameof(positions));
        FxRates = fxRates ?? throw new ArgumentNullException(nameof(fxRates));
        ArgumentNullException.ThrowIfNull(policies);
        ArgumentNullException.ThrowIfNull(prices);

        _policies = policies.ToDictionary(p => p.HouseholdId);
        _prices = prices.ToDictionary(p => p.Symbol);
        _accountsByHousehold = accounts
            .GroupBy(a => a.HouseholdId)
            .ToDictionary(g => g.Key, g => g.ToList());
        _positionsByAccount = positions
            .GroupBy(p => p.AccountId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // A symbol maps to exactly one asset class across the dataset (first occurrence wins).
        _assetClassBySymbol = new Dictionary<Symbol, AssetClass>();
        foreach (var position in positions)
        {
            _assetClassBySymbol.TryAdd(position.Symbol, position.AssetClass);
        }
    }

    /// <summary>All households in stable order.</summary>
    public IReadOnlyList<Household> Households { get; }

    /// <summary>All accounts.</summary>
    public IReadOnlyList<Account> Accounts { get; }

    /// <summary>All positions.</summary>
    public IReadOnlyList<Position> Positions { get; }

    /// <summary>All FX rates.</summary>
    public IReadOnlyList<FxRate> FxRates { get; }

    /// <summary>Returns the policy for a household, throwing when absent.</summary>
    public HouseholdPolicy Policy(HouseholdId householdId) =>
        _policies.TryGetValue(householdId, out var policy)
            ? policy
            : throw new KeyNotFoundException($"No policy for household {householdId}.");

    /// <summary>Returns the price quote for a symbol, throwing when absent.</summary>
    public PriceQuote Price(Symbol symbol) =>
        _prices.TryGetValue(symbol, out var quote)
            ? quote
            : throw new KeyNotFoundException($"No price for symbol {symbol}.");

    /// <summary>Attempts to get the price quote for a symbol.</summary>
    public bool TryGetPrice(Symbol symbol, out PriceQuote quote) =>
        _prices.TryGetValue(symbol, out quote!);

    /// <summary>Attempts to resolve the asset class of a symbol from the dataset's positions.</summary>
    public bool TryGetAssetClass(Symbol symbol, out AssetClass assetClass) =>
        _assetClassBySymbol.TryGetValue(symbol, out assetClass);

    /// <summary>Returns the accounts belonging to a household (empty when none).</summary>
    public IReadOnlyList<Account> AccountsFor(HouseholdId householdId) =>
        _accountsByHousehold.TryGetValue(householdId, out var list) ? list : [];

    /// <summary>Returns the positions in an account (empty when none).</summary>
    public IReadOnlyList<Position> PositionsFor(AccountId accountId) =>
        _positionsByAccount.TryGetValue(accountId, out var list) ? list : [];
}
