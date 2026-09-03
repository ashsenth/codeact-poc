using System.Text.Json;
using SterlingVale.AgentShared;
using SterlingVale.Application.Portfolio;
using SterlingVale.DataGenerator;
using SterlingVale.Domain.Model;
using SterlingVale.Domain.ValueObjects;

namespace SterlingVale.Infrastructure.Data;

/// <summary>A loaded dataset profile: its snapshot and the manifest-derived dataset hash.</summary>
public sealed record LoadedDataset(PortfolioSnapshot Snapshot, string DatasetHash);

/// <summary>
/// Loads a generated dataset profile from disk (the six JSON files produced by the DataGenerator),
/// maps the DTOs into the domain model, and derives a stable dataset hash from the manifest.
/// </summary>
public sealed class DatasetLoader
{
    private static readonly DateTimeOffset FallbackAsOf = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>Loads and maps the profile located in <paramref name="profileDirectory"/>.</summary>
    public LoadedDataset Load(string profileDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileDirectory);
        if (!Directory.Exists(profileDirectory))
        {
            throw new DirectoryNotFoundException($"Dataset directory not found: {profileDirectory}");
        }

        var households = Read<List<HouseholdDto>>(profileDirectory, "households.json");
        var accounts = Read<List<AccountDto>>(profileDirectory, "accounts.json");
        var positions = Read<List<PositionDto>>(profileDirectory, "positions.json");
        var prices = Read<List<PriceDto>>(profileDirectory, "prices.json");
        var fx = Read<List<FxRateDto>>(profileDirectory, "fx.json");
        var manifest = Read<ManifestDto>(profileDirectory, "manifest.json");

        var snapshot = new PortfolioSnapshot(
            households.Select(MapHousehold).ToList(),
            accounts.Select(MapAccount).ToList(),
            positions.Select(MapPosition).ToList(),
            households.Select(MapPolicy).ToList(),
            prices.Select(MapPrice).ToList(),
            fx.Select(MapFx).ToList());

        return new LoadedDataset(snapshot, ComputeDatasetHash(manifest));
    }

    private static T Read<T>(string dir, string file)
    {
        string path = Path.Combine(dir, file);
        string json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<T>(json, CanonicalJson.Options)
            ?? throw new InvalidDataException($"Failed to deserialize {file}.");
    }

    private static Household MapHousehold(HouseholdDto h) =>
        new(HouseholdId.Create(h.Id), h.Name, Currency.Parse(h.BaseCurrency));

    private static HouseholdPolicy MapPolicy(HouseholdDto h) => new(
        HouseholdId.Create(h.Id),
        h.Policy.Targets
            .Select(t => new AllocationTarget(AssetClasses.Parse(t.AssetClass), Percentage.FromFraction(t.TargetFraction)))
            .ToList(),
        Percentage.FromFraction(h.Policy.DriftToleranceFraction),
        Percentage.FromFraction(h.Policy.MaxConcentrationFraction),
        Percentage.FromFraction(h.Policy.MaxFxExposureFraction),
        Percentage.FromFraction(h.Policy.MaxCryptoExposureFraction));

    private static Account MapAccount(AccountDto a) =>
        new(AccountId.Create(a.Id), HouseholdId.Create(a.HouseholdId), CustodianId.Create(a.CustodianId), a.Name, Currency.Parse(a.Currency));

    private static Position MapPosition(PositionDto p) =>
        new(PositionId.Create(p.Id), AccountId.Create(p.AccountId), Symbol.Create(p.Symbol), p.Quantity, AssetClasses.Parse(p.AssetClass), Currency.Parse(p.Currency));

    private static PriceQuote MapPrice(PriceDto p) =>
        new(Symbol.Create(p.Symbol), p.Price, Currency.Parse(p.Currency), ParseAsOf(p.AsOf));

    private static FxRate MapFx(FxRateDto f) =>
        new(Currency.Parse(f.From), Currency.Parse(f.To), f.Rate, ParseAsOf(f.AsOf));

    private static DateTimeOffset ParseAsOf(string value) =>
        DateTimeOffset.TryParse(value, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.RoundtripKind, out var parsed)
            ? parsed
            : FallbackAsOf;

    private static string ComputeDatasetHash(ManifestDto manifest)
    {
        var ordered = manifest.Files
            .OrderBy(f => f.File, StringComparer.Ordinal)
            .Select(f => $"{f.File}:{f.Sha256}");
        return Hashing.Sha256Hex($"{manifest.Profile}|{manifest.Seed}|{string.Join('|', ordered)}");
    }
}
