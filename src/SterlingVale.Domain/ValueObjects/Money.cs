namespace SterlingVale.Domain.ValueObjects;

/// <summary>
/// A monetary amount in a specific <see cref="Currency"/>. Amounts are rounded to the currency's
/// minor-unit precision on construction using banker's rounding. Arithmetic requires matching
/// currencies.
/// </summary>
public readonly record struct Money : IComparable<Money>
{
    /// <summary>Creates a money value, rounding to the currency's minor units.</summary>
    public Money(decimal amount, Currency currency)
    {
        Currency = currency;
        Amount = FinancialMath.RoundMoney(amount, currency.MinorUnits);
    }

    /// <summary>The rounded amount.</summary>
    public decimal Amount { get; }

    /// <summary>The currency of this amount.</summary>
    public Currency Currency { get; }

    /// <summary>Zero in the given currency.</summary>
    public static Money Zero(Currency currency) => new(0m, currency);

    /// <summary>Adds two amounts of the same currency.</summary>
    public Money Add(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(Amount + other.Amount, Currency);
    }

    /// <summary>Subtracts an amount of the same currency.</summary>
    public Money Subtract(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(Amount - other.Amount, Currency);
    }

    /// <summary>Multiplies the amount by a scalar factor.</summary>
    public Money Multiply(decimal factor) => new(Amount * factor, Currency);

    /// <inheritdoc />
    public int CompareTo(Money other)
    {
        EnsureSameCurrency(other);
        return Amount.CompareTo(other.Amount);
    }

    public static bool operator <(Money left, Money right) => left.CompareTo(right) < 0;

    public static bool operator >(Money left, Money right) => left.CompareTo(right) > 0;

    public static bool operator <=(Money left, Money right) => left.CompareTo(right) <= 0;

    public static bool operator >=(Money left, Money right) => left.CompareTo(right) >= 0;

    private void EnsureSameCurrency(Money other)
    {
        if (Currency != other.Currency)
        {
            throw new InvalidOperationException(
                $"Cannot combine {Currency} and {other.Currency} amounts.");
        }
    }

    /// <inheritdoc />
    public override string ToString() => $"{Amount} {Currency.Code}";
}
