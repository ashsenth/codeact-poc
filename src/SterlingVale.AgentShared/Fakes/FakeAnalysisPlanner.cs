using SterlingVale.AgentShared.Contracts;
using SterlingVale.Application.Oracle;
using SterlingVale.Application.Portfolio;
using SterlingVale.Domain.Analysis;

namespace SterlingVale.AgentShared.Fakes;

/// <summary>
/// Synthesizes a deterministic fake-model plan for offline runs. To produce plausible, schema-valid
/// output without a live model, it uses the reference oracle to compute the report and emits it as
/// the model's answer, after a representative tool call. This makes offline runs a useful control
/// (both modes emit identical, correct output), never a substitute for measuring a real model.
/// </summary>
public static class FakeAnalysisPlanner
{
    private static readonly DateTimeOffset FixedGeneratedAt = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>Builds a plan that calls a representative tool, then returns the oracle report JSON.</summary>
    public static FakeChatPlan BuildPlan(PortfolioSnapshot snapshot, AnalysisRequest request, string runId, string mode)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(request);

        var report = new ExposureOracle().Analyze(snapshot, request, runId, FixedGeneratedAt);
        string json = ReportJson.Serialize(report);

        // Target the tool surface each mode exposes: Classic calls a granular tool directly;
        // CodeAct calls execute_code (a valid standalone program a real sandbox can run cleanly).
        if (string.Equals(mode, "codeact", StringComparison.OrdinalIgnoreCase))
        {
            return new FakeChatPlan()
                .CallTool("execute_code", new Dictionary<string, object?> { ["code"] = "print('sterling-vale codeact')" })
                .Respond(json);
        }

        return new FakeChatPlan()
            .CallTool("list_households")
            .Respond(json);
    }
}
