using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using SterlingVale.DataGenerator;

namespace SterlingVale.Classic.IntegrationTests;

/// <summary>
/// A factory for LIVE-model tests: it generates a small dataset and applies conservative caps, but
/// leaves Azure OpenAI settings to the process environment (so a real deployment is used only when
/// explicitly configured). Never forces the fake model.
/// </summary>
public sealed class ClassicLiveApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dataRoot;

    public ClassicLiveApiFactory()
    {
        _dataRoot = Path.Combine(Path.GetTempPath(), "sterlingvale-classic-live-" + Guid.NewGuid().ToString("N"));
        new DatasetWriter().Write(new SyntheticDataGenerator().Generate(DatasetProfile.Small), Path.Combine(_dataRoot, "small"));
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.UseEnvironment("LiveTesting");
        builder.ConfigureHostConfiguration(config =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DATASET_ROOT"] = _dataRoot,
                ["DATASET_PROFILES"] = "small",
                ["ARTIFACTS_ROOT"] = Path.Combine(_dataRoot, "artifacts"),
                // Cost/iteration caps for live runs.
                ["BENCHMARK_MAX_TURNS"] = "6",
                ["BENCHMARK_TIMEOUT_SECONDS"] = "60",
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
            }
        }
    }
}
