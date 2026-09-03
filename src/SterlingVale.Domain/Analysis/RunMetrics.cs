namespace SterlingVale.Domain.Analysis;

/// <summary>
/// Metrics captured for a single analysis run. Token counts are nullable: when the model provider
/// does not report usage, the value stays <c>null</c> (unknown) and is NEVER coerced to zero.
/// </summary>
public sealed record RunMetrics
{
    /// <summary>Wall-clock duration of the run.</summary>
    public required TimeSpan Duration { get; init; }

    /// <summary>Number of model requests issued.</summary>
    public int ModelRequestCount { get; init; }

    /// <summary>Number of model turns (assistant responses).</summary>
    public int ModelTurnCount { get; init; }

    /// <summary>Total number of tool calls across all turns.</summary>
    public int ToolCallCount { get; init; }

    /// <summary>Tool-call counts keyed by tool name.</summary>
    public IReadOnlyDictionary<string, int> ToolCallsByName { get; init; } =
        new Dictionary<string, int>(StringComparer.Ordinal);

    /// <summary>Number of <c>execute_code</c> calls (CodeAct only; 0 for Classic).</summary>
    public int ExecuteCodeCallCount { get; init; }

    /// <summary>Input tokens, or null when the provider did not report usage.</summary>
    public long? InputTokens { get; init; }

    /// <summary>Output tokens, or null when the provider did not report usage.</summary>
    public long? OutputTokens { get; init; }

    /// <summary>Total tokens, or null when the provider did not report usage.</summary>
    public long? TotalTokens { get; init; }

    /// <summary>Estimated cost in USD, or null when token usage is unknown.</summary>
    public decimal? EstimatedCostUsd { get; init; }

    /// <summary>Number of retries recorded (never silently swallowed).</summary>
    public int RetryCount { get; init; }

    /// <summary>Whether the final output validated against the shared schema.</summary>
    public bool SchemaValid { get; init; }
}
