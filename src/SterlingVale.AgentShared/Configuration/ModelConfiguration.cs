using System.Globalization;
using System.Text.Json.Serialization;
using Microsoft.Extensions.AI;
using SterlingVale.AgentShared.Prompting;

namespace SterlingVale.AgentShared.Configuration;

/// <summary>
/// Model and run-limit configuration. Both modes must use identical values; the
/// <see cref="Fingerprint"/> is asserted equal across a comparison.
/// </summary>
public sealed record ModelConfiguration
{
    /// <summary>The Azure OpenAI deployment / model id.</summary>
    public required string DeploymentName { get; init; }

    /// <summary>Sampling temperature (0 for determinism when supported).</summary>
    public float Temperature { get; init; }

    /// <summary>Maximum model turns before a run is capped.</summary>
    public int MaxTurns { get; init; } = 12;

    /// <summary>Per-run timeout in seconds.</summary>
    public int TimeoutSeconds { get; init; } = 120;

    /// <summary>The per-run timeout as a <see cref="TimeSpan"/>.</summary>
    public TimeSpan Timeout => TimeSpan.FromSeconds(TimeoutSeconds);

    public string ResponseFormat => "json_schema";
    public string ResponseSchemaId => OutputSchema.SchemaId;
    public string ResponseSchemaHash => StructuredOutput.SchemaHash;
    [JsonPropertyName("strict")]
    public bool ResponseSchemaStrict => false;
    [JsonPropertyName("parallel_tool_calls")]
    public bool AllowMultipleToolCalls => true;
    public int MaxOutputTokens => 16384;

    public string DescribeOutputSettings() =>
        $"Model output: response_format={ResponseFormat}; parallel_tool_calls={AllowMultipleToolCalls.ToString().ToLowerInvariant()}; " +
        $"strict={ResponseSchemaStrict.ToString().ToLowerInvariant()}; validation=local {ResponseSchemaId}";

    public ChatOptions CreateChatOptions(string instructions, IList<AITool>? tools = null) => new()
    {
        ModelId = DeploymentName,
        Instructions = instructions,
        Temperature = Temperature,
        MaxOutputTokens = MaxOutputTokens,
        ResponseFormat = ChatResponseFormat.ForJsonSchema(StructuredOutput.Schema, StructuredOutput.SchemaName),
        AdditionalProperties = new() { ["strict"] = ResponseSchemaStrict },
        AllowMultipleToolCalls = AllowMultipleToolCalls,
        Tools = tools,
    };

    /// <summary>A deterministic fingerprint over all model/limit fields.</summary>
    public string Fingerprint() => Hashing.Sha256Hex(string.Create(
        CultureInfo.InvariantCulture,
        $"deployment={DeploymentName};temperature={Temperature:R};maxTurns={MaxTurns};timeoutSeconds={TimeoutSeconds};" +
        $"responseFormat={ResponseFormat};responseSchema={ResponseSchemaHash};strict={ResponseSchemaStrict};" +
        $"parallel_tool_calls={AllowMultipleToolCalls};maxOutputTokens={MaxOutputTokens}"));
}

/// <summary>Token pricing (USD per 1,000,000 tokens). Both modes must use identical values.</summary>
public sealed record PricingConfiguration
{
    /// <summary>USD per million input tokens.</summary>
    public decimal InputPerMillion { get; init; }

    /// <summary>USD per million output tokens.</summary>
    public decimal OutputPerMillion { get; init; }

    public string? SourceUrl { get; init; }
    public string? VerifiedOn { get; init; }
    public string? Basis { get; init; }

    /// <summary>Estimates cost in USD for the given token counts, or null when usage is unknown.</summary>
    public decimal? EstimateCost(long? inputTokens, long? outputTokens)
    {
        if (inputTokens is null || outputTokens is null)
        {
            return null;
        }

        return (inputTokens.Value / 1_000_000m * InputPerMillion)
             + (outputTokens.Value / 1_000_000m * OutputPerMillion);
    }

    /// <summary>A deterministic fingerprint over the pricing fields.</summary>
    public string Fingerprint() => Hashing.Sha256Hex(string.Create(
        CultureInfo.InvariantCulture,
        $"in={InputPerMillion};out={OutputPerMillion}"));
}

/// <summary>
/// Hyperlight guest configuration. When <see cref="GuestPath"/> is empty, the CodeAct API uses the
/// offline <c>execute_code</c> stand-in instead of a real sandbox.
/// </summary>
public sealed record HyperlightOptions
{
    /// <summary>Absolute path to the Hyperlight guest module, or null/empty when unavailable.</summary>
    public string? GuestPath { get; init; }

    /// <summary>True when a guest path is configured.</summary>
    public bool HasGuest => !string.IsNullOrWhiteSpace(GuestPath);
}
