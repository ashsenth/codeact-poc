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
    public float? Temperature { get; init; } = 0f;
    public string ApiMode { get; init; } = "chat-completions";
    public string? ModelName { get; init; }
    public string? ModelVersion { get; init; }
    public string DefaultReasoningEffort { get; init; } = "not-supported";
    public bool? Store => ApiMode == "responses" ? false : null;
    public string ContextPolicy => ApiMode == "responses" ? "full-context/no-previous-response-id/v1" : "chat-history";

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
    public string TurnLimitEnforcement => "model-request-boundary/v1";
    public string FunctionInvocationPolicy => "sequential-within-response/v1";

    public string DescribeOutputSettings() =>
        $"Model output: response_format={ResponseFormat}; parallel_tool_calls={AllowMultipleToolCalls.ToString().ToLowerInvariant()}; " +
        $"strict={ResponseSchemaStrict.ToString().ToLowerInvariant()}; validation=local {ResponseSchemaId}";

    public ChatOptions CreateChatOptions(string instructions, IList<AITool>? tools = null) => new()
    {
        ModelId = DeploymentName,
        Instructions = instructions,
        Temperature = Temperature,
        Reasoning = DefaultReasoningEffort == "medium" ? new ReasoningOptions { Effort = ReasoningEffort.Medium } : null,
        MaxOutputTokens = MaxOutputTokens,
        ResponseFormat = ChatResponseFormat.ForJsonSchema(StructuredOutput.Schema, StructuredOutput.SchemaName),
        AdditionalProperties = new() { ["strict"] = ResponseSchemaStrict },
        AllowMultipleToolCalls = AllowMultipleToolCalls,
        Tools = tools,
    };

    /// <summary>A deterministic fingerprint over all model/limit fields.</summary>
    public string Fingerprint() => Hashing.Sha256Hex(string.Create(
        CultureInfo.InvariantCulture,
        $"deployment={DeploymentName};temperature={Temperature:R};maxTurns={MaxTurns};turnLimitEnforcement={TurnLimitEnforcement};timeoutSeconds={TimeoutSeconds};" +
        $"responseFormat={ResponseFormat};responseSchema={ResponseSchemaHash};strict={ResponseSchemaStrict};" +
        $"parallel_tool_calls={AllowMultipleToolCalls};maxOutputTokens={MaxOutputTokens};" +
        $"api={ApiMode};model={ModelName};version={ModelVersion};defaultReasoningEffort={DefaultReasoningEffort};store={Store};context={ContextPolicy};" +
        $"functionInvocationPolicy={FunctionInvocationPolicy}"));
}

/// <summary>Token pricing (USD per 1,000,000 tokens). Both modes must use identical values.</summary>
public sealed record PricingConfiguration
{
    /// <summary>USD per million input tokens.</summary>
    public decimal InputPerMillion { get; init; }

    /// <summary>USD per million output tokens.</summary>
    public decimal OutputPerMillion { get; init; }
    public decimal? CachedInputPerMillion { get; init; }
    public decimal? CacheWritePerMillion { get; init; }
    public int? LongContextThreshold { get; init; }
    public decimal? LongInputPerMillion { get; init; }
    public decimal? LongOutputPerMillion { get; init; }
    public decimal? LongCachedInputPerMillion { get; init; }
    public decimal? LongCacheWritePerMillion { get; init; }

    public string? SourceUrl { get; init; }
    public string? VerifiedOn { get; init; }
    public string? Basis { get; init; }

    /// <summary>Estimates cost in USD for the given token counts, or null when usage is unknown.</summary>
    public decimal? EstimateCost(long? inputTokens, long? outputTokens, long? cachedInputTokens = 0, long cacheWriteTokens = 0)
    {
        if (inputTokens is null || outputTokens is null)
        {
            return null;
        }

        bool longContext = LongContextThreshold is { } threshold && inputTokens > threshold;
        decimal input = longContext ? LongInputPerMillion ?? throw new InvalidOperationException("Missing long-context input rate.") : InputPerMillion;
        decimal output = longContext ? LongOutputPerMillion ?? throw new InvalidOperationException("Missing long-context output rate.") : OutputPerMillion;
        decimal? cached = longContext ? LongCachedInputPerMillion : CachedInputPerMillion;
        decimal? write = longContext ? LongCacheWritePerMillion : CacheWritePerMillion;
        if (cached is not null && cachedInputTokens is null)
            return null;
        long cachedTokens = cachedInputTokens ?? 0;
        if (cachedTokens < 0 || cacheWriteTokens < 0 || cachedTokens + cacheWriteTokens > inputTokens)
            throw new InvalidOperationException("Cache token counts exceed input usage.");
        if (cacheWriteTokens > 0 && write is null)
            throw new InvalidOperationException("Missing cache-write rate.");
        return ((inputTokens.Value - cachedTokens - cacheWriteTokens) * input
            + cachedTokens * (cached ?? input) + cacheWriteTokens * (write ?? input)
            + outputTokens.Value * output) / 1_000_000m;
    }

    public decimal? EstimateCost(IReadOnlyList<SterlingVale.Telemetry.ModelUsage> usage) =>
        usage.Count > 0 && usage.All(item => item.InputTokens is not null && item.OutputTokens is not null)
            ? SumCosts(usage) : null;

    private decimal? SumCosts(IReadOnlyList<SterlingVale.Telemetry.ModelUsage> usage)
    {
        decimal total = 0;
        foreach (var item in usage)
        {
            var cost = EstimateCost(item.InputTokens, item.OutputTokens, item.CachedInputTokens, item.CacheWriteTokens);
            if (cost is null) return null;
            total += cost.Value;
        }
        return total;
    }

    /// <summary>A deterministic fingerprint over the pricing fields.</summary>
    public string Fingerprint() => Hashing.Sha256Hex(
        string.Create(CultureInfo.InvariantCulture, $"in={InputPerMillion:G29};out={OutputPerMillion:G29}") +
        (CachedInputPerMillion is null && CacheWritePerMillion is null && LongContextThreshold is null ? "" :
        string.Create(CultureInfo.InvariantCulture,
            $";cached={CachedInputPerMillion:G29};write={CacheWritePerMillion:G29};longThreshold={LongContextThreshold};" +
            $"longIn={LongInputPerMillion:G29};longOut={LongOutputPerMillion:G29};longCached={LongCachedInputPerMillion:G29};longWrite={LongCacheWritePerMillion:G29}")));
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
