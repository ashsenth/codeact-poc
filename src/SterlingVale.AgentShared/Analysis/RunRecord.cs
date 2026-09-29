using SterlingVale.Domain.Analysis;

namespace SterlingVale.AgentShared.Analysis;

/// <summary>
/// The complete result of a run: the API-facing response, the raw (untrusted) model output preserved
/// as a benchmark artifact, and the fairness fingerprints. The raw output is captured verbatim and
/// never repaired.
/// </summary>
public sealed record RunRecord
{
    /// <summary>The API-facing analysis response.</summary>
    public required AnalysisResponse Response { get; init; }

    /// <summary>Orchestration mode ("classic" or "codeact").</summary>
    public required string Mode { get; init; }

    /// <summary>The raw final model output, preserved verbatim as a protected artifact.</summary>
    public required string RawModelOutput { get; init; }

    /// <summary>
    /// The model-generated program captured from the <c>execute_code</c> call arguments (CodeAct),
    /// or null when unavailable (Classic, or when the SDK does not surface it).
    /// </summary>
    public string? GeneratedCode { get; init; }

    /// <summary>
    /// The tool results returned to the model (e.g. the sandbox stdout/stderr for each
    /// <c>execute_code</c> call), preserved verbatim for diagnostics; null when none were captured.
    /// </summary>
    public string? ToolResults { get; init; }

    /// <summary>Fairness fingerprints recorded for this run.</summary>
    public required FairnessFingerprints Fingerprints { get; init; }
}
