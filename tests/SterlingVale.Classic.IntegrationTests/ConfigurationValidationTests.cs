using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SterlingVale.Infrastructure;
using Xunit;

namespace SterlingVale.Classic.IntegrationTests;

/// <summary>
/// Verifies that <see cref="InfrastructureServiceCollectionExtensions.AddSterlingValeInfrastructure"/>
/// validates configuration at startup: unset values fall back to defaults, but a value that is
/// present yet malformed or out of range fails fast instead of silently reverting to a default.
/// </summary>
public sealed class ConfigurationValidationTests
{
    private static IConfiguration Config(params (string Key, string Value)[] settings) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(settings.Select(s => new KeyValuePair<string, string?>(s.Key, s.Value)))
            .Build();

    [Fact]
    public void Unset_numeric_configuration_uses_defaults()
    {
        var exception = Record.Exception(() =>
            new ServiceCollection().AddSterlingValeInfrastructure(Config()));

        Assert.Null(exception);
    }

    [Theory]
    [InlineData("BENCHMARK_MAX_TURNS", "not-a-number")]
    [InlineData("BENCHMARK_TIMEOUT_SECONDS", "12.5")]
    [InlineData("PRICING_INPUT_PER_MILLION", "abc")]
    public void Malformed_numeric_configuration_fails_fast(string key, string value)
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AddSterlingValeInfrastructure(Config((key, value))));

        Assert.Contains(key, exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("BENCHMARK_MAX_TURNS", "0")]
    [InlineData("BENCHMARK_TIMEOUT_SECONDS", "-5")]
    [InlineData("PRICING_OUTPUT_PER_MILLION", "-1")]
    public void Out_of_range_numeric_configuration_fails_fast(string key, string value)
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AddSterlingValeInfrastructure(Config((key, value))));

        Assert.Contains(key, exception.Message, StringComparison.Ordinal);
    }
}
