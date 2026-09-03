using SterlingVale.AgentShared.Configuration;
using SterlingVale.AgentShared.Prompting;

namespace SterlingVale.AgentShared.Analysis;

/// <summary>
/// The set of fairness fingerprints recorded on every run. A comparison FAILS if any of these
/// differ between the paired Classic and CodeAct runs (except the intentional orchestration wiring).
/// </summary>
public sealed record FairnessFingerprints
{
    /// <summary>SHA-256 of the shared prompt text.</summary>
    public required string PromptHash { get; init; }

    /// <summary>SHA-256 of the output JSON schema text.</summary>
    public required string SchemaHash { get; init; }

    /// <summary>Fingerprint of the tool catalog (names + descriptions + schemas).</summary>
    public required string ToolFingerprint { get; init; }

    /// <summary>Fingerprint of the model configuration and limits.</summary>
    public required string ModelFingerprint { get; init; }

    /// <summary>Fingerprint of the pricing configuration.</summary>
    public required string PricingFingerprint { get; init; }

    /// <summary>Hash of the dataset manifest snapshot.</summary>
    public required string DatasetHash { get; init; }

    /// <summary>Builds the fingerprints from the shared prompt/schema plus the supplied inputs.</summary>
    public static FairnessFingerprints Create(
        string toolFingerprint,
        string datasetHash,
        ModelConfiguration model,
        PricingConfiguration pricing) => new()
        {
            PromptHash = Hashing.Sha256Hex(Prompt.Shared),
            SchemaHash = Hashing.Sha256Hex(OutputSchema.SchemaText),
            ToolFingerprint = toolFingerprint,
            ModelFingerprint = model.Fingerprint(),
            PricingFingerprint = pricing.Fingerprint(),
            DatasetHash = datasetHash,
        };

    /// <summary>Returns the fingerprints as a stable ordered dictionary for reporting.</summary>
    public IReadOnlyDictionary<string, string> ToDictionary() => new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["prompt"] = PromptHash,
        ["schema"] = SchemaHash,
        ["tools"] = ToolFingerprint,
        ["model"] = ModelFingerprint,
        ["pricing"] = PricingFingerprint,
        ["dataset"] = DatasetHash,
    };
}

/// <summary>Immutable inputs for a single analysis run, shared by both orchestration modes.</summary>
public sealed record RunContext
{
    /// <summary>Unique run identifier.</summary>
    public required string RunId { get; init; }

    /// <summary>Orchestration mode ("classic" or "codeact").</summary>
    public required string Mode { get; init; }

    /// <summary>The user message that starts the run.</summary>
    public required string UserMessage { get; init; }

    /// <summary>Model and limit configuration.</summary>
    public required ModelConfiguration Model { get; init; }

    /// <summary>Pricing configuration.</summary>
    public required PricingConfiguration Pricing { get; init; }

    /// <summary>Fairness fingerprints for this run.</summary>
    public required FairnessFingerprints Fingerprints { get; init; }
}
