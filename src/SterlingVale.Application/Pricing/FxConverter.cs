using SterlingVale.Domain.Model;
using SterlingVale.Domain.ValueObjects;

namespace SterlingVale.Application.Pricing;

/// <summary>Raised when a required FX rate (direct or inverse) is not available.</summary>
public sealed class FxRateNotFoundException(Currency from, Currency to)
    : Exception($"No FX rate available to convert {from.Code} to {to.Code}.")
{
    /// <summary>The source currency.</summary>
    public Currency From { get; } = from;

    /// <summary>The target currency.</summary>
    public Currency To { get; } = to;
}

/// <summary>
/// Converts amounts between currencies using a fixed set of FX rates.
///
/// <para><b>Direct behavior:</b> when a rate From→To exists, the amount is multiplied by it.</para>
/// <para><b>Inverse behavior:</b> when only To→From exists, the amount is divided by that rate.</para>
/// <para><b>Identity:</b> converting a currency to itself returns the amount unchanged (rate 1).</para>
/// <para><b>Missing-rate behavior:</b> when neither direction exists, an
/// <see cref="FxRateNotFoundException"/> is thrown (the oracle never silently drops value).</para>
/// <para><b>Precision:</b> conversions are computed with full <see cref="decimal"/> precision and are
/// NOT rounded here; rounding to a currency's minor units happens only when a
/// <see cref="Money"/> is materialized.</para>
/// </summary>
public sealed class FxConverter
{
    private readonly Dictionary<(string From, string To), decimal> _rates;

    /// <summary>Creates a converter from the given rate set. Duplicate directed pairs throw.</summary>
    public FxConverter(IEnumerable<FxRate> rates)
    {
        ArgumentNullException.ThrowIfNull(rates);
        _rates = new Dictionary<(string, string), decimal>();
        foreach (var rate in rates)
        {
            _rates.Add((rate.From.Code, rate.To.Code), rate.Rate);
        }
    }

    /// <summary>Returns the effective rate to convert one unit of <paramref name="from"/> to <paramref name="to"/>.</summary>
    public decimal Rate(Currency from, Currency to)
    {
        if (from == to)
        {
            return 1m;
        }

        if (_rates.TryGetValue((from.Code, to.Code), out var direct))
        {
            return direct;
        }

        if (_rates.TryGetValue((to.Code, from.Code), out var inverse) && inverse != 0m)
        {
            return 1m / inverse;
        }

        throw new FxRateNotFoundException(from, to);
    }

    /// <summary>Converts a raw amount from one currency to another with full decimal precision.</summary>
    public decimal ConvertRaw(decimal amount, Currency from, Currency to) => amount * Rate(from, to);
}
