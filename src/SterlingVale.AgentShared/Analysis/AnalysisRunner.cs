using System.Diagnostics;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using SterlingVale.AgentShared.Contracts;
using SterlingVale.AgentShared.Prompting;
using SterlingVale.Domain.Analysis;
using SterlingVale.Domain.Model;

namespace SterlingVale.AgentShared.Analysis;

/// <summary>
/// The single orchestration-agnostic runner used by BOTH modes. It runs a pre-built
/// <see cref="AIAgent"/>, aggregates usage across turns, counts turns/tool-calls, enforces the
/// timeout and turn cap, validates the final output against the shared schema, estimates cost, and
/// captures the raw output. Because both modes share this exact runner, the only variable is how the
/// agent was constructed. It never repairs an invalid model answer.
/// </summary>
public sealed class AnalysisRunner
{
    /// <summary>Runs the agent and produces a complete <see cref="RunRecord"/>.</summary>
    public async Task<RunRecord> RunAsync(AIAgent agent, RunContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentNullException.ThrowIfNull(context);

        var stopwatch = Stopwatch.StartNew();
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(context.Model.Timeout);

        AgentResponse? run = null;
        bool timedOut = false;
        try
        {
            run = await agent.RunAsync(context.UserMessage, cancellationToken: timeoutCts.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                throw; // caller-initiated cancellation propagates
            }

            timedOut = true; // our per-run timeout fired
        }

        stopwatch.Stop();

        if (timedOut || run is null)
        {
            return BuildRecord(context, RunStatus.TimedOut, rawOutput: string.Empty, report: null,
                generatedCode: null, metrics: EmptyMetrics(stopwatch.Elapsed));
        }

        var messages = run.Messages;
        int turns = messages.Count(m => m.Role == ChatRole.Assistant);
        var calls = messages.SelectMany(m => m.Contents).OfType<FunctionCallContent>().ToList();
        var byName = calls
            .GroupBy(c => c.Name, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

        string? generatedCode = ExtractGeneratedCode(calls);

        long? input = run.Usage?.InputTokenCount;
        long? output = run.Usage?.OutputTokenCount;
        long? total = run.Usage?.TotalTokenCount ?? (input is not null && output is not null ? input + output : null);
        decimal? cost = context.Pricing.EstimateCost(input, output);

        string raw = run.Text ?? string.Empty;
        bool capped = turns > context.Model.MaxTurns;
        bool valid = !capped && OutputSchema.IsValid(raw);

        ExposureReport? report = null;
        if (valid)
        {
            var dto = ReportJson.TryParse(raw);
            report = dto is not null ? ReportJson.ToDomain(dto) : null;
            valid = report is not null;
        }

        var status = capped ? RunStatus.Capped : valid ? RunStatus.Completed : RunStatus.Failed;

        var metrics = new RunMetrics
        {
            Duration = stopwatch.Elapsed,
            ModelRequestCount = turns,
            ModelTurnCount = turns,
            ToolCallCount = calls.Count,
            ToolCallsByName = byName,
            ExecuteCodeCallCount = byName.GetValueOrDefault("execute_code"),
            InputTokens = input,
            OutputTokens = output,
            TotalTokens = total,
            EstimatedCostUsd = cost,
            RetryCount = 0,
            SchemaValid = valid,
        };

        return BuildRecord(context, status, raw, report, generatedCode, metrics);
    }

    private static string? ExtractGeneratedCode(IEnumerable<FunctionCallContent> calls)
    {
        foreach (var call in calls.Where(c => string.Equals(c.Name, "execute_code", StringComparison.Ordinal)))
        {
            if (call.Arguments is not null &&
                call.Arguments.TryGetValue("code", out var value) &&
                value?.ToString() is { Length: > 0 } code)
            {
                return code;
            }
        }

        return null;
    }

    private static RunMetrics EmptyMetrics(TimeSpan duration) => new()
    {
        Duration = duration,
        SchemaValid = false,
    };

    private static RunRecord BuildRecord(
        RunContext context, RunStatus status, string rawOutput, ExposureReport? report, string? generatedCode, RunMetrics metrics) => new()
        {
            Response = new AnalysisResponse
            {
                RunId = context.RunId,
                Mode = context.Mode,
                Status = status,
                Report = report,
                Metrics = metrics,
            },
            Mode = context.Mode,
            RawModelOutput = rawOutput,
            GeneratedCode = generatedCode,
            Fingerprints = context.Fingerprints,
        };
}
