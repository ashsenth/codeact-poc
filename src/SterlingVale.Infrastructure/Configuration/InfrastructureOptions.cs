namespace SterlingVale.Infrastructure.Configuration;

/// <summary>Where to find the generated dataset profiles on disk.</summary>
public sealed class DatasetOptions
{
    /// <summary>Configuration section name.</summary>
    public const string Section = "Dataset";

    /// <summary>Root directory containing per-profile subfolders (small/medium/large).</summary>
    public string DataRoot { get; set; } = "data";

    /// <summary>Profiles to load at startup.</summary>
    public IReadOnlyList<string> Profiles { get; set; } = ["small", "medium", "large"];
}

/// <summary>Azure OpenAI connection settings. When endpoint is empty, the offline fake model is used.</summary>
public sealed class AzureOpenAIOptions
{
    /// <summary>Configuration section name.</summary>
    public const string Section = "AzureOpenAI";

    /// <summary>The Azure OpenAI endpoint. Empty ⇒ offline fake model.</summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>The deployment/model name.</summary>
    public string DeploymentName { get; set; } = "gpt-4o";

    /// <summary>Optional API key. Prefer DefaultAzureCredential (leave empty) when possible.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>True when a live endpoint is configured.</summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(Endpoint);
}

/// <summary>Token pricing and run limits.</summary>
public sealed class BenchmarkOptions
{
    /// <summary>Configuration section name.</summary>
    public const string Section = "Benchmark";

    /// <summary>Sampling temperature.</summary>
    public float Temperature { get; set; }

    /// <summary>Maximum model turns before a run is capped.</summary>
    public int MaxTurns { get; set; } = 12;

    /// <summary>Per-run timeout in seconds.</summary>
    public int TimeoutSeconds { get; set; } = 120;

    /// <summary>USD per million input tokens.</summary>
    public decimal PricingInputPerMillion { get; set; } = 2.50m;

    /// <summary>USD per million output tokens.</summary>
    public decimal PricingOutputPerMillion { get; set; } = 10.00m;
}

/// <summary>Where per-run artifacts are written.</summary>
public sealed class ArtifactsOptions
{
    /// <summary>Configuration section name.</summary>
    public const string Section = "Artifacts";

    /// <summary>Root directory for artifacts (runs are written under <c>{Root}/runs/{runId}</c>).</summary>
    public string Root { get; set; } = "artifacts";
}
