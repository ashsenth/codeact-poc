using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace SterlingVale.Telemetry;

public sealed record ExecutionContext(
    string ComparisonId, string TrialId, string RunId, string CorrelationId,
    string TraceId, string SpanId, string AgentMode, string DatasetProfile)
{
    public bool IsWarmup { get; init; }
}

public sealed record ExecutionEvent(
    string EventId, string SchemaVersion, long SequenceNumber, DateTimeOffset Timestamp,
    string EventType, string CorrelationId, string RunId, string ComparisonId, string TrialId,
    string TraceId, string SpanId, string AgentMode, string DatasetProfile,
    string Status, double? DurationMs, string? ErrorCategory, object? Data)
{
    public bool IsWarmup { get; init; }
}

public sealed record ModelUsage(int ModelTurn, long? InputTokens, long? OutputTokens, long? TotalTokens);

public sealed class ExecutionSession : IDisposable
{
    private static readonly AsyncLocal<ExecutionSession?> Ambient = new();
    private static readonly AsyncLocal<string?> Invocation = new();
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly object _gate = new();
    private readonly List<ExecutionEvent> _events = [];
    private readonly List<ModelUsage> _usage = [];
    private readonly StreamWriter _writer;
    private readonly Activity _activity;
    private readonly ExecutionSession? _previous;
    private bool _disposed;
    private readonly ExecutionLogLevel _logLevel;

    public ExecutionSession(string artifactsRoot, string agentMode, string datasetProfile,
        string? comparisonId = null, string? trialId = null, PayloadPolicy? payloads = null, bool isWarmup = false)
    {
        _logLevel = Enum.TryParse<ExecutionLogLevel>(Environment.GetEnvironmentVariable("EXECUTION_LOG_LEVEL"), true, out var level) ? level : ExecutionLogLevel.Normal;
        Payloads = payloads ?? new PayloadPolicy(
            Enum.TryParse<PayloadCaptureMode>(Environment.GetEnvironmentVariable("EXECUTION_PAYLOAD_CAPTURE"), true, out var capture) ? capture : PayloadCaptureMode.Metadata);
        _previous = Ambient.Value;
        _activity = SterlingValeTelemetry.ActivitySource.StartActivity("agent.run", ActivityKind.Internal)
            ?? new Activity("agent.run").SetIdFormat(ActivityIdFormat.W3C).Start();
        Context = new ExecutionContext(comparisonId ?? Guid.NewGuid().ToString("N"),
            trialId ?? Guid.NewGuid().ToString("N"), Guid.NewGuid().ToString("N"),
            Guid.NewGuid().ToString("N"), _activity.TraceId.ToString(), _activity.SpanId.ToString(),
            agentMode, datasetProfile) { IsWarmup = isWarmup };
        _activity.SetTag("run.id", Context.RunId);
        _activity.SetTag("correlation.id", Context.CorrelationId);
        RunDirectory = Path.Combine(artifactsRoot, "runs", Context.RunId);
        Directory.CreateDirectory(RunDirectory);
        _writer = new StreamWriter(new FileStream(Path.Combine(RunDirectory, "events.jsonl"),
            FileMode.CreateNew, FileAccess.Write, FileShare.Read), new UTF8Encoding(false)) { AutoFlush = true };
        Ambient.Value = this;
        WriteArtifact("metadata.json", JsonSerializer.Serialize(Context, Json));
    }

    public static ExecutionSession? Current => Ambient.Value;
    public ExecutionContext Context { get; }
    public string RunDirectory { get; }
    public PayloadPolicy Payloads { get; }
    public string SandboxRuntime { get; set; } = "not-configured";
    public bool ModelIsLive { get; set; }
    public static string? ParentInvocationId => Invocation.Value;

    public static IDisposable BeginInvocation(string invocationId)
    {
        string? previous = Invocation.Value;
        Invocation.Value = invocationId;
        return new InvocationScope(previous);
    }

    private sealed class InvocationScope(string? previous) : IDisposable
    {
        public void Dispose() => Invocation.Value = previous;
    }
    public void RecordUsage(ModelUsage usage)
    {
        lock (_gate)
        {
            _usage.Add(usage);
        }
    }

    public IReadOnlyList<ModelUsage> UsageSnapshot()
    {
        lock (_gate)
        {
            return _usage.ToArray();
        }
    }

    public void AppendArtifact(string relativePath, object value)
    {
        lock (_gate)
        {
            string path = Path.Combine(RunDirectory, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.AppendAllText(path, JsonSerializer.Serialize(value, Json) + "\n", new UTF8Encoding(false));
        }
    }

    public ExecutionEvent Emit(string eventType, string status = "ok", double? durationMs = null,
        string? errorCategory = null, object? data = null)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var activity = Activity.Current;
            var entry = new ExecutionEvent(Guid.NewGuid().ToString("N"), "1.0", _events.Count + 1,
                DateTimeOffset.UtcNow, eventType, Context.CorrelationId, Context.RunId,
                Context.ComparisonId, Context.TrialId, activity?.TraceId.ToString() ?? Context.TraceId,
                activity?.SpanId.ToString() ?? Context.SpanId, Context.AgentMode, Context.DatasetProfile,
                status, durationMs, errorCategory, data) { IsWarmup = Context.IsWarmup };
            _writer.WriteLine(JsonSerializer.Serialize(entry, Json));
            _events.Add(entry);
            string? line = ExecutionConsole.Format(entry, _logLevel);
            if (line is not null)
            {
                Console.WriteLine(line);
            }
            return entry;
        }
    }

    public IReadOnlyList<ExecutionEvent> Snapshot()
    {
        lock (_gate)
        {
            return _events.ToArray();
        }
    }

    public void FinalizeTimeline()
    {
        lock (_gate)
        {
            WriteArtifact("timeline.json", JsonSerializer.Serialize(_events.Select(entry => new
            {
                entry.SequenceNumber, entry.Timestamp, entry.EventType, entry.Status, entry.CorrelationId,
                entry.RunId, entry.AgentMode, eventReference = $"events.jsonl#eventId={entry.EventId}", entry.Data,
            }), Json));
            WriteArtifact("timeline.md", "# Execution Timeline\n\n" + string.Join("\n", _events.Select(entry =>
                $"- {entry.SequenceNumber}. {entry.Timestamp:O} {entry.EventType} ({entry.Status}) [event {entry.EventId}](events.jsonl)")) + "\n");
        }
    }

    public void WriteArtifact(string relativePath, string content)
    {
        lock (_gate)
        {
            string path = Path.Combine(RunDirectory, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            File.WriteAllText(temporary, content, new UTF8Encoding(false));
            File.Move(temporary, path, overwrite: true);
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            FinalizeTimeline();
            _disposed = true;
            _writer.Dispose();
            Ambient.Value = _previous;
            _activity.Dispose();
        }
    }
}