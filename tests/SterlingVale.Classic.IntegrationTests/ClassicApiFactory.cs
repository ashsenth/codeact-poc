using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using SterlingVale.DataGenerator;

namespace SterlingVale.Classic.IntegrationTests;

/// <summary>
/// A WebApplicationFactory that generates a deterministic "small" dataset into a temp directory and
/// forces the offline fake model, so the Classic API runs end-to-end without live model access.
/// </summary>
public sealed class ClassicApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dataRoot;

    /// <summary>The temp artifacts root used by this factory (asserted by tests).</summary>
    public string ArtifactsRoot { get; }

    public ClassicApiFactory()
    {
        _dataRoot = Path.Combine(Path.GetTempPath(), "sterlingvale-classic-" + Guid.NewGuid().ToString("N"));
        ArtifactsRoot = Path.Combine(_dataRoot, "artifacts");
        var profile = DatasetProfile.Small;
        var dataset = new SyntheticDataGenerator().Generate(profile);
        new DatasetWriter().Write(dataset, Path.Combine(_dataRoot, profile.Name));
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureHostConfiguration(config =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DATASET_ROOT"] = _dataRoot,
                ["DATASET_PROFILES"] = "small",
                ["AZURE_OPENAI_ENDPOINT"] = string.Empty,
                ["ARTIFACTS_ROOT"] = ArtifactsRoot,
            });
        });
        return base.CreateHost(builder);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && Directory.Exists(_dataRoot))
        {
            try
            {
                Directory.Delete(_dataRoot, recursive: true);
            }
            catch (IOException)
            {
                // Best-effort cleanup.
            }
        }
    }
}
