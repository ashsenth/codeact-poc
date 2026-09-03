namespace SterlingVale.AgentShared.Contracts;

/// <summary>Run metadata written to metadata.json.</summary>
public sealed record RunMetadataDto(
    string RunId,
    string Mode,
    string Status,
    string PromptId,
    string SchemaId,
    string ModelDeployment,
    float Temperature,
    int MaxTurns,
    int TimeoutSeconds,
    decimal PricingInputPerMillion,
    decimal PricingOutputPerMillion,
    decimal? EstimatedCostUsd,
    IReadOnlyDictionary<string, string> Fingerprints,
    bool GeneratedCodeAvailable,
    string GeneratedAtUtc,
    string Runtime);
