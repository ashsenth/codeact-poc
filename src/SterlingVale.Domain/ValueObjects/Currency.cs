namespace SterlingVale.Domain.ValueObjects;

/// <summary>
/// A validated ISO-4217-style currency code. Only the four synthetic-scenario currencies
/// are supported; construction of any other code throws.
/// </summary>
public readonly record struct Currency
{
    /// <summary>United States dollar.</summary>
    public static readonly Currency Usd = new("USD", 2);

    /// <summary>Euro.</summary>
    public static readonly Currency Eur = new("EUR", 2);

    /// <summary>Pound sterling.</summary>
    public static readonly Currency Gbp = new("GBP", 2);

    /// <summary>Japanese yen (zero minor units).</summary>
    public static readonly Currency Jpy = new("JPY", 0);

    private static readonly IReadOnlyDictionary<string, Currency> Known =
        new Dictionary<string, Currency>(StringComparer.Ordinal)
        {
            [Usd.Code] = Usd,
            [Eur.Code] = Eur,
            [Gbp.Code] = Gbp,
            [Jpy.Code] = Jpy,
        };

    private Currency(string code, int minorUnits)
    {
        Code = code;
        MinorUnits = minorUnits;
    }

    /// <summary>The three-letter uppercase currency code (e.g. "USD").</summary>
    public string Code { get; }

    /// <summary>Number of decimal places used when rounding amounts in this currency.</summary>
    public int MinorUnits { get; }

    /// <summary>All supported currencies.</summary>
    public static IReadOnlyCollection<Currency> All => (IReadOnlyCollection<Currency>)Known.Values;

    /// <summary>Parses a currency code, throwing <see cref="ArgumentException"/> when unsupported.</summary>
    public static Currency Parse(string code)
    {
        if (!TryParse(code, out var currency))
        {
            throw new ArgumentException($"Unsupported currency code '{code}'.", nameof(code));
        }

        return currency;
    }

    /// <summary>Attempts to parse a currency code (case-insensitive, trimmed).</summary>
    public static bool TryParse(string? code, out Currency currency)
    {
        if (!string.IsNullOrWhiteSpace(code) &&
            Known.TryGetValue(code.Trim().ToUpperInvariant(), out currency))
        {
            return true;
        }

        currency = default;
        return false;
    }

    /// <inheritdoc />
    public override string ToString() => Code;
}
