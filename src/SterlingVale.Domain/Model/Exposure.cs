using SterlingVale.Domain.ValueObjects;

namespace SterlingVale.Domain.Model;

/// <summary>Actual vs target allocation for a single asset class, with a breach flag.</summary>
public sealed record AllocationDrift(
    AssetClass AssetClass,
    Percentage Actual,
    Percentage Target,
    Percentage Drift,
    bool Breached);

/// <summary>The kind of a risk-limit breach.</summary>
public enum RiskBreachKind
{
    /// <summary>Single asset-class concentration exceeds the policy limit.</summary>
    Concentration = 0,

    /// <summary>Non-base-currency exposure exceeds the policy limit.</summary>
    FxExposure = 1,

    /// <summary>Crypto exposure exceeds the policy limit.</summary>
    CryptoExposure = 2,

    /// <summary>Allocation drift for an asset class exceeds the policy tolerance.</summary>
    AllocationDrift = 3,
}

/// <summary>A detected breach of a policy risk limit.</summary>
public sealed record RiskBreach(
    RiskBreachKind Kind,
    string Detail,
    decimal Observed,
    decimal Limit);

/// <summary>Direction of a proposed rebalancing trade.</summary>
public enum TradeDirection
{
    /// <summary>Increase exposure to the asset class.</summary>
    Buy = 0,

    /// <summary>Decrease exposure to the asset class.</summary>
    Sell = 1,
}

/// <summary>
/// A proposed rebalancing trade expressed strictly as an asset-class notional in the household's
/// base currency. Never references a named security (synthetic analysis, not investment advice).
/// </summary>
public sealed record ProposedTrade(
    AssetClass AssetClass,
    TradeDirection Direction,
    Money Notional);

/// <summary>The computed exposure and risk picture for a single household.</summary>
public sealed record HouseholdExposure(
    HouseholdId HouseholdId,
    Money TotalValue,
    IReadOnlyList<AllocationDrift> Drifts,
    IReadOnlyList<RiskBreach> Breaches,
    IReadOnlyList<ProposedTrade> ProposedTrades,
    bool Flagged);

/// <summary>
/// The top-level analysis output: per-household exposures plus the run identifier. This is the
/// schema-validated payload emitted by both Classic and CodeAct agents.
/// </summary>
public sealed record ExposureReport(
    string RunId,
    DateTimeOffset GeneratedAt,
    IReadOnlyList<HouseholdExposure> Households);
