using SterlingVale.Application.Oracle;
using SterlingVale.Application.Pricing;
using SterlingVale.Domain.Model;
using SterlingVale.Domain.ValueObjects;
using Xunit;

namespace SterlingVale.Application.Tests;

public sealed class FxConverterTests
{
    private static readonly DateTimeOffset AsOf = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static FxConverter Converter(params FxRate[] rates) => new(rates);

    [Fact]
    public void Identity_conversion_returns_same_amount()
    {
        var fx = Converter();
        Assert.Equal(100m, fx.ConvertRaw(100m, Currency.Usd, Currency.Usd));
    }

    [Fact]
    public void Direct_rate_is_applied()
    {
        var fx = Converter(new FxRate(Currency.Usd, Currency.Eur, 0.9m, AsOf));
        Assert.Equal(90m, fx.ConvertRaw(100m, Currency.Usd, Currency.Eur));
    }

    [Fact]
    public void Inverse_rate_is_derived_when_only_reverse_exists()
    {
        var fx = Converter(new FxRate(Currency.Eur, Currency.Usd, 1.25m, AsOf));
        // 1 USD = 1/1.25 EUR = 0.8 EUR
        Assert.Equal(80m, fx.ConvertRaw(100m, Currency.Usd, Currency.Eur));
    }

    [Fact]
    public void Missing_rate_throws()
    {
        var fx = Converter(new FxRate(Currency.Usd, Currency.Eur, 0.9m, AsOf));
        Assert.Throws<FxRateNotFoundException>(() => fx.ConvertRaw(1m, Currency.Usd, Currency.Jpy));
    }
}

public sealed class ValuationTests
{
    [Theory]
    [InlineData(10, 5, 50)]
    [InlineData(0, 100, 0)]
    [InlineData(2.5, 4, 10)]
    public void NativeValue_multiplies_quantity_by_price(decimal quantity, decimal price, decimal expected) =>
        Assert.Equal(expected, Valuation.NativeValue(quantity, price));
}
