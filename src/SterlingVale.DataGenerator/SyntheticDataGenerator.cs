using SterlingVale.Domain.ValueObjects;

namespace SterlingVale.DataGenerator;

/// <summary>
/// Produces a deterministic synthetic dataset from a <see cref="DatasetProfile"/>. Guarantees:
/// referential integrity, full price coverage, full FX coverage, non-zero household totals,
/// deliberate policy breaches, and reproducibility from the seed.
/// </summary>
public sealed class SyntheticDataGenerator
{
    /// <summary>Fixed as-of timestamp for all market data so output is byte-stable.</summary>
    private const string AsOf = "2026-01-01T00:00:00+00:00";

    // Value of one unit of each currency expressed in USD. Cross rates are derived from these,
    // guaranteeing internally consistent, round-trippable FX.
    private static readonly IReadOnlyDictionary<string, decimal> UsdValue =
        new Dictionary<string, decimal>(StringComparer.Ordinal)
        {
            ["USD"] = 1.000000m,
            ["EUR"] = 1.080000m,
            ["GBP"] = 1.270000m,
            ["JPY"] = 0.006700m,
        };

    private sealed record UniverseEntry(string Symbol, AssetClass AssetClass, string Currency, decimal Price);

    private static readonly IReadOnlyList<UniverseEntry> Universe =
    [
        new("EQ_US_LARGE", AssetClass.Equity, "USD", 187.42m),
        new("EQ_US_MID", AssetClass.Equity, "USD", 94.15m),
        new("EQ_EU_CORE", AssetClass.Equity, "EUR", 132.80m),
        new("EQ_UK_CORE", AssetClass.Equity, "GBP", 78.55m),
        new("EQ_JP_CORE", AssetClass.Equity, "JPY", 2450m),
        new("FI_US_AGG", AssetClass.FixedIncome, "USD", 98.30m),
        new("FI_EU_GOV", AssetClass.FixedIncome, "EUR", 101.20m),
        new("FI_UK_GILT", AssetClass.FixedIncome, "GBP", 95.60m),
        new("FI_JP_GOV", AssetClass.FixedIncome, "JPY", 100.00m),
        new("CASH_USD", AssetClass.Cash, "USD", 1m),
        new("CASH_EUR", AssetClass.Cash, "EUR", 1m),
        new("CASH_GBP", AssetClass.Cash, "GBP", 1m),
        new("CASH_JPY", AssetClass.Cash, "JPY", 1m),
        new("ALT_REIT", AssetClass.Alternative, "USD", 55.25m),
        new("ALT_INFRA", AssetClass.Alternative, "EUR", 42.10m),
        new("ALT_CMDTY", AssetClass.Alternative, "USD", 63.90m),
        new("CRY_BTC", AssetClass.Crypto, "USD", 61234.50m),
        new("CRY_ETH", AssetClass.Crypto, "USD", 3421.75m),
    ];

    /// <summary>Generates the complete dataset for the given profile.</summary>
    public GeneratedDataset Generate(DatasetProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var rng = new DeterministicRandom(profile.Seed);

        var households = new List<HouseholdDto>();
        var accounts = new List<AccountDto>();
        var positions = new List<PositionDto>();

        for (int i = 1; i <= profile.HouseholdCount; i++)
        {
            string hid = $"H{i:D4}";
            string baseCcy = PickBaseCurrency(i);
            households.Add(new HouseholdDto(hid, $"Household {i:D4}", baseCcy, StandardPolicy()));

            int accountCount = rng.NextInt(profile.MinAccountsPerHousehold, profile.MaxAccountsPerHousehold + 1);
            for (int a = 1; a <= accountCount; a++)
            {
                string aid = $"{hid}-A{a:D2}";
                string custodian = $"CUST{((i + a) % 4) + 1:D2}";
                string acctCcy = a == 1 ? baseCcy : PickAccountCurrency(rng, baseCcy);
                accounts.Add(new AccountDto(aid, hid, custodian, $"Account {a:D2}", acctCcy));

                int positionCount = rng.NextInt(profile.MinPositionsPerAccount, profile.MaxPositionsPerAccount + 1);
                for (int p = 1; p <= positionCount; p++)
                {
                    positions.Add(GeneratePosition(rng, aid, p));
                }
            }

            InjectDeliberateBreach(i, hid, positions);
        }

        return new GeneratedDataset(profile, households, accounts, positions, Prices(), FxRates());
    }

    private static string PickBaseCurrency(int index) =>
        index % 11 == 0 ? "JPY" :
        index % 7 == 0 ? "GBP" :
        index % 5 == 0 ? "EUR" :
        "USD";

    private static string PickAccountCurrency(DeterministicRandom rng, string baseCcy) =>
        rng.Chance(0.30) ? rng.Pick(["USD", "EUR", "GBP", "JPY"]) : baseCcy;

    private static PolicyDto StandardPolicy() => new(
        DriftToleranceFraction: 0.05m,
        MaxConcentrationFraction: 0.60m,
        MaxFxExposureFraction: 0.30m,
        MaxCryptoExposureFraction: 0.10m,
        Targets:
        [
            new("Equity", 0.50m),
            new("FixedIncome", 0.30m),
            new("Cash", 0.10m),
            new("Alternative", 0.07m),
            new("Crypto", 0.03m),
        ]);

    private static PositionDto GeneratePosition(DeterministicRandom rng, string accountId, int index)
    {
        // Weighted asset-class selection roughly aligned with target allocations.
        double roll = rng.NextDouble();
        AssetClass cls =
            roll < 0.48 ? AssetClass.Equity :
            roll < 0.78 ? AssetClass.FixedIncome :
            roll < 0.88 ? AssetClass.Cash :
            roll < 0.96 ? AssetClass.Alternative :
            AssetClass.Crypto;

        var candidates = Universe.Where(u => u.AssetClass == cls).ToList();
        var entry = rng.Pick(candidates);
        decimal quantity = QuantityFor(rng, cls);
        string pid = $"{accountId}-P{index:D2}";
        return new PositionDto(pid, accountId, entry.Symbol, quantity, entry.AssetClass.ToString(), entry.Currency);
    }

    private static decimal QuantityFor(DeterministicRandom rng, AssetClass cls) => cls switch
    {
        AssetClass.Cash => rng.NextDecimal(1000m, 50000m, 2),
        AssetClass.Crypto => rng.NextDecimal(0.05m, 3.0m, 4),
        _ => rng.NextDecimal(10m, 500m, 2),
    };

    // Deterministically make every third household breach a limit, rotating the breach kind so all
    // kinds are represented in the dataset.
    private static void InjectDeliberateBreach(int index, string householdId, List<PositionDto> positions)
    {
        if (index % 3 != 0)
        {
            return;
        }

        string firstAccount = $"{householdId}-A01";
        int breachType = (index / 3) % 3;
        var (symbol, cls, ccy, qty) = breachType switch
        {
            0 => ("CRY_BTC", AssetClass.Crypto, "USD", 40m),      // crypto exposure breach
            1 => ("EQ_US_LARGE", AssetClass.Equity, "USD", 6000m), // concentration breach
            _ => ("EQ_EU_CORE", AssetClass.Equity, "EUR", 5000m),  // FX exposure breach
        };

        positions.Add(new PositionDto(
            $"{firstAccount}-BREACH", firstAccount, symbol, qty, cls.ToString(), ccy));
    }

    private static IReadOnlyList<PriceDto> Prices() =>
        Universe.Select(u => new PriceDto(u.Symbol, u.Price, u.Currency, AsOf)).ToList();

    private static IReadOnlyList<FxRateDto> FxRates()
    {
        var codes = new[] { "USD", "EUR", "GBP", "JPY" };
        var rates = new List<FxRateDto>();
        foreach (var from in codes)
        {
            foreach (var to in codes)
            {
                if (from == to)
                {
                    continue;
                }

                decimal rate = Math.Round(UsdValue[from] / UsdValue[to], 6, MidpointRounding.ToEven);
                rates.Add(new FxRateDto(from, to, rate, AsOf));
            }
        }

        return rates;
    }
}
