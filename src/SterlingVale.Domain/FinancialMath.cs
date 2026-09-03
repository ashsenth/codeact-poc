namespace SterlingVale.Domain;

/// <summary>
/// Centralized rounding and comparison rules for all financial calculations.
/// Every monetary and ratio computation in the domain and oracle MUST route through
/// these helpers so that Classic and CodeAct outputs are compared on identical terms.
/// </summary>
public static class FinancialMath
{
    /// <summary>Decimal places used when rounding ratio/percentage values (fractions).</summary>
    public const int RatioDecimals = 10;

    /// <summary>
    /// Default absolute tolerance for comparing two ratios/percentages expressed as fractions
    /// (e.g. 0.0001 = 1 basis point). Documented in docs/metrics.md.
    /// </summary>
    public const decimal DefaultRatioTolerance = 0.0001m;

    /// <summary>
    /// Tolerance (as a fraction of the household total) within which the sum of proposed
    /// asset-class trade notionals must net to zero.
    /// </summary>
    public const decimal NetNotionalTolerance = 0.0001m;

    /// <summary>
    /// Tolerance within which allocation targets must sum to 100% (expressed as a fraction: 1.0).
    /// </summary>
    public const decimal AllocationSumTolerance = 0.0001m;

    /// <summary>Banker's rounding (round-half-to-even) is used everywhere for reproducibility.</summary>
    public const MidpointRounding Rounding = MidpointRounding.ToEven;

    /// <summary>Rounds a monetary amount to the given currency's minor-unit precision.</summary>
    public static decimal RoundMoney(decimal value, int decimals) =>
        Math.Round(value, decimals, Rounding);

    /// <summary>Rounds a ratio/fraction to <see cref="RatioDecimals"/> places.</summary>
    public static decimal RoundRatio(decimal value) =>
        Math.Round(value, RatioDecimals, Rounding);

    /// <summary>
    /// Compares two decimals within an absolute tolerance. Returns true when
    /// <c>|a - b| &lt;= tolerance</c>.
    /// </summary>
    public static bool ApproximatelyEqual(decimal a, decimal b, decimal tolerance) =>
        Math.Abs(a - b) <= tolerance;

    /// <summary>
    /// Safe division that returns 0 when the denominator is zero. Zero-total households
    /// therefore produce zero allocations rather than throwing (see docs/metrics.md).
    /// </summary>
    public static decimal SafeDivide(decimal numerator, decimal denominator) =>
        denominator == 0m ? 0m : numerator / denominator;
}
