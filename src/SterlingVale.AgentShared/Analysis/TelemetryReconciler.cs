using SterlingVale.Domain.Analysis;
using SterlingVale.Telemetry;

namespace SterlingVale.AgentShared.Analysis;

public static class TelemetryReconciler
{
    public static RunMetrics Reconcile(RunMetrics metrics, ExecutionSession session)
    {
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentNullException.ThrowIfNull(session);
        var events = session.Snapshot();
        var usage = session.UsageSnapshot();
        int Count(string type) => events.Count(entry => entry.EventType == type);
        int requests = Count("MODEL_REQUEST_STARTED");
        int responses = Count("MODEL_RESPONSE_RECEIVED");
        int modelTools = Count("MODEL_TOOL_CALL_STARTED");
        int granular = Count("TOOL_CALL_STARTED");
        int code = Count("EXECUTE_CODE_CALL_STARTED");
        var errors = new List<string>();
        if (requests != responses + Count("MODEL_REQUEST_FAILED")) errors.Add("Unfinished model request");
        if (responses != usage.Count) errors.Add("Missing per-response usage record");
        if (modelTools != Count("MODEL_TOOL_CALL_COMPLETED") + Count("MODEL_TOOL_CALL_FAILED")) errors.Add("Unfinished model-facing invocation");
        if (granular != Count("TOOL_CALL_COMPLETED") + Count("TOOL_CALL_FAILED")) errors.Add("Unfinished granular invocation");
        if (code != Count("EXECUTE_CODE_CALL_COMPLETED") + Count("EXECUTE_CODE_CALL_FAILED")) errors.Add("Unfinished code execution");
        if (metrics.ModelRequestCount != requests) errors.Add("Model request count mismatch");
        if (metrics.ModelTurnCount != responses) errors.Add("Model response count mismatch");
        if (metrics.ModelFacingToolCallCount != modelTools) errors.Add("Model-facing tool count mismatch");
        if (metrics.GranularToolCallCount != granular) errors.Add("Granular tool count mismatch");
        if (metrics.ExecuteCodeCallCount != code) errors.Add("Code execution count mismatch");
        long? Sum(Func<ModelUsage, long?> select) => usage.Count > 0 && usage.All(item => select(item).HasValue) ? usage.Sum(item => select(item)!.Value) : null;
        if (metrics.InputTokens != Sum(item => item.InputTokens)) errors.Add("Input token total mismatch");
        if (metrics.OutputTokens != Sum(item => item.OutputTokens)) errors.Add("Output token total mismatch");
        if (metrics.TotalTokens != Sum(item => item.TotalTokens)) errors.Add("Total token total mismatch");
        return metrics with { TelemetryValid = errors.Count == 0, TelemetryErrors = errors };
    }

    public static RunMetrics FromEvidence(ExecutionSession session, TimeSpan duration, bool schemaValid, decimal? cost)
    {
        var events = session.Snapshot();
        var usage = session.UsageSnapshot();
        int Count(string type) => events.Count(entry => entry.EventType == type);
        long? Sum(Func<ModelUsage, long?> select) => usage.Count > 0 && usage.All(item => select(item).HasValue) ? usage.Sum(item => select(item)!.Value) : null;
        int granular = Count("TOOL_CALL_STARTED");
        return Reconcile(new RunMetrics
        {
            Duration = duration, SchemaValid = schemaValid,
            ModelRequestCount = Count("MODEL_REQUEST_STARTED"), ModelTurnCount = Count("MODEL_RESPONSE_RECEIVED"),
            ModelFacingToolCallCount = Count("MODEL_TOOL_CALL_STARTED"), GranularToolCallCount = granular,
            SandboxCallbackCount = session.Context.AgentMode == "codeact" ? granular : 0,
            ToolCallCount = granular, ExecuteCodeCallCount = Count("EXECUTE_CODE_CALL_STARTED"),
            InputTokens = Sum(item => item.InputTokens), OutputTokens = Sum(item => item.OutputTokens),
            TotalTokens = Sum(item => item.TotalTokens), EstimatedCostUsd = cost,
        }, session);
    }
}