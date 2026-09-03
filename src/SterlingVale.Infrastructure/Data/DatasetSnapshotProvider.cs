using Microsoft.Extensions.Options;
using SterlingVale.AgentShared.Analysis;
using SterlingVale.Application.Portfolio;
using SterlingVale.Infrastructure.Configuration;

namespace SterlingVale.Infrastructure.Data;

/// <summary>
/// Eagerly loads the configured dataset profiles at startup and serves validated snapshots. Load
/// failures are captured (not thrown) so the readiness endpoint can report them.
/// </summary>
public sealed class DatasetSnapshotProvider : ISnapshotProvider
{
    private readonly Dictionary<string, LoadedDataset> _datasets = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _errors = [];

    /// <summary>Loads all configured profiles from disk.</summary>
    public DatasetSnapshotProvider(DatasetLoader loader, IOptions<DatasetOptions> options)
    {
        ArgumentNullException.ThrowIfNull(loader);
        ArgumentNullException.ThrowIfNull(options);
        var config = options.Value;

        foreach (var profile in config.Profiles)
        {
            string dir = Path.Combine(config.DataRoot, profile);
            try
            {
                var loaded = loader.Load(dir);
                if (loaded.Snapshot.Households.Count == 0)
                {
                    _errors.Add($"Profile '{profile}' loaded but contains no households.");
                    continue;
                }

                _datasets[profile] = loaded;
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or FormatException or ArgumentException)
            {
                _errors.Add($"Profile '{profile}' failed to load: {ex.Message}");
            }
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<string> AvailableProfiles => _datasets.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList();

    /// <inheritdoc />
    public PortfolioSnapshot GetSnapshot(string profile) =>
        _datasets.TryGetValue(profile, out var loaded)
            ? loaded.Snapshot
            : throw new KeyNotFoundException($"Dataset profile '{profile}' is not available.");

    /// <inheritdoc />
    public string GetDatasetHash(string profile) =>
        _datasets.TryGetValue(profile, out var loaded)
            ? loaded.DatasetHash
            : throw new KeyNotFoundException($"Dataset profile '{profile}' is not available.");

    /// <inheritdoc />
    public DatasetReadiness Validate() =>
        _errors.Count == 0 && _datasets.Count > 0
            ? DatasetReadiness.Ready
            : new DatasetReadiness(false, _datasets.Count == 0
                ? [.. _errors, "No dataset profiles were loaded."]
                : _errors);
}
