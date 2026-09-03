using System.Globalization;
using Microsoft.Extensions.Options;
using SterlingVale.AgentShared.Analysis;
using SterlingVale.AgentShared.Configuration;
using SterlingVale.AgentShared.Fakes;
using SterlingVale.Infrastructure.Configuration;
using SterlingVale.Infrastructure.Data;
using SterlingVale.Infrastructure.Model;
using SterlingVale.Infrastructure.Runs;
using SterlingVale.Telemetry;

namespace SterlingVale.Benchmark;

/// <summary>
/// Builds the shared services the harness needs, reading the same environment configuration as the
/// APIs. Both modes use the SAME snapshot provider, chat-client provider, runner, model, and pricing
/// — only the agent factory differs.
/// </summary>
public sealed class Composition
{
    public Composition()
    {
        DataRoot = Environment.GetEnvironmentVariable("DATASET_ROOT") ?? "data";
        ArtifactsRoot = Environment.GetEnvironmentVariable("ARTIFACTS_ROOT") ?? "artifacts";
        Trials = ParseInt(Environment.GetEnvironmentVariable("BENCHMARK_TRIALS"), 3);

        var azure = new AzureOpenAIOptions
        {
            Endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT") ?? string.Empty,
            DeploymentName = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME") ?? "gpt-4o",
            ApiKey = Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY") ?? string.Empty,
        };

        Model = new ModelConfiguration
        {
            DeploymentName = azure.IsConfigured ? azure.DeploymentName : "fake-model",
            Temperature = 0f,
            MaxTurns = ParseInt(Environment.GetEnvironmentVariable("BENCHMARK_MAX_TURNS"), 12),
            TimeoutSeconds = ParseInt(Environment.GetEnvironmentVariable("BENCHMARK_TIMEOUT_SECONDS"), 120),
        };

        Pricing = new PricingConfiguration
        {
            InputPerMillion = ParseDecimal(Environment.GetEnvironmentVariable("PRICING_INPUT_PER_MILLION"), 2.50m),
            OutputPerMillion = ParseDecimal(Environment.GetEnvironmentVariable("PRICING_OUTPUT_PER_MILLION"), 10.00m),
        };

        Hyperlight = new HyperlightOptions { GuestPath = Environment.GetEnvironmentVariable("HYPERLIGHT_PYTHON_GUEST_PATH") };

        var datasetOptions = Options.Create(new DatasetOptions { DataRoot = DataRoot, Profiles = ["small", "medium", "large"] });
        Snapshots = new DatasetSnapshotProvider(new DatasetLoader(), datasetOptions);
        ChatClientProvider = azure.IsConfigured
            ? new AzureOpenAIChatClientProvider(azure)
            : new FakeChatClientProvider();
        Runner = new AnalysisRunner();
        ToolMetrics = new ToolMetrics();
        RunStore = new FileRunStore(new ArtifactsOptions { Root = ArtifactsRoot }, Model, Pricing);
        Live = azure.IsConfigured;
    }

    public string DataRoot { get; }

    public string ArtifactsRoot { get; }

    public int Trials { get; }

    public bool Live { get; }

    public ISnapshotProvider Snapshots { get; }

    public IChatClientProvider ChatClientProvider { get; }

    public AnalysisRunner Runner { get; }

    public ToolMetrics ToolMetrics { get; }

    public FileRunStore RunStore { get; }

    public ModelConfiguration Model { get; }

    public PricingConfiguration Pricing { get; }

    public HyperlightOptions Hyperlight { get; }

    /// <summary>Builds an analysis service bound to a specific mode factory.</summary>
    public AnalysisService ServiceFor(IAgentFactory factory) =>
        new(Snapshots, ChatClientProvider, factory, Runner, Model, Pricing, ToolMetrics);

    private static int ParseInt(string? value, int fallback) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : fallback;

    private static decimal ParseDecimal(string? value, decimal fallback) =>
        decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) ? parsed : fallback;
}
