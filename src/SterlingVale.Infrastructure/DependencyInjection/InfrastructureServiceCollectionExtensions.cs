using System.Globalization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SterlingVale.AgentShared.Analysis;
using SterlingVale.AgentShared.Configuration;
using SterlingVale.AgentShared.Fakes;
using SterlingVale.Infrastructure.Configuration;
using SterlingVale.Infrastructure.Data;
using SterlingVale.Infrastructure.Model;
using SterlingVale.Infrastructure.Runs;
using SterlingVale.Telemetry;

namespace SterlingVale.Infrastructure;

/// <summary>
/// Registers all shared infrastructure services. Both API composition roots call this and then add
/// only their mode-specific <c>IAgentFactory</c>, keeping the APIs thin.
/// </summary>
public static class InfrastructureServiceCollectionExtensions
{
    /// <summary>Adds dataset loading, the chat-client provider, run store, config, and the analysis service.</summary>
    public static IServiceCollection AddSterlingValeInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var azure = new AzureOpenAIOptions
        {
            Endpoint = configuration["AZURE_OPENAI_ENDPOINT"] ?? string.Empty,
            DeploymentName = configuration["AZURE_OPENAI_DEPLOYMENT_NAME"] ?? "gpt-4o",
            ApiKey = configuration["AZURE_OPENAI_API_KEY"] ?? string.Empty,
        };

        var benchmark = new BenchmarkOptions
        {
            Temperature = 0f,
            MaxTurns = ParsePositiveInt(configuration, "BENCHMARK_MAX_TURNS", 12),
            TimeoutSeconds = ParsePositiveInt(configuration, "BENCHMARK_TIMEOUT_SECONDS", 120),
            PricingInputPerMillion = ParseNonNegativeDecimal(configuration, "PRICING_INPUT_PER_MILLION", 2.50m),
            PricingOutputPerMillion = ParseNonNegativeDecimal(configuration, "PRICING_OUTPUT_PER_MILLION", 10.00m),
        };

        var dataset = new DatasetOptions
        {
            DataRoot = configuration["DATASET_ROOT"] ?? "data",
            Profiles = (configuration["DATASET_PROFILES"] ?? "small,medium,large")
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
        };

        var model = new ModelConfiguration
        {
            DeploymentName = azure.IsConfigured ? azure.DeploymentName : "fake-model",
            Temperature = benchmark.Temperature,
            MaxTurns = benchmark.MaxTurns,
            TimeoutSeconds = benchmark.TimeoutSeconds,
        };

        var pricing = new PricingConfiguration
        {
            InputPerMillion = benchmark.PricingInputPerMillion,
            OutputPerMillion = benchmark.PricingOutputPerMillion,
        };

        var hyperlight = new HyperlightOptions
        {
            GuestPath = configuration["HYPERLIGHT_PYTHON_GUEST_PATH"],
        };

        var artifacts = new ArtifactsOptions
        {
            Root = configuration["ARTIFACTS_ROOT"] ?? "artifacts",
        };

        services.AddSingleton(Options.Create(dataset));
        services.AddSingleton(azure);
        services.AddSingleton(model);
        services.AddSingleton(pricing);
        services.AddSingleton(hyperlight);
        services.AddSingleton(artifacts);
        services.AddSingleton<ToolMetrics>();

        services.AddSingleton<DatasetLoader>();
        services.AddSingleton<ISnapshotProvider, DatasetSnapshotProvider>();
        services.AddSingleton<IRunStore, FileRunStore>();
        services.AddSingleton<AnalysisRunner>();

        if (azure.IsConfigured)
        {
            services.AddSingleton<IChatClientProvider>(new AzureOpenAIChatClientProvider(azure));
        }
        else
        {
            services.AddSingleton<IChatClientProvider, FakeChatClientProvider>();
        }

        services.AddScoped<AnalysisService>();
        return services;
    }

    // Configuration is validated at startup: an unset value uses the default, but a value that is
    // present yet malformed (or out of range) fails fast rather than silently reverting to a default.
    private static int ParsePositiveInt(IConfiguration configuration, string key, int fallback)
    {
        var value = configuration[key];
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            throw new InvalidOperationException($"Configuration '{key}' must be an integer, but was '{value}'.");
        }

        if (parsed <= 0)
        {
            throw new InvalidOperationException($"Configuration '{key}' must be greater than zero, but was {parsed}.");
        }

        return parsed;
    }

    private static decimal ParseNonNegativeDecimal(IConfiguration configuration, string key, decimal fallback)
    {
        var value = configuration[key];
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
        {
            throw new InvalidOperationException($"Configuration '{key}' must be a decimal, but was '{value}'.");
        }

        if (parsed < 0)
        {
            throw new InvalidOperationException($"Configuration '{key}' must not be negative, but was {parsed}.");
        }

        return parsed;
    }
}
