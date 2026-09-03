namespace SterlingVale.DataGenerator;

// Plain, serialization-friendly DTOs for the generated data files. Kept separate from the pure
// domain records so the on-disk format is explicit and stable.

/// <summary>A target allocation entry within a policy.</summary>
public sealed record AllocationTargetDto(string AssetClass, decimal TargetFraction);

/// <summary>A household's investment policy embedded in the household record.</summary>
public sealed record PolicyDto(
    decimal DriftToleranceFraction,
    decimal MaxConcentrationFraction,
    decimal MaxFxExposureFraction,
    decimal MaxCryptoExposureFraction,
    IReadOnlyList<AllocationTargetDto> Targets);

/// <summary>A household record in households.json.</summary>
public sealed record HouseholdDto(
    string Id,
    string Name,
    string BaseCurrency,
    PolicyDto Policy);

/// <summary>An account record in accounts.json.</summary>
public sealed record AccountDto(
    string Id,
    string HouseholdId,
    string CustodianId,
    string Name,
    string Currency);

/// <summary>A position record in positions.json.</summary>
public sealed record PositionDto(
    string Id,
    string AccountId,
    string Symbol,
    decimal Quantity,
    string AssetClass,
    string Currency);

/// <summary>A price record in prices.json.</summary>
public sealed record PriceDto(
    string Symbol,
    decimal Price,
    string Currency,
    string AsOf);

/// <summary>An FX-rate record in fx.json (1 unit of From = Rate units of To).</summary>
public sealed record FxRateDto(
    string From,
    string To,
    decimal Rate,
    string AsOf);

/// <summary>Per-file integrity entry in the manifest.</summary>
public sealed record FileHashDto(string File, int RecordCount, string Sha256);

/// <summary>manifest.json: seed, version, counts, and per-file SHA-256 hashes.</summary>
public sealed record ManifestDto(
    string Profile,
    ulong Seed,
    string GeneratorVersion,
    string GeneratedAt,
    IReadOnlyList<FileHashDto> Files);

/// <summary>The complete in-memory dataset produced by the generator.</summary>
public sealed record GeneratedDataset(
    DatasetProfile Profile,
    IReadOnlyList<HouseholdDto> Households,
    IReadOnlyList<AccountDto> Accounts,
    IReadOnlyList<PositionDto> Positions,
    IReadOnlyList<PriceDto> Prices,
    IReadOnlyList<FxRateDto> FxRates);
