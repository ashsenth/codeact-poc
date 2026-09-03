namespace SterlingVale.Tools;

// Bounded, JSON-serializable views returned by the tools. Views expose only retrieval/classification
// data — never valuations, allocations, or any computed analysis.

/// <summary>Summary of a household.</summary>
public sealed record HouseholdView(string Id, string Name, string BaseCurrency);

/// <summary>A single target allocation within a policy.</summary>
public sealed record AllocationTargetView(string AssetClass, decimal TargetFraction);

/// <summary>A household's investment policy.</summary>
public sealed record PolicyView(
    string HouseholdId,
    decimal DriftToleranceFraction,
    decimal MaxConcentrationFraction,
    decimal MaxFxExposureFraction,
    decimal MaxCryptoExposureFraction,
    IReadOnlyList<AllocationTargetView> Targets);

/// <summary>Summary of an account.</summary>
public sealed record AccountView(
    string Id,
    string HouseholdId,
    string CustodianId,
    string Name,
    string Currency);

/// <summary>Summary of a position.</summary>
public sealed record PositionView(
    string Id,
    string AccountId,
    string Symbol,
    decimal Quantity,
    string AssetClass,
    string Currency);

/// <summary>A price quote.</summary>
public sealed record PriceView(string Symbol, decimal Price, string Currency, string AsOf);

/// <summary>An FX rate (direct or inverse-derived).</summary>
public sealed record FxView(string From, string To, decimal Rate);

/// <summary>The asset-class classification of a symbol.</summary>
public sealed record AssetClassView(string Symbol, string AssetClass);
