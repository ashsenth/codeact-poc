namespace SterlingVale.AgentShared.Analysis;

public sealed record FailureInfo(string Category, string Message, int? HttpStatus = null, string? ErrorCode = null)
{
    public static FailureInfo FromException(Exception exception) => exception switch
    {
        TurnLimitReachedException limit => new("TurnLimitReached", limit.Message),
        ModelRequestException request => request.Failure,
        HttpRequestException http => new(exception.GetType().Name,
            $"Model transport request failed (HTTP {(int?)http.StatusCode}). Check networking and endpoint configuration.",
            (int?)http.StatusCode),
        OperationCanceledException => new("Cancelled", "Execution was cancelled or timed out."),
        _ => new(exception.GetType().Name, "Execution failed. Raw exception text is omitted to protect credentials and request data."),
    };
}

public sealed class TurnLimitReachedException(int maxTurns)
    : Exception($"The model request limit of {maxTurns} was reached.");

public sealed class ModelRequestException(FailureInfo failure, Exception inner) : Exception(failure.Message, inner)
{
    public FailureInfo Failure { get; } = failure;
}
