using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SterlingVale.AgentShared.Contracts;
using SterlingVale.AgentShared.Prompting;
using SterlingVale.Domain.Analysis;
using SterlingVale.Domain.Model;
using SterlingVale.Tools.Observability;

namespace SterlingVale.AgentShared.Analysis;

/// <summary>
/// The single orchestration-agnostic runner used by BOTH modes. It runs a pre-built
/// <see cref="AIAgent"/>, aggregates usage across turns, counts turns/tool-calls, enforces the
/// timeout and turn cap, validates the final output against the shared schema, estimates cost, and
/// captures the raw output, the model transcript, and every host-side tool call. Because both modes
/// share this exact runner, the only variable is how the agent was constructed. It never repairs an
/// invalid model answer.
/// </summary>
public sealed partial class AnalysisRunner(ILogger<AnalysisRunner>? logger = null)
{
    private const int LogFieldLimit = 500;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly ILogger _logger = logger ?? NullLogger<AnalysisRunner>.Instance;

    /// <summary>Runs the agent and produces a complete <see cref="RunRecord"/>.</summary>
    public async Task<RunRecord> RunAsync(AIAgent agent, RunContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentNullException.ThrowIfNull(context);

        LogStarting(context.RunId, context.Mode);

        var recorder = new ToolCallRecorder();
        var stopwatch = Stopwatch.StartNew();
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(context.Model.Timeout);

        AgentResponse? run = null;
        bool timedOut = false;
        try
        {
            using (ToolCallRecorder.Activate(recorder))
            {
                run = await agent.RunAsync(context.UserMessage, cancellationToken: timeoutCts.Token)
                    .ConfigureAwait(false);
            }
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
        var toolCalls = recorder.Snapshot();

        if (timedOut || run is null)
        {
            LogTimedOut(context.RunId, context.Mode, context.Model.Timeout.TotalSeconds, toolCalls.Count);
            return BuildRecord(context, RunStatus.TimedOut, rawOutput: string.Empty, report: null,
                generatedCode: null, toolResults: null, toolCalls: toolCalls, transcript: [],
                metrics: EmptyMetrics(stopwatch.Elapsed));
        }

        var messages = run.Messages;
        int turns = messages.Count(m => m.Role == ChatRole.Assistant);
        var calls = messages.SelectMany(m => m.Contents).OfType<FunctionCallContent>().ToList();
        var byName = calls
            .GroupBy(c => c.Name, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

        string? generatedCode = ExtractGeneratedCode(calls);
        string? toolResults = ExtractToolResults(messages);
        var transcript = BuildTranscript(messages);

        string raw = run.Text ?? string.Empty;

        long? input = run.Usage?.InputTokenCount;
        long? output = run.Usage?.OutputTokenCount;
        long? total = run.Usage?.TotalTokenCount ?? (input is not null && output is not null ? input + output : null);
        decimal? cost = context.Pricing.EstimateCost(input, output);

        bool capped = turns > context.Model.MaxTurns;
        bool valid = !capped && OutputSchema.IsValid(raw);

        ExposureReport? report = null;
        if (valid)
        {
            var dto = ReportJson.TryParse(raw);
            report = dto is not null ? ReportJson.ToDomain(dto) : null;
            // A schema-valid but empty households array is an analytically empty result (e.g. a stub
            // the model salvaged after a sandbox failure); treat it as a failure, not a pass.
            valid = report is not null && report.Households.Count > 0;
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

        LogRunDetail(context, status, turns, toolCalls, byName.GetValueOrDefault("execute_code"), total);
        return BuildRecord(context, status, raw, report, generatedCode, toolResults, toolCalls, transcript, metrics);
    }

    private void LogRunDetail(
        RunContext context, RunStatus status, int turns, IReadOnlyList<ToolCallRecord> toolCalls, int executeCodeCalls, long? totalTokens)
    {
        LogCompleted(context.RunId, context.Mode, status, turns, toolCalls.Count, executeCodeCalls, totalTokens);

        if (!_logger.IsEnabled(LogLevel.Debug))
        {
            return;
        }

        foreach (var call in toolCalls)
        {
            LogToolCall(call.Seq, call.Name, call.Status, call.DurationMs, Truncate(call.ArgumentsJson), Truncate(call.ResultJson));
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Run {RunId} ({Mode}) starting")]
    private partial void LogStarting(string runId, string mode);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Run {RunId} ({Mode}) timed out after {TimeoutSeconds}s ({ToolCalls} tool calls captured)")]
    private partial void LogTimedOut(string runId, string mode, double timeoutSeconds, int toolCalls);

    [LoggerMessage(Level = LogLevel.Information, Message = "Run {RunId} ({Mode}) {Status}: turns={Turns} toolCalls={ToolCalls} executeCode={ExecuteCode} tokens={Tokens}")]
    private partial void LogCompleted(string runId, string mode, RunStatus status, int turns, int toolCalls, int executeCode, long? tokens);

    [LoggerMessage(Level = LogLevel.Debug, Message = "tool[{Seq}] {Name} {Status} {DurationMs}ms args={Args} result={Result}")]
    private partial void LogToolCall(int seq, string name, string status, double durationMs, string args, string result);

    private static string Truncate(string? value) =>
        value is null ? string.Empty
        : value.Length <= LogFieldLimit ? value
        : value[..LogFieldLimit] + "…";

    private static string? ExtractToolResults(IEnumerable<ChatMessage> messages)
    {
        var results = messages
            .SelectMany(m => m.Contents)
            .OfType<FunctionResultContent>()
            .Select(r => r.Result?.ToString())
            .Where(s => !string.IsNullOrEmpty(s))
            .ToList();

        return results.Count == 0 ? null : string.Join("\n---\n", results);
    }

    private static string? ExtractGeneratedCode(IReadOnlyList<FunctionCallContent> calls)
    {
        var programs = calls
            .Where(c => string.Equals(c.Name, "execute_code", StringComparison.Ordinal))
            .Select(c => c.Arguments is not null && c.Arguments.TryGetValue("code", out var value) ? value?.ToString() : null)
            .Where(code => !string.IsNullOrEmpty(code))
            .ToList();

        if (programs.Count == 0)
        {
            return null;
        }

        if (programs.Count == 1)
        {
            return programs[0];
        }

        // CodeAct chunks its output across several execute_code calls; keep every program, delimited.
        var sb = new StringBuilder();
        for (int i = 0; i < programs.Count; i++)
        {
            sb.Append("# === execute_code program ").Append(i + 1).Append(" of ").Append(programs.Count).Append(" ===\n");
            sb.Append(programs[i]).Append('\n');
        }

        return sb.ToString();
    }

    private static IReadOnlyList<TranscriptMessage> BuildTranscript(IEnumerable<ChatMessage> messages)
    {
        var transcript = new List<TranscriptMessage>();
        int index = 0;
        foreach (var message in messages)
        {
            var calls = message.Contents.OfType<FunctionCallContent>()
                .Select(c => new TranscriptCall(c.CallId, c.Name, SerializeValue(c.Arguments)))
                .ToList();
            var results = message.Contents.OfType<FunctionResultContent>()
                .Select(r => new TranscriptResult(r.CallId, null, SerializeValue(r.Result)))
                .ToList();
            string? text = message.Text is { Length: > 0 } t ? t : null;
            transcript.Add(new TranscriptMessage(index++, message.Role.Value, text, calls, results));
        }

        return transcript;
    }

    private static string? SerializeValue(object? value)
    {
        if (value is null)
        {
            return null;
        }

        try
        {
            return value is string s ? s : JsonSerializer.Serialize(value, Json);
        }
        catch (NotSupportedException)
        {
            return value.ToString();
        }
    }

    private static RunMetrics EmptyMetrics(TimeSpan duration) => new()
    {
        Duration = duration,
        SchemaValid = false,
    };

    private static RunRecord BuildRecord(
        RunContext context, RunStatus status, string rawOutput, ExposureReport? report, string? generatedCode,
        string? toolResults, IReadOnlyList<ToolCallRecord> toolCalls, IReadOnlyList<TranscriptMessage> transcript,
        RunMetrics metrics) => new()
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
            ToolResults = toolResults,
            ToolCalls = toolCalls,
            Transcript = transcript,
            Fingerprints = context.Fingerprints,
        };
}
