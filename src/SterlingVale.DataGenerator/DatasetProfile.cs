namespace SterlingVale.DataGenerator;

/// <summary>Identifies the size profile of a generated dataset.</summary>
public enum DatasetSize
{
    /// <summary>Small profile for fast tests.</summary>
    Small = 0,

    /// <summary>Canonical benchmark profile (~40 households).</summary>
    Medium = 1,

    /// <summary>Large profile for stress scenarios.</summary>
    Large = 2,
}

/// <summary>
/// Deterministic configuration for a dataset profile. The same profile always yields the same
/// seed and counts, and therefore the same files and hashes.
/// </summary>
public sealed record DatasetProfile(
    DatasetSize Size,
    string Name,
    ulong Seed,
    int HouseholdCount,
    int MinAccountsPerHousehold,
    int MaxAccountsPerHousehold,
    int MinPositionsPerAccount,
    int MaxPositionsPerAccount)
{
    /// <summary>The generator version. Bump when generation logic changes; recorded in the manifest.</summary>
    public const string GeneratorVersion = "1.0.0";

    /// <summary>The small profile.</summary>
    public static readonly DatasetProfile Small =
        new(DatasetSize.Small, "small", Seed: 0x51_7E_11_10_00_00_00_01UL, 10, 3, 4, 8, 14);

    /// <summary>The medium (canonical) profile.</summary>
    public static readonly DatasetProfile Medium =
        new(DatasetSize.Medium, "medium", Seed: 0x51_7E_11_10_00_00_00_02UL, 40, 3, 5, 8, 20);

    /// <summary>The large profile.</summary>
    public static readonly DatasetProfile Large =
        new(DatasetSize.Large, "large", Seed: 0x51_7E_11_10_00_00_00_03UL, 120, 3, 5, 10, 20);

    /// <summary>All profiles in stable order.</summary>
    public static readonly IReadOnlyList<DatasetProfile> All = [Small, Medium, Large];

    /// <summary>Resolves a profile by case-insensitive name.</summary>
    public static DatasetProfile ByName(string name) =>
        All.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))
        ?? throw new ArgumentException($"Unknown dataset profile '{name}'.", nameof(name));
}
