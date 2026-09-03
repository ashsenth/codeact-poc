using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using SterlingVale.DataGenerator;

namespace SterlingVale.CodeAct.IntegrationTests;

/// <summary>
/// LIVE-model factory for CodeAct: leaves Azure OpenAI settings to the environment and forces the
/// offline <c>execute_code</c> stand-in (no guest) so a live run exercises the model + CodeAct
/// orchestration without needing a Hyperlight runtime. Applies conservative caps.
/// </summary>
public sealed class CodeActLiveApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dataRoot;

    public CodeActLiveApiFactory()
    {
        _dataRoot = Path.Combine(Path.GetTempPath(), "sterlingvale-codeact-live-" + Guid.NewGuid().ToString("N"));
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
                ["HYPERLIGHT_PYTHON_GUEST_PATH"] = string.Empty,
                ["BENCHMARK_MAX_TURNS"] = "6",
                ["BENCHMARK_TIMEOUT_SECONDS"] = "60",
            });
        });
        return base.CreateHost(builder);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        Cleanup(_dataRoot, disposing);
    }

    internal static void Cleanup(string dir, bool disposing)
    {
        if (disposing && Directory.Exists(dir))
        {
            try
            {
                Directory.Delete(dir, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }
}

/// <summary>
/// Gated factory for Hyperlight RUNTIME end-to-end tests: forces the offline fake model (no Azure)
/// but leaves <c>HYPERLIGHT_PYTHON_GUEST_PATH</c> to the environment, so <c>execute_code</c> runs in
/// the real micro-VM when a guest is available.
/// </summary>
public sealed class CodeActGuestApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dataRoot;

    public CodeActGuestApiFactory()
    {
        _dataRoot = Path.Combine(Path.GetTempPath(), "sterlingvale-codeact-guest-" + Guid.NewGuid().ToString("N"));
        new DatasetWriter().Write(new SyntheticDataGenerator().Generate(DatasetProfile.Small), Path.Combine(_dataRoot, "small"));
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.UseEnvironment("HyperlightTesting");
        builder.ConfigureHostConfiguration(config =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DATASET_ROOT"] = _dataRoot,
                ["DATASET_PROFILES"] = "small",
                ["ARTIFACTS_ROOT"] = Path.Combine(_dataRoot, "artifacts"),
                ["AZURE_OPENAI_ENDPOINT"] = string.Empty,
                // HYPERLIGHT_PYTHON_GUEST_PATH intentionally NOT set: flows from the environment.
            });
        });
        return base.CreateHost(builder);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        CodeActLiveApiFactory.Cleanup(_dataRoot, disposing);
    }
}
