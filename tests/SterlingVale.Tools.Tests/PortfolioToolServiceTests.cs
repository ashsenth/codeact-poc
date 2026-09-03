using SterlingVale.Tools;
using Xunit;

namespace SterlingVale.Tools.Tests;

public sealed class PortfolioToolServiceTests
{
    private readonly PortfolioToolService _service = TestSnapshot.Service();

    [Fact]
    public void ListHouseholds_returns_all_households()
    {
        var households = _service.ListHouseholds();
        Assert.Single(households);
        Assert.Equal("H0001", households[0].Id);
        Assert.Equal("USD", households[0].BaseCurrency);
    }

    [Fact]
    public void GetPolicy_returns_targets_and_limits()
    {
        var policy = _service.GetPolicy("H0001");
        Assert.Equal("H0001", policy.HouseholdId);
        Assert.Equal(2, policy.Targets.Count);
        Assert.Equal(0.60m, policy.MaxConcentrationFraction);
    }

    [Fact]
    public void ListAccounts_returns_household_accounts()
    {
        var accounts = _service.ListAccounts("H0001");
        Assert.Single(accounts);
        Assert.Equal("H0001-A01", accounts[0].Id);
    }

    [Fact]
    public void ListPositions_returns_account_positions()
    {
        var positions = _service.ListPositions("H0001-A01");
        Assert.Equal(2, positions.Count);
    }

    [Fact]
    public void GetPrice_returns_quote()
    {
        var price = _service.GetPrice("EQ_US_LARGE");
        Assert.Equal(100m, price.Price);
        Assert.Equal("USD", price.Currency);
    }

    [Fact]
    public void GetFx_identity_is_one()
    {
        Assert.Equal(1m, _service.GetFx("USD", "USD").Rate);
    }

    [Fact]
    public void GetFx_direct_rate_is_returned()
    {
        Assert.Equal(1.1m, _service.GetFx("EUR", "USD").Rate);
    }

    [Fact]
    public void GetFx_inverse_rate_is_derived()
    {
        // Only EUR->USD (1.1) exists; USD->EUR must be derived as 1/1.1.
        Assert.Equal(1m / 1.1m, _service.GetFx("USD", "EUR").Rate);
    }

    [Fact]
    public void AssetClass_classifies_known_symbol()
    {
        Assert.Equal("Equity", _service.AssetClass("EQ_US_LARGE").AssetClass);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void GetPolicy_rejects_empty_argument(string householdId) =>
        Assert.Throws<ToolArgumentException>(() => _service.GetPolicy(householdId));

    [Fact]
    public void GetPolicy_unknown_household_throws_not_found() =>
        Assert.Throws<ToolNotFoundException>(() => _service.GetPolicy("H9999"));

    [Fact]
    public void GetPrice_unknown_symbol_throws_not_found() =>
        Assert.Throws<ToolNotFoundException>(() => _service.GetPrice("NOPE"));

    [Fact]
    public void GetFx_invalid_currency_throws_argument() =>
        Assert.Throws<ToolArgumentException>(() => _service.GetFx("USD", "CHF"));

    [Fact]
    public void AssetClass_unknown_symbol_throws_not_found() =>
        Assert.Throws<ToolNotFoundException>(() => _service.AssetClass("NOPE"));

    [Fact]
    public void Cancellation_is_honored()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        Assert.Throws<OperationCanceledException>(() => _service.ListHouseholds(cts.Token));
    }

    [Fact]
    public void Results_are_deterministic()
    {
        var first = _service.ListPositions("H0001-A01");
        var second = _service.ListPositions("H0001-A01");
        Assert.Equal(first, second);
    }

    [Fact]
    public async Task Service_is_safe_for_concurrent_use()
    {
        var tasks = Enumerable.Range(0, 32).Select(iteration => Task.Run(() =>
        {
            _ = iteration;
            _ = _service.ListHouseholds();
            _ = _service.GetPolicy("H0001");
            _ = _service.GetFx("EUR", "USD");
            _ = _service.AssetClass("FI_US_AGG");
        }));
        await Task.WhenAll(tasks);
    }
}
