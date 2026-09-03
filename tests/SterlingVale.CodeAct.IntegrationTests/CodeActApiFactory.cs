using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using SterlingVale.DataGenerator;

namespace SterlingVale.CodeAct.IntegrationTests;

/// <summary>
/// A WebApplicationFactory that generates a deterministic "small" dataset and forces both the
/// offline fake model AND the offline execute_code stand-in (no Hyperlight guest), so the CodeAct
/// API runs end-to-end without a live model or a Hyperlight runtime.
/// </summary>
public sealed class CodeActApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dataRoot;

    /// <summary>The temp artifacts root used by this factory (asserted by tests).</summary>
    public string ArtifactsRoot { get; }

    public CodeActApiFactory()
    {
        _dataRoot = Path.Combine(Path.GetTempPath(), "sterlingvale-codeact-" + Guid.NewGuid().ToString("N"));
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
                ["HYPERLIGHT_PYTHON_GUEST_PATH"] = string.Empty,
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
