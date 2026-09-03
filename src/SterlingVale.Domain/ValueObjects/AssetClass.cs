namespace SterlingVale.Domain.ValueObjects;

/// <summary>
/// The asset classes tracked by the scenario. Values are stable and used as ordering /
/// comparison keys in reports; do not renumber.
/// </summary>
public enum AssetClass
{
    /// <summary>Publicly traded equities.</summary>
    Equity = 0,

    /// <summary>Bonds and other fixed-income instruments.</summary>
    FixedIncome = 1,

    /// <summary>Cash and cash equivalents.</summary>
    Cash = 2,

    /// <summary>Alternatives (real assets, private markets, etc.).</summary>
    Alternative = 3,

    /// <summary>Crypto assets.</summary>
    Crypto = 4,
}

/// <summary>Helpers for parsing and enumerating <see cref="AssetClass"/> values deterministically.</summary>
public static class AssetClasses
{
    /// <summary>All asset classes in stable ordinal order.</summary>
    public static readonly IReadOnlyList<AssetClass> All =
    [
        AssetClass.Equity,
        AssetClass.FixedIncome,
        AssetClass.Cash,
        AssetClass.Alternative,
        AssetClass.Crypto,
    ];

    /// <summary>Parses an asset-class name (case-insensitive), throwing when unknown.</summary>
    public static AssetClass Parse(string value) =>
        Enum.TryParse<AssetClass>(value, ignoreCase: true, out var result) && Enum.IsDefined(result)
            ? result
            : throw new ArgumentException($"Unknown asset class '{value}'.", nameof(value));
}
