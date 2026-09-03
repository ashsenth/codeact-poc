using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SterlingVale.DataGenerator;

/// <summary>Serializes a <see cref="GeneratedDataset"/> to canonical JSON files plus a hashed manifest.</summary>
public sealed class DatasetWriter
{
    /// <summary>Writes all dataset files into <paramref name="outputDirectory"/> and returns the manifest.</summary>
    public ManifestDto Write(GeneratedDataset dataset, string outputDirectory)
    {
        ArgumentNullException.ThrowIfNull(dataset);
        Directory.CreateDirectory(outputDirectory);

        var files = new List<FileHashDto>
        {
            WriteFile(outputDirectory, "households.json", dataset.Households),
            WriteFile(outputDirectory, "accounts.json", dataset.Accounts),
            WriteFile(outputDirectory, "positions.json", dataset.Positions),
            WriteFile(outputDirectory, "prices.json", dataset.Prices),
            WriteFile(outputDirectory, "fx.json", dataset.FxRates),
        };

        var manifest = new ManifestDto(
            dataset.Profile.Name,
            dataset.Profile.Seed,
            DatasetProfile.GeneratorVersion,
            DateTimeOffset.UtcNow.ToString("O"),
            files);

        string manifestJson = JsonSerializer.Serialize(manifest, CanonicalJson.Options);
        File.WriteAllText(Path.Combine(outputDirectory, "manifest.json"), manifestJson, new UTF8Encoding(false));
        return manifest;
    }

    private static FileHashDto WriteFile<T>(string dir, string fileName, IReadOnlyList<T> records)
    {
        string json = JsonSerializer.Serialize(records, CanonicalJson.Options);
        byte[] bytes = new UTF8Encoding(false).GetBytes(json);
        File.WriteAllBytes(Path.Combine(dir, fileName), bytes);
        return new FileHashDto(fileName, records.Count, Sha256Hex(bytes));
    }

    /// <summary>Computes the lowercase hex SHA-256 of the given bytes.</summary>
    public static string Sha256Hex(byte[] bytes) =>
        Convert.ToHexStringLower(SHA256.HashData(bytes));
}
