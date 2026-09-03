using System.ComponentModel;
using SterlingVale.Application.Portfolio;
using SterlingVale.Application.Pricing;
using SterlingVale.Domain.ValueObjects;
using SterlingVale.Telemetry;

namespace SterlingVale.Tools;

/// <summary>
/// Implements the seven granular tools over a single <see cref="PortfolioSnapshot"/>. Each tool only
/// retrieves or classifies data — it never values, aggregates, or evaluates policy. The service is
/// immutable after construction and safe for concurrent use. Every method validates arguments,
/// honors cancellation, returns bounded deterministic JSON views, throws typed errors, and is
/// instrumented via <see cref="ToolMetrics"/>.
/// </summary>
public sealed class PortfolioToolService
{
    private readonly PortfolioSnapshot _snapshot;
    private readonly FxConverter _fx;
    private readonly ToolMetrics _metrics;

    /// <summary>Creates the service over a snapshot, with optional shared tool metrics.</summary>
    public PortfolioToolService(PortfolioSnapshot snapshot, ToolMetrics? metrics = null)
    {
        _snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        _fx = new FxConverter(snapshot.FxRates);
        _metrics = metrics ?? new ToolMetrics();
    }

    [Description("Lists every household in the dataset with its identifier, name, and base currency.")]
    public IReadOnlyList<HouseholdView> ListHouseholds(CancellationToken cancellationToken = default)
    {
        using var scope = _metrics.Measure(ToolNames.ListHouseholds);
        cancellationToken.ThrowIfCancellationRequested();
        return _snapshot.Households
            .Select(h => new HouseholdView(h.Id.Value, h.Name, h.BaseCurrency.Code))
            .ToList();
    }

    [Description("Gets the investment policy (target allocations and risk limits) for a household.")]
    public PolicyView GetPolicy(
        [Description("The household identifier, e.g. 'H0001'.")] string householdId,
        CancellationToken cancellationToken = default)
    {
        using var scope = _metrics.Measure(ToolNames.GetPolicy);
        cancellationToken.ThrowIfCancellationRequested();
        var id = RequireHousehold(ToolNames.GetPolicy, householdId);

        var policy = _snapshot.Policy(id);
        var targets = policy.Targets
            .Select(t => new AllocationTargetView(t.AssetClass.ToString(), t.Target.Fraction))
            .ToList();
        return new PolicyView(
            policy.HouseholdId.Value,
            policy.DriftTolerance.Fraction,
            policy.MaxConcentration.Fraction,
            policy.MaxFxExposure.Fraction,
            policy.MaxCryptoExposure.Fraction,
            targets);
    }

    [Description("Lists the accounts belonging to a household.")]
    public IReadOnlyList<AccountView> ListAccounts(
        [Description("The household identifier, e.g. 'H0001'.")] string householdId,
        CancellationToken cancellationToken = default)
    {
        using var scope = _metrics.Measure(ToolNames.ListAccounts);
        cancellationToken.ThrowIfCancellationRequested();
        var id = RequireHousehold(ToolNames.ListAccounts, householdId);
        return _snapshot.AccountsFor(id)
            .Select(a => new AccountView(a.Id.Value, a.HouseholdId.Value, a.CustodianId.Value, a.Name, a.Currency.Code))
            .ToList();
    }

    [Description("Lists the positions held in an account.")]
    public IReadOnlyList<PositionView> ListPositions(
        [Description("The account identifier, e.g. 'H0001-A01'.")] string accountId,
        CancellationToken cancellationToken = default)
    {
        using var scope = _metrics.Measure(ToolNames.ListPositions);
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(accountId))
        {
            throw new ToolArgumentException(ToolNames.ListPositions, "accountId is required.");
        }

        var id = AccountId.Create(accountId);
        if (!_snapshot.Accounts.Any(a => a.Id == id))
        {
            throw new ToolNotFoundException(ToolNames.ListPositions, $"Account '{accountId}' not found.");
        }

        return _snapshot.PositionsFor(id)
            .Select(p => new PositionView(p.Id.Value, p.AccountId.Value, p.Symbol.Value, p.Quantity, p.AssetClass.ToString(), p.Currency.Code))
            .ToList();
    }

    [Description("Gets the current price quote for a symbol.")]
    public PriceView GetPrice(
        [Description("The instrument symbol, e.g. 'EQ_US_LARGE'.")] string symbol,
        CancellationToken cancellationToken = default)
    {
        using var scope = _metrics.Measure(ToolNames.GetPrice);
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(symbol))
        {
            throw new ToolArgumentException(ToolNames.GetPrice, "symbol is required.");
        }

        var sym = Symbol.Create(symbol);
        if (!_snapshot.TryGetPrice(sym, out var quote))
        {
            throw new ToolNotFoundException(ToolNames.GetPrice, $"No price for symbol '{symbol}'.");
        }

        return new PriceView(quote.Symbol.Value, quote.Price, quote.Currency.Code, quote.AsOf.ToString("O"));
    }

    [Description("Gets the FX rate to convert one unit of a source currency into a target currency.")]
    public FxView GetFx(
        [Description("The source currency code (USD, EUR, GBP, or JPY).")] string fromCurrency,
        [Description("The target currency code (USD, EUR, GBP, or JPY).")] string toCurrency,
        CancellationToken cancellationToken = default)
    {
        using var scope = _metrics.Measure(ToolNames.GetFx);
        cancellationToken.ThrowIfCancellationRequested();
        var from = RequireCurrency(ToolNames.GetFx, fromCurrency);
        var to = RequireCurrency(ToolNames.GetFx, toCurrency);

        try
        {
            return new FxView(from.Code, to.Code, _fx.Rate(from, to));
        }
        catch (FxRateNotFoundException ex)
        {
            throw new ToolNotFoundException(ToolNames.GetFx, ex.Message);
        }
    }

    [Description("Classifies a symbol into its asset class (Equity, FixedIncome, Cash, Alternative, or Crypto).")]
    public AssetClassView AssetClass(
        [Description("The instrument symbol, e.g. 'CRY_BTC'.")] string symbol,
        CancellationToken cancellationToken = default)
    {
        using var scope = _metrics.Measure(ToolNames.AssetClass);
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(symbol))
        {
            throw new ToolArgumentException(ToolNames.AssetClass, "symbol is required.");
        }

        var sym = Symbol.Create(symbol);
        if (!_snapshot.TryGetAssetClass(sym, out var assetClass))
        {
            throw new ToolNotFoundException(ToolNames.AssetClass, $"Unknown symbol '{symbol}'.");
        }

        return new AssetClassView(sym.Value, assetClass.ToString());
    }

    private HouseholdId RequireHousehold(string toolName, string householdId)
    {
        if (string.IsNullOrWhiteSpace(householdId))
        {
            throw new ToolArgumentException(toolName, "householdId is required.");
        }

        var id = HouseholdId.Create(householdId);
        if (!_snapshot.Households.Any(h => h.Id == id))
        {
            throw new ToolNotFoundException(toolName, $"Household '{householdId}' not found.");
        }

        return id;
    }

    private static Currency RequireCurrency(string toolName, string code)
    {
        if (!Currency.TryParse(code, out var currency))
        {
            throw new ToolArgumentException(toolName, $"Unsupported currency '{code}'.");
        }

        return currency;
    }
}
