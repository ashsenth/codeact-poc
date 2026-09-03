namespace SterlingVale.AgentShared.Contracts;

// Stable wire DTOs for the ExposureReport. These define the exact JSON contract both agents must
// emit and the API returns. Kept separate from the domain model (which uses value objects) so the
// serialized shape is explicit, versioned, and schema-checkable.

/// <summary>A single asset-class allocation drift entry.</summary>
public sealed record AllocationDriftDto(
    string AssetClass,
    decimal ActualFraction,
    decimal TargetFraction,
    decimal DriftFraction,
    bool Breached);

/// <summary>A detected risk-limit breach.</summary>
public sealed record RiskBreachDto(
    string Kind,
    string Detail,
    decimal Observed,
    decimal Limit);

/// <summary>A proposed asset-class rebalancing notional (never a named security).</summary>
public sealed record ProposedTradeDto(
    string AssetClass,
    string Direction,
    decimal Notional,
    string Currency);

/// <summary>The exposure and risk picture for one household.</summary>
public sealed record HouseholdExposureDto(
    string HouseholdId,
    string BaseCurrency,
    decimal TotalValue,
    bool Flagged,
    IReadOnlyList<AllocationDriftDto> Drifts,
    IReadOnlyList<RiskBreachDto> Breaches,
    IReadOnlyList<ProposedTradeDto> ProposedTrades);

/// <summary>The top-level analysis output emitted by both Classic and CodeAct agents.</summary>
public sealed record ExposureReportDto(
    string RunId,
    string GeneratedAt,
    IReadOnlyList<HouseholdExposureDto> Households);
