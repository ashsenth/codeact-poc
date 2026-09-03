using SterlingVale.Domain.ValueObjects;

namespace SterlingVale.Domain.Model;

/// <summary>A synthetic price quote for a symbol, denominated in <see cref="Currency"/>.</summary>
public sealed record PriceQuote(
    Symbol Symbol,
    decimal Price,
    Currency Currency,
    DateTimeOffset AsOf);

/// <summary>
/// A synthetic FX rate: one unit of <see cref="From"/> equals <see cref="Rate"/> units of
/// <see cref="To"/>. Inverse conversions are derived by the oracle; see docs/metrics.md.
/// </summary>
public sealed record FxRate(
    Currency From,
    Currency To,
    decimal Rate,
    DateTimeOffset AsOf);
