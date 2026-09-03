namespace SterlingVale.Domain.ValueObjects;

/// <summary>
/// A ratio expressed as a fraction in the range [0, 1] (so 0.25 == 25%). Drift values may be
/// negative, so <see cref="Percentage"/> permits negatives; use <see cref="Fraction"/> factories
/// that validate bounds where appropriate.
/// </summary>
public readonly record struct Percentage : IComparable<Percentage>
{
    private Percentage(decimal fraction) => Fraction = FinancialMath.RoundRatio(fraction);

    /// <summary>The underlying fraction (0.25 == 25%).</summary>
    public decimal Fraction { get; }

    /// <summary>The value expressed in whole percent (25 == 25%).</summary>
    public decimal Percent => Fraction * 100m;

    /// <summary>Zero.</summary>
    public static Percentage Zero => new(0m);

    /// <summary>One hundred percent (fraction 1.0).</summary>
    public static Percentage OneHundred => new(1m);

    /// <summary>Creates a percentage from a fraction (0.25 == 25%). Allows negatives (drift).</summary>
    public static Percentage FromFraction(decimal fraction) => new(fraction);

    /// <summary>Creates a percentage from whole percent (25 == 25%).</summary>
    public static Percentage FromPercent(decimal percent) => new(percent / 100m);

    /// <summary>
    /// Creates a bounded fraction in [0, 1], throwing when out of range. Use for target allocations
    /// and exposure limits that cannot be negative or exceed 100%.
    /// </summary>
    public static Percentage BoundedFraction(decimal fraction)
    {
        if (fraction < 0m || fraction > 1m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(fraction), fraction, "Bounded percentage must be within [0, 1].");
        }

        return new Percentage(fraction);
    }

    /// <summary>Absolute value of the percentage.</summary>
    public Percentage Abs() => new(Math.Abs(Fraction));

    /// <inheritdoc />
    public int CompareTo(Percentage other) => Fraction.CompareTo(other.Fraction);

    public static bool operator <(Percentage left, Percentage right) => left.CompareTo(right) < 0;

    public static bool operator >(Percentage left, Percentage right) => left.CompareTo(right) > 0;

    public static bool operator <=(Percentage left, Percentage right) => left.CompareTo(right) <= 0;

    public static bool operator >=(Percentage left, Percentage right) => left.CompareTo(right) >= 0;

    /// <inheritdoc />
    public override string ToString() => $"{Percent:0.####}%";
}
