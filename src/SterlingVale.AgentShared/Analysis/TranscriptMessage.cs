namespace SterlingVale.AgentShared.Analysis;

/// <summary>One model-visible tool call within a transcript message (name + JSON arguments).</summary>
public sealed record TranscriptCall(string? Id, string Name, string? ArgumentsJson);

/// <summary>One tool result delivered back to the model within a transcript message.</summary>
public sealed record TranscriptResult(string? CallId, string? Name, string? ResultJson);

/// <summary>
/// One message in the run's model conversation: its role, any text, the tool calls the model made,
/// and the tool results returned to it. Built from the SDK message history for the transcript.jsonl
/// artifact so a reviewer can see exactly what the model emitted and what came back.
/// </summary>
public sealed record TranscriptMessage(
    int Index,
    string Role,
    string? Text,
    IReadOnlyList<TranscriptCall> Calls,
    IReadOnlyList<TranscriptResult> Results);
