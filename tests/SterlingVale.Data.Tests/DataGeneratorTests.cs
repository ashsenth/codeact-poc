using System.Text;
using System.Text.Json;
using SterlingVale.DataGenerator;
using Xunit;

namespace SterlingVale.Data.Tests;

public sealed class DataGeneratorTests
{
    private static GeneratedDataset Generate(DatasetProfile profile) =>
        new SyntheticDataGenerator().Generate(profile);

    [Theory]
    [MemberData(nameof(Profiles))]
    public void Dataset_is_valid(DatasetProfile profile)
    {
        var dataset = Generate(profile);
        var result = new DatasetValidator().Validate(dataset);
        Assert.True(result.IsValid, string.Join("; ", result.Errors));
    }

    [Theory]
    [MemberData(nameof(Profiles))]
    public void Generation_is_deterministic_by_hash(DatasetProfile profile)
    {
        string first = HashDataset(Generate(profile));
        string second = HashDataset(Generate(profile));
        Assert.Equal(first, second);
    }

    [Fact]
    public void Medium_profile_has_expected_household_count()
    {
        var dataset = Generate(DatasetProfile.Medium);
        Assert.Equal(40, dataset.Households.Count);
    }

    [Fact]
    public void Dataset_contains_all_four_currencies()
    {
        var dataset = Generate(DatasetProfile.Large);
        var currencies = dataset.Households.Select(h => h.BaseCurrency).ToHashSet(StringComparer.Ordinal);
        Assert.Contains("USD", currencies);
        Assert.Contains("EUR", currencies);
        Assert.Contains("GBP", currencies);
        Assert.Contains("JPY", currencies);
    }

    [Fact]
    public void Dataset_covers_all_asset_classes()
    {
        var dataset = Generate(DatasetProfile.Large);
        var classes = dataset.Positions.Select(p => p.AssetClass).ToHashSet(StringComparer.Ordinal);
        foreach (var expected in new[] { "Equity", "FixedIncome", "Cash", "Alternative", "Crypto" })
        {
            Assert.Contains(expected, classes);
        }
    }

    [Fact]
    public void Deliberate_breach_positions_are_present()
    {
        var dataset = Generate(DatasetProfile.Medium);
        Assert.Contains(dataset.Positions, p => p.Id.EndsWith("-BREACH", StringComparison.Ordinal));
    }

    [Fact]
    public void Fx_rates_round_trip_within_tolerance()
    {
        var dataset = Generate(DatasetProfile.Small);
        var byPair = dataset.FxRates.ToDictionary(r => (r.From, r.To), r => r.Rate);
        foreach (var rate in dataset.FxRates)
        {
            var inverse = byPair[(rate.To, rate.From)];
            Assert.True(Math.Abs((rate.Rate * inverse) - 1m) < 0.01m,
                $"{rate.From}->{rate.To} round trip drifted: {rate.Rate * inverse}");
        }
    }

    public static IEnumerable<object[]> Profiles() =>
        DatasetProfile.All.Select(p => new object[] { p });

    private static string HashDataset(GeneratedDataset dataset)
    {
        var payload = new
        {
            dataset.Households,
            dataset.Accounts,
            dataset.Positions,
            dataset.Prices,
            dataset.FxRates,
        };
        string json = JsonSerializer.Serialize(payload, CanonicalJson.Options);
        return DatasetWriter.Sha256Hex(new UTF8Encoding(false).GetBytes(json));
    }
}
