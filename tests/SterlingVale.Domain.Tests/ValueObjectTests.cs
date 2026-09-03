using SterlingVale.Domain;
using SterlingVale.Domain.ValueObjects;
using Xunit;

namespace SterlingVale.Domain.Tests;

public sealed class CurrencyTests
{
    [Theory]
    [InlineData("USD")]
    [InlineData("eur")]
    [InlineData(" gbp ")]
    [InlineData("JPY")]
    public void Parse_accepts_supported_currencies(string code)
    {
        var currency = Currency.Parse(code);
        Assert.Equal(code.Trim().ToUpperInvariant(), currency.Code);
    }

    [Theory]
    [InlineData("CHF")]
    [InlineData("")]
    [InlineData("US")]
    public void Parse_rejects_unsupported_currencies(string code) =>
        Assert.Throws<ArgumentException>(() => Currency.Parse(code));

    [Fact]
    public void Jpy_has_zero_minor_units() => Assert.Equal(0, Currency.Jpy.MinorUnits);

    [Fact]
    public void Usd_has_two_minor_units() => Assert.Equal(2, Currency.Usd.MinorUnits);
}

public sealed class MoneyTests
{
    [Fact]
    public void Rounds_to_currency_minor_units()
    {
        var money = new Money(10.005m, Currency.Usd);
        Assert.Equal(10.00m, money.Amount); // banker's rounding: 10.005 -> 10.00
    }

    [Fact]
    public void Jpy_rounds_to_whole_units()
    {
        var money = new Money(1234.6m, Currency.Jpy);
        Assert.Equal(1235m, money.Amount);
    }

    [Fact]
    public void Add_requires_same_currency()
    {
        var usd = new Money(1m, Currency.Usd);
        var eur = new Money(1m, Currency.Eur);
        Assert.Throws<InvalidOperationException>(() => usd.Add(eur));
    }

    [Fact]
    public void Add_sums_same_currency()
    {
        var result = new Money(1.25m, Currency.Usd).Add(new Money(2.50m, Currency.Usd));
        Assert.Equal(3.75m, result.Amount);
    }
}

public sealed class PercentageTests
{
    [Fact]
    public void FromPercent_and_FromFraction_agree()
    {
        Assert.Equal(Percentage.FromFraction(0.25m), Percentage.FromPercent(25m));
    }

    [Fact]
    public void BoundedFraction_rejects_out_of_range()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Percentage.BoundedFraction(1.5m));
        Assert.Throws<ArgumentOutOfRangeException>(() => Percentage.BoundedFraction(-0.1m));
    }

    [Fact]
    public void Drift_allows_negative_fractions()
    {
        var drift = Percentage.FromFraction(-0.03m);
        Assert.Equal(-0.03m, drift.Fraction);
        Assert.Equal(0.03m, drift.Abs().Fraction);
    }
}

public sealed class FinancialMathTests
{
    [Fact]
    public void SafeDivide_returns_zero_for_zero_denominator() =>
        Assert.Equal(0m, FinancialMath.SafeDivide(5m, 0m));

    [Fact]
    public void ApproximatelyEqual_respects_tolerance()
    {
        Assert.True(FinancialMath.ApproximatelyEqual(1.00000m, 1.00005m, FinancialMath.DefaultRatioTolerance));
        Assert.False(FinancialMath.ApproximatelyEqual(1.0m, 1.01m, FinancialMath.DefaultRatioTolerance));
    }
}

public sealed class IdTests
{
    [Fact]
    public void HouseholdId_rejects_empty() =>
        Assert.Throws<ArgumentException>(() => HouseholdId.Create("  "));

    [Fact]
    public void Symbol_is_uppercased() =>
        Assert.Equal("CRY_BTC", Symbol.Create("cry_btc").Value);
}
