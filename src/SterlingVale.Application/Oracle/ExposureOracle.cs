using SterlingVale.Application.Portfolio;
using SterlingVale.Application.Pricing;
using SterlingVale.Domain;
using SterlingVale.Domain.Analysis;
using SterlingVale.Domain.Model;
using SterlingVale.Domain.ValueObjects;

namespace SterlingVale.Application.Oracle;

/// <summary>
/// The deterministic correctness oracle. It reproduces the exposure/rebalancing analysis using
/// exact decimal arithmetic and the documented rules, producing the SAME
/// <see cref="ExposureReport"/> shape that the agents must emit. This type is used exclusively for
/// validation, scoring, and tests — it is NEVER registered as an agent tool.
///
/// <para><b>Rules (see docs/metrics.md):</b>
/// native value = quantity × price; base value = native × FX(position→base);
/// allocation = asset-class base value / household total;
/// drift = actual − target, breached when |drift| &gt; drift tolerance;
/// concentration = symbol base value / total; FX exposure = non-base value / total;
/// crypto exposure = crypto value / total; proposed notional = target value − current value.</para>
///
/// <para><b>Precision:</b> base values are accumulated as raw <see cref="decimal"/> values with no
/// intermediate rounding; ratios are rounded to <see cref="FinancialMath.RatioDecimals"/> places;
/// money outputs are rounded to their currency's minor units with banker's rounding.</para>
///
/// <para><b>Zero-value behavior:</b> when a household total is zero, all allocations are zero
/// (via <see cref="FinancialMath.SafeDivide"/>) and no proposed trades are produced.</para>
/// </summary>
public sealed class ExposureOracle
{
    /// <summary>Analyzes the requested households (or all) and returns the exposure report.</summary>
    public ExposureReport Analyze(
        PortfolioSnapshot snapshot,
        AnalysisRequest request,
        string runId,
        DateTimeOffset generatedAt)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(request);

        var fx = new FxConverter(snapshot.FxRates);
        var selected = SelectHouseholds(snapshot, request);
        var exposures = selected.Select(h => AnalyzeHousehold(snapshot, fx, h)).ToList();
        return new ExposureReport(runId, generatedAt, exposures);
    }

    /// <summary>Analyzes a single household. Public to allow targeted testing.</summary>
    public HouseholdExposure AnalyzeHousehold(PortfolioSnapshot snapshot, FxConverter fx, Household household)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(fx);
        ArgumentNullException.ThrowIfNull(household);

        var policy = snapshot.Policy(household.Id);
        var baseCcy = household.BaseCurrency;

        var classValue = NewClassMap();
        var symbolValue = new Dictionary<Symbol, decimal>();
        decimal total = 0m;
        decimal nonBaseValue = 0m;

        foreach (var account in snapshot.AccountsFor(household.Id))
        {
            foreach (var position in snapshot.PositionsFor(account.Id))
            {
                var quote = snapshot.Price(position.Symbol);
                decimal native = Valuation.NativeValue(position.Quantity, quote.Price);
                decimal baseValue = fx.ConvertRaw(native, quote.Currency, baseCcy);

                total += baseValue;
                classValue[position.AssetClass] += baseValue;
                symbolValue[position.Symbol] = symbolValue.GetValueOrDefault(position.Symbol) + baseValue;
                if (quote.Currency != baseCcy)
                {
                    nonBaseValue += baseValue;
                }
            }
        }

        var drifts = ComputeDrifts(policy, classValue, total);
        var breaches = ComputeBreaches(policy, classValue, symbolValue, total, nonBaseValue, drifts);
        var trades = ComputeProposedTrades(policy, classValue, total, baseCcy);
        bool flagged = breaches.Count > 0;

        return new HouseholdExposure(
            household.Id,
            new Money(total, baseCcy),
            drifts,
            breaches,
            trades,
            flagged);
    }

    private static IReadOnlyList<Household> SelectHouseholds(PortfolioSnapshot snapshot, AnalysisRequest request)
    {
        if (request.HouseholdIds is not { Count: > 0 })
        {
            return snapshot.Households;
        }

        var wanted = request.HouseholdIds.ToHashSet(StringComparer.Ordinal);
        return snapshot.Households.Where(h => wanted.Contains(h.Id.Value)).ToList();
    }

    private static Dictionary<AssetClass, decimal> NewClassMap()
    {
        var map = new Dictionary<AssetClass, decimal>();
        foreach (var cls in AssetClasses.All)
        {
            map[cls] = 0m;
        }

        return map;
    }

    private static IReadOnlyList<AllocationDrift> ComputeDrifts(
        HouseholdPolicy policy, IReadOnlyDictionary<AssetClass, decimal> classValue, decimal total)
    {
        var targetByClass = policy.Targets.ToDictionary(t => t.AssetClass, t => t.Target.Fraction);
        decimal tolerance = policy.DriftTolerance.Fraction;

        var drifts = new List<AllocationDrift>(AssetClasses.All.Count);
        foreach (var cls in AssetClasses.All)
        {
            decimal actual = FinancialMath.RoundRatio(FinancialMath.SafeDivide(classValue[cls], total));
            decimal target = targetByClass.GetValueOrDefault(cls);
            decimal drift = FinancialMath.RoundRatio(actual - target);
            bool breached = Math.Abs(drift) > tolerance;
            drifts.Add(new AllocationDrift(
                cls,
                Percentage.FromFraction(actual),
                Percentage.FromFraction(target),
                Percentage.FromFraction(drift),
                breached));
        }

        return drifts;
    }

    private static IReadOnlyList<RiskBreach> ComputeBreaches(
        HouseholdPolicy policy,
        IReadOnlyDictionary<AssetClass, decimal> classValue,
        IReadOnlyDictionary<Symbol, decimal> symbolValue,
        decimal total,
        decimal nonBaseValue,
        IReadOnlyList<AllocationDrift> drifts)
    {
        var breaches = new List<RiskBreach>();

        foreach (var drift in drifts.Where(d => d.Breached))
        {
            breaches.Add(new RiskBreach(
                RiskBreachKind.AllocationDrift,
                $"{drift.AssetClass} allocation drift exceeds tolerance.",
                Math.Abs(drift.Drift.Fraction),
                policy.DriftTolerance.Fraction));
        }

        // Concentration is per symbol (largest single-symbol share).
        foreach (var (symbol, value) in symbolValue.OrderBy(kv => kv.Key.Value, StringComparer.Ordinal))
        {
            decimal concentration = FinancialMath.RoundRatio(FinancialMath.SafeDivide(value, total));
            if (concentration > policy.MaxConcentration.Fraction)
            {
                breaches.Add(new RiskBreach(
                    RiskBreachKind.Concentration,
                    $"Symbol {symbol} concentration exceeds limit.",
                    concentration,
                    policy.MaxConcentration.Fraction));
            }
        }

        decimal fxExposure = FinancialMath.RoundRatio(FinancialMath.SafeDivide(nonBaseValue, total));
        if (fxExposure > policy.MaxFxExposure.Fraction)
        {
            breaches.Add(new RiskBreach(
                RiskBreachKind.FxExposure,
                "Non-base-currency exposure exceeds limit.",
                fxExposure,
                policy.MaxFxExposure.Fraction));
        }

        decimal cryptoExposure = FinancialMath.RoundRatio(FinancialMath.SafeDivide(classValue[AssetClass.Crypto], total));
        if (cryptoExposure > policy.MaxCryptoExposure.Fraction)
        {
            breaches.Add(new RiskBreach(
                RiskBreachKind.CryptoExposure,
                "Crypto exposure exceeds limit.",
                cryptoExposure,
                policy.MaxCryptoExposure.Fraction));
        }

        return breaches;
    }

    private static IReadOnlyList<ProposedTrade> ComputeProposedTrades(
        HouseholdPolicy policy,
        IReadOnlyDictionary<AssetClass, decimal> classValue,
        decimal total,
        Currency baseCcy)
    {
        if (total <= 0m)
        {
            return [];
        }

        var targetByClass = policy.Targets.ToDictionary(t => t.AssetClass, t => t.Target.Fraction);
        var trades = new List<ProposedTrade>();
        foreach (var cls in AssetClasses.All)
        {
            decimal targetValue = total * targetByClass.GetValueOrDefault(cls);
            decimal delta = targetValue - classValue[cls];
            var direction = delta >= 0m ? TradeDirection.Buy : TradeDirection.Sell;
            trades.Add(new ProposedTrade(cls, direction, new Money(Math.Abs(delta), baseCcy)));
        }

        return trades;
    }
}
