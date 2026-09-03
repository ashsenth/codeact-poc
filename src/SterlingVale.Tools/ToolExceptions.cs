namespace SterlingVale.Tools;

/// <summary>Base type for typed, deterministic tool errors surfaced to the agent.</summary>
public abstract class ToolException(string toolName, string message) : Exception(message)
{
    /// <summary>The tool that produced the error.</summary>
    public string ToolName { get; } = toolName;
}

/// <summary>Raised when a tool argument is missing, malformed, or out of range.</summary>
public sealed class ToolArgumentException(string toolName, string message)
    : ToolException(toolName, message);

/// <summary>Raised when a requested entity (household, account, symbol, rate) does not exist.</summary>
public sealed class ToolNotFoundException(string toolName, string message)
    : ToolException(toolName, message);
