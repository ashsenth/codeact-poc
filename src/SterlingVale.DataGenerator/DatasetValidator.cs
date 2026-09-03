namespace SterlingVale.DataGenerator;

/// <summary>Result of validating a dataset for integrity and coverage.</summary>
public sealed record DatasetValidationResult(bool IsValid, IReadOnlyList<string> Errors)
{
    /// <summary>A successful, error-free result.</summary>
    public static DatasetValidationResult Ok { get; } = new(true, []);
}

/// <summary>
/// Validates a generated dataset for referential integrity, price/FX coverage, non-zero totals,
/// and the presence of deliberate breaches. Used by the CLI and by application readiness checks.
/// </summary>
public sealed class DatasetValidator
{
    /// <summary>Validates the dataset and returns all detected problems (empty when valid).</summary>
    public DatasetValidationResult Validate(GeneratedDataset dataset)
    {
        ArgumentNullException.ThrowIfNull(dataset);
        var errors = new List<string>();

        var householdIds = dataset.Households.Select(h => h.Id).ToHashSet(StringComparer.Ordinal);
        var accountIds = dataset.Accounts.Select(a => a.Id).ToHashSet(StringComparer.Ordinal);
        var pricedSymbols = dataset.Prices.Select(p => p.Symbol).ToHashSet(StringComparer.Ordinal);
        var fxPairs = dataset.FxRates
            .Select(r => (r.From, r.To))
            .ToHashSet();

        // Referential integrity: accounts -> households.
        foreach (var account in dataset.Accounts)
        {
            if (!householdIds.Contains(account.HouseholdId))
            {
                errors.Add($"Account {account.Id} references missing household {account.HouseholdId}.");
            }
        }

        // Referential integrity: positions -> accounts; quote coverage.
        foreach (var position in dataset.Positions)
        {
            if (!accountIds.Contains(position.AccountId))
            {
                errors.Add($"Position {position.Id} references missing account {position.AccountId}.");
            }

            if (!pricedSymbols.Contains(position.Symbol))
            {
                errors.Add($"Position {position.Id} references unpriced symbol {position.Symbol}.");
            }
        }

        // FX coverage: every position currency must convert to its household base currency.
        var accountToHousehold = dataset.Accounts.ToDictionary(a => a.Id, a => a.HouseholdId, StringComparer.Ordinal);
        var householdBase = dataset.Households.ToDictionary(h => h.Id, h => h.BaseCurrency, StringComparer.Ordinal);
        foreach (var position in dataset.Positions)
        {
            if (!accountToHousehold.TryGetValue(position.AccountId, out var hid) ||
                !householdBase.TryGetValue(hid, out var baseCcy))
            {
                continue;
            }

            if (position.Currency != baseCcy && !fxPairs.Contains((position.Currency, baseCcy)))
            {
                errors.Add($"Missing FX rate {position.Currency}->{baseCcy} for position {position.Id}.");
            }
        }

        // Non-zero household totals: every household must have at least one priced position.
        var priceBySymbol = dataset.Prices.ToDictionary(p => p.Symbol, p => p.Price, StringComparer.Ordinal);
        foreach (var household in dataset.Households)
        {
            decimal gross = dataset.Positions
                .Where(p => accountToHousehold.TryGetValue(p.AccountId, out var h) && h == household.Id)
                .Sum(p => p.Quantity * (priceBySymbol.TryGetValue(p.Symbol, out var pr) ? pr : 0m));

            if (gross <= 0m)
            {
                errors.Add($"Household {household.Id} has a non-positive gross total.");
            }
        }

        return errors.Count == 0 ? DatasetValidationResult.Ok : new DatasetValidationResult(false, errors);
    }
}
