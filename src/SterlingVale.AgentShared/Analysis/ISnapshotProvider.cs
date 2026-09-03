using SterlingVale.Application.Portfolio;

namespace SterlingVale.AgentShared.Analysis;

/// <summary>Readiness status of the dataset(s) backing an application.</summary>
public sealed record DatasetReadiness(bool IsReady, IReadOnlyList<string> Errors)
{
    /// <summary>A ready, error-free status.</summary>
    public static DatasetReadiness Ready { get; } = new(true, []);
}

/// <summary>
/// Supplies validated, indexed portfolio snapshots and their dataset hashes. Implemented by
/// Infrastructure; consumed by the shared analysis service so the APIs stay thin.
/// </summary>
public interface ISnapshotProvider
{
    /// <summary>The dataset profiles available (e.g. "small", "medium", "large").</summary>
    IReadOnlyList<string> AvailableProfiles { get; }

    /// <summary>Returns the snapshot for a profile, throwing when the profile is unknown.</summary>
    PortfolioSnapshot GetSnapshot(string profile);

    /// <summary>Returns the dataset manifest hash for a profile.</summary>
    string GetDatasetHash(string profile);

    /// <summary>Validates all loaded datasets for readiness checks.</summary>
    DatasetReadiness Validate();
}
