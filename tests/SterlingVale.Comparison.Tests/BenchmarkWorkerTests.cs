using System.Diagnostics;
using System.Text.Json;
using SterlingVale.AgentShared.Analysis;
using SterlingVale.Benchmark;
using SterlingVale.DataGenerator;
using SterlingVale.Domain.Analysis;
using SterlingVale.Telemetry;
using Xunit;

namespace SterlingVale.Comparison.Tests;

[Collection("environment")]
public sealed class BenchmarkWorkerTests
{
    [Theory]
    [InlineData("classic")]
    [InlineData("codeact")]
    public async Task Sensitivity_timeout_flows_to_worker_configuration_and_changes_only_the_model_fingerprint(string mode)
    {
        string root = Path.Combine(Path.GetTempPath(), "sv-worker-sensitivity-" + Guid.NewGuid().ToString("N"));
        string[] keys = ["DATASET_ROOT", "ARTIFACTS_ROOT", "BENCHMARK_TIMEOUT_SECONDS", "HYPERLIGHT_PYTHON_GUEST_PATH"];
        var saved = keys.ToDictionary(key => key, Environment.GetEnvironmentVariable);
        try
        {
            new DatasetWriter().Write(new SyntheticDataGenerator().Generate(DatasetProfile.Small), Path.Combine(root, "small"));
            Environment.SetEnvironmentVariable("DATASET_ROOT", root);
            Environment.SetEnvironmentVariable("ARTIFACTS_ROOT", Path.Combine(root, "artifacts"));
            Environment.SetEnvironmentVariable("BENCHMARK_TIMEOUT_SECONDS", "600");
            Environment.SetEnvironmentVariable("HYPERLIGHT_PYTHON_GUEST_PATH", "");
            using var composition = new Composition("fake");
            var baseline = composition.Model with { TimeoutSeconds = 120 };
            Assert.Equal(TimeSpan.FromSeconds(600), composition.Model.Timeout);
            Assert.NotEqual(baseline.Fingerprint(), composition.Model.Fingerprint());
            Assert.Equal(baseline.Fingerprint(), (composition.Model with { TimeoutSeconds = 120 }).Fingerprint());
            var record = await BenchmarkWorker.RunAsync(composition, mode, new AnalysisRequest(),
                "comparison-sensitivity", "trial-sensitivity", CancellationToken.None);
            Assert.NotEqual(RunStatus.TimedOut, record.Response.Status);
            Assert.Equal(composition.Model.Fingerprint(), record.Fingerprints.ModelFingerprint);
            var configuration = Assert.Single(record.Events, entry => entry.EventType == "EXECUTION_CONFIGURATION");
            var data = JsonSerializer.SerializeToElement(configuration.Data, JsonSerializerOptions.Web);
            Assert.Equal(600, data.GetProperty("modelConfiguration").GetProperty("timeoutSeconds").GetInt32());
        }
        finally
        {
            foreach (var (key, value) in saved) Environment.SetEnvironmentVariable(key, value);
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Watchdog_reaps_an_uncooperative_process_on_deadline_or_caller_cancellation(bool callerCancellation)
    {
        var start = new ProcessStartInfo(OperatingSystem.IsWindows() ? "cmd.exe" : "/bin/sh") { UseShellExecute = false };
        start.ArgumentList.Add(OperatingSystem.IsWindows() ? "/c" : "-c");
        start.ArgumentList.Add(OperatingSystem.IsWindows() ? "ping -n 30 127.0.0.1 >nul" : "exec sleep 30");
        using var process = Process.Start(start)!;
        using var cancellation = new CancellationTokenSource();
        if (callerCancellation) cancellation.CancelAfter(TimeSpan.FromMilliseconds(150));
        var clock = Stopwatch.StartNew();
        if (callerCancellation)
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                BenchmarkWorker.WaitForExitAsync(process, TimeSpan.FromSeconds(20), cancellation.Token));
        else
            Assert.False(await BenchmarkWorker.WaitForExitAsync(process, TimeSpan.FromMilliseconds(150), cancellation.Token));
        Assert.True(process.HasExited);
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(6), $"Watchdog took {clock.Elapsed}.");
    }

    [Theory]
    [InlineData("classic")]
    [InlineData("codeact")]
    public async Task Expired_worker_budget_persists_TimedOut_without_inventing_a_report(string mode)
    {
        string root = Path.Combine(Path.GetTempPath(), "sv-worker-timeout-" + Guid.NewGuid().ToString("N"));
        string[] keys = ["DATASET_ROOT", "ARTIFACTS_ROOT", "BENCHMARK_TIMEOUT_SECONDS", "HYPERLIGHT_PYTHON_GUEST_PATH"];
        var saved = keys.ToDictionary(key => key, Environment.GetEnvironmentVariable);
        try
        {
            new DatasetWriter().Write(new SyntheticDataGenerator().Generate(DatasetProfile.Small), Path.Combine(root, "small"));
            Environment.SetEnvironmentVariable("DATASET_ROOT", root);
            Environment.SetEnvironmentVariable("ARTIFACTS_ROOT", Path.Combine(root, "artifacts"));
            Environment.SetEnvironmentVariable("BENCHMARK_TIMEOUT_SECONDS", "0");
            Environment.SetEnvironmentVariable("HYPERLIGHT_PYTHON_GUEST_PATH", "");
            using var composition = new Composition("fake");
            var record = await BenchmarkWorker.RunAsync(composition, mode, new AnalysisRequest(),
                "comparison-timeout", "trial-timeout", CancellationToken.None);
            composition.RunStore.Save(record);
            Assert.Equal(RunStatus.TimedOut, record.Response.Status);
            Assert.Null(record.Response.Report);
            Assert.False(record.Response.Metrics.SchemaValid);
            Assert.Null(record.Response.Metrics.InputTokens);
            Assert.Contains(record.Events, entry => entry.EventType == "WORKER_TERMINATED" && entry.ErrorCategory == "TimedOut");
            Assert.Contains(record.Events, entry => entry.EventType == "RUN_FAILED" && entry.Status == "TimedOut");
            string directory = Path.Combine(composition.ArtifactsRoot, "runs", record.Response.RunId);
            using var result = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "result.json")));
            Assert.Equal("TimedOut", result.RootElement.GetProperty("status").GetString());
            Assert.True(File.Exists(Path.Combine(directory, "timeline.json")));
            Assert.Empty(Directory.GetFiles(directory, "*.tmp"));
        }
        finally
        {
            foreach (var (key, value) in saved) Environment.SetEnvironmentVariable(key, value);
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Interrupted_execution_recovery_retains_source_usage_and_partial_journal_evidence()
    {
        string root = Path.Combine(Path.GetTempPath(), "sv-worker-recovery-" + Guid.NewGuid().ToString("N"));
        string[] keys = ["DATASET_ROOT", "ARTIFACTS_ROOT"];
        var saved = keys.ToDictionary(key => key, Environment.GetEnvironmentVariable);
        try
        {
            new DatasetWriter().Write(new SyntheticDataGenerator().Generate(DatasetProfile.Small), Path.Combine(root, "small"));
            Environment.SetEnvironmentVariable("DATASET_ROOT", root);
            Environment.SetEnvironmentVariable("ARTIFACTS_ROOT", Path.Combine(root, "artifacts"));
            using var composition = new Composition("fake");
            string runId;
            string directory;
            using (var session = new ExecutionSession(composition.ArtifactsRoot, "codeact", "small", "comparison", "trial"))
            {
                runId = session.Context.RunId;
                directory = session.RunDirectory;
                session.Emit("RUN_STARTED");
                session.Emit("MODEL_REQUEST_STARTED", data: new { requestId = "request" });
                session.Emit("MODEL_RESPONSE_RECEIVED", data: new { requestId = "request", finishReason = "tool_calls" });
                session.Emit("MODEL_USAGE_RECORDED", data: new ModelUsage(1, 123, 45, 168));
                var call = new { invocationId = "code-1", toolName = "execute_code" };
                session.Emit("MODEL_TOOL_CALL_STARTED", data: call);
                session.Emit("EXECUTE_CODE_CALL_STARTED", data: call);
                session.Emit("CODE_EXECUTION_STARTED", data: call);
                session.WriteArtifact("codeact/generated-code-001.py", "while True:\n    pass");
            }
            File.AppendAllText(Path.Combine(directory, "events.jsonl"), "{\"interrupted");
            var record = BenchmarkWorker.Recover(composition, "codeact", new AnalysisRequest(), runId, "comparison", "trial",
                false, TimeSpan.FromSeconds(1), RunStatus.TimedOut, new FailureInfo("TimedOut", "INCOMPLETE: worker terminated."));
            composition.RunStore.Save(record);
            Assert.Equal(RunStatus.TimedOut, record.Response.Status);
            Assert.True(record.Response.Metrics.TelemetryValid, string.Join("; ", record.Response.Metrics.TelemetryErrors));
            Assert.Equal(123, record.Response.Metrics.InputTokens);
            Assert.Equal(45, record.Response.Metrics.OutputTokens);
            Assert.Equal(1, record.Response.Metrics.ExecuteCodeCallCount);
            Assert.Contains(record.Events, entry => entry.EventType == "CODE_EXECUTION_FAILED" && entry.ErrorCategory == "TimedOut");
            Assert.Contains(record.Events, entry => entry.EventType == "JOURNAL_WRITE_INTERRUPTED");
            Assert.Equal("while True:\n    pass", File.ReadAllText(Path.Combine(directory, "codeact/generated-code-001.py")));
            Assert.Equal("{\"interrupted", File.ReadAllText(Path.Combine(directory, "events.interrupted-fragment.bin")));
            Assert.Single(record.Events.Select(entry => entry.TraceId).Distinct());
            Assert.Equal(Enumerable.Range(1, record.Events.Count).Select(value => (long)value),
                record.Events.Select(entry => entry.SequenceNumber));
            var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
            var transported = JsonSerializer.Deserialize<RunRecord>(JsonSerializer.Serialize(record, json), json)!;
            Assert.Equal("TimedOut", BenchmarkHarness.FindFailure(transported)!.Category);
            var invalidOutput = record.Events[0] with
            {
                EventType = "RUN_FAILED", ErrorCategory = "InvalidModelOutput",
                Data = new { failure = new FailureInfo("InvalidModelOutput", "Invalid report.") },
            };
            foreach (string type in new[] { "WORKER_TERMINATED", "MODEL_REQUEST_FAILED" })
            {
                var infrastructure = invalidOutput with
                {
                    EventType = type, ErrorCategory = "WorkerFailed",
                    Data = new { failure = new FailureInfo("WorkerFailed", "Infrastructure failed.") },
                };
                Assert.Equal("WorkerFailed", BenchmarkHarness.FindFailure(record with { Events = [invalidOutput, infrastructure] })!.Category);
            }
        }
        finally
        {
            foreach (var (key, value) in saved) Environment.SetEnvironmentVariable(key, value);
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
