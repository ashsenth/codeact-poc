using SterlingVale.Domain.ValueObjects;

namespace SterlingVale.Domain.Model;

/// <summary>A synthetic advisory household. All monetary values are reported in <see cref="BaseCurrency"/>.</summary>
public sealed record Household(
    HouseholdId Id,
    string Name,
    Currency BaseCurrency);

/// <summary>A custodial account belonging to a household.</summary>
public sealed record Account(
    AccountId Id,
    HouseholdId HouseholdId,
    CustodianId CustodianId,
    string Name,
    Currency Currency);

/// <summary>A held position within an account.</summary>
public sealed record Position(
    PositionId Id,
    AccountId AccountId,
    Symbol Symbol,
    decimal Quantity,
    AssetClass AssetClass,
    Currency Currency);

/// <summary>A target allocation for a single asset class.</summary>
public sealed record AllocationTarget(AssetClass AssetClass, Percentage Target);

/// <summary>
/// The investment policy for a household: target allocations plus risk limits. Targets must sum
/// to 100% within <see cref="FinancialMath.AllocationSumTolerance"/>; validate via
/// <see cref="TargetsSumValid"/>.
/// </summary>
public sealed record HouseholdPolicy(
    HouseholdId HouseholdId,
    IReadOnlyList<AllocationTarget> Targets,
    Percentage DriftTolerance,
    Percentage MaxConcentration,
    Percentage MaxFxExposure,
    Percentage MaxCryptoExposure)
{
    /// <summary>Returns true when the target allocations sum to ~100%.</summary>
    public bool TargetsSumValid()
    {
        decimal sum = 0m;
        foreach (var t in Targets)
        {
            sum += t.Target.Fraction;
        }

        return FinancialMath.ApproximatelyEqual(sum, 1m, FinancialMath.AllocationSumTolerance);
    }
}
