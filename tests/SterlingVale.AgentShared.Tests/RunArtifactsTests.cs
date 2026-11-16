using System.Text.Json;
using SterlingVale.AgentShared.Analysis;
using SterlingVale.AgentShared.Artifacts;
using SterlingVale.AgentShared.Configuration;
using SterlingVale.Domain.Analysis;
using SterlingVale.Tools.Observability;
using SterlingVale.Telemetry;
using Xunit;

namespace SterlingVale.AgentShared.Tests;

public sealed class RunArtifactsTests
{
    private static readonly ModelConfiguration Model = new()
    {
        DeploymentName = "fake-model",
        Temperature = 0f,
        MaxTurns = 12,
        TimeoutSeconds = 120,
    };

    private static readonly PricingConfiguration Pricing = new()
    {
        InputPerMillion = 2.50m,
        OutputPerMillion = 10.00m,
    };

    private static RunRecord Record(string? generatedCode)
    {
        var fingerprints = new FairnessFingerprints
        {
            PromptHash = "p",
            SchemaHash = "s",
            ToolFingerprint = "t",
            ModelFingerprint = "m",
            PricingFingerprint = "pr",
            DatasetHash = "d",
        };
        var metrics = new RunMetrics
        {
            Duration = TimeSpan.FromMilliseconds(5),
            ModelRequestCount = 2,
            ModelTurnCount = 2,
            ToolCallCount = 1,
            ToolCallsByName = new Dictionary<string, int>(StringComparer.Ordinal) { ["list_households"] = 1 },
            TotalTokens = 42,
            SchemaValid = true,
        };
        var response = new AnalysisResponse
        {
            RunId = "run-1",
            Mode = "classic",
            Status = RunStatus.Completed,
            Report = null,
            Metrics = metrics,
        };
        return new RunRecord
        {
            Response = response,
            Mode = "classic",
            RawModelOutput = "{\"households\":[]}",
            GeneratedCode = generatedCode,
            ToolCalls =
            [
                new ToolCallRecord(0, "get_policy", "{\"householdId\":\"H0001\"}", "{\"driftToleranceFraction\":0.05}", 1.2, "ok", DateTimeOffset.UnixEpoch),
            ],
            Transcript =
            [
                new TranscriptMessage(0, "assistant", null,
                    [new TranscriptCall("c1", "get_policy", "{\"householdId\":\"H0001\"}")],
                    []),
                new TranscriptMessage(1, "tool", null, [], [new TranscriptResult("c1", null, "{\"driftToleranceFraction\":0.05}")]),
            ],
            Fingerprints = fingerprints,
        };
    }

    [Fact]
    public void Build_produces_all_five_artifact_files()
    {
        var files = RunArtifacts.Build(Record(null), Model, Pricing);
        Assert.Contains(RunArtifacts.Files.Result, files.Keys);
        Assert.Contains(RunArtifacts.Files.Metrics, files.Keys);
        Assert.Contains(RunArtifacts.Files.Events, files.Keys);
        Assert.Contains(RunArtifacts.Files.Metadata, files.Keys);
        Assert.Contains(RunArtifacts.Files.GeneratedCode, files.Keys);
    }

    [Fact]
    public void Generated_code_marker_when_absent()
    {
        var files = RunArtifacts.Build(Record(null), Model, Pricing);
        Assert.Equal(RunArtifacts.GeneratedCodeUnavailable, files[RunArtifacts.Files.GeneratedCode]);
    }

    [Fact]
    public void Generated_code_preserved_when_present()
    {
        var files = RunArtifacts.Build(Record("print('hi')"), Model, Pricing);
        Assert.Equal("print('hi')", files[RunArtifacts.Files.GeneratedCode]);
    }

    [Fact]
    public void Events_are_not_fabricated_when_no_execution_was_recorded()
    {
        var files = RunArtifacts.Build(Record(null), Model, Pricing);
        Assert.Empty(files[RunArtifacts.Files.Events]);
    }

    [Fact]
    public void Metadata_includes_fingerprints_and_prompt_id()
    {
        var files = RunArtifacts.Build(Record(null), Model, Pricing);
        string metadata = files[RunArtifacts.Files.Metadata];
        Assert.Contains("fingerprints", metadata, StringComparison.Ordinal);
        Assert.Contains("exposure-analysis/v3", metadata, StringComparison.Ordinal);
        using var document = System.Text.Json.JsonDocument.Parse(metadata);
        var configuration = document.RootElement.GetProperty("modelConfiguration");
        Assert.Equal("json_schema", configuration.GetProperty("responseFormat").GetString());
        Assert.False(configuration.GetProperty("strict").GetBoolean());
        Assert.True(configuration.GetProperty("parallel_tool_calls").GetBoolean());
        Assert.Equal(SterlingVale.AgentShared.Prompting.StructuredOutput.SchemaHash,
            configuration.GetProperty("responseSchemaHash").GetString());
    }

    [Fact]
    public void Raw_model_output_is_captured()
    {
        var files = RunArtifacts.Build(Record(null), Model, Pricing);
        Assert.Contains(RunArtifacts.Files.RawOutput, files.Keys);
        Assert.Equal("{\"households\":[]}", files[RunArtifacts.Files.RawOutput]);
    }

    [Fact]
    public void Tool_calls_jsonl_stamps_runid_and_nests_json()
    {
        var jsonl = RunArtifacts.Build(Record(null), Model, Pricing)[RunArtifacts.Files.ToolCalls];
        Assert.Contains("\"runId\":\"run-1\"", jsonl, StringComparison.Ordinal);
        Assert.Contains("\"name\":\"get_policy\"", jsonl, StringComparison.Ordinal);
        // Arguments/results are embedded as nested JSON, not re-stringified.
        Assert.Contains("\"arguments\":{\"householdId\":\"H0001\"}", jsonl, StringComparison.Ordinal);
        Assert.Contains("\"result\":{\"driftToleranceFraction\":0.05}", jsonl, StringComparison.Ordinal);
    }

    [Fact]
    public void Transcript_jsonl_records_each_message_with_runid()
    {
        var jsonl = RunArtifacts.Build(Record(null), Model, Pricing)[RunArtifacts.Files.Transcript];
        var lines = jsonl.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lines.Length);
        Assert.All(lines, l => Assert.Contains("\"runId\":\"run-1\"", l, StringComparison.Ordinal));
        Assert.Contains(lines, l => l.Contains("\"role\":\"assistant\"", StringComparison.Ordinal) && l.Contains("get_policy", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.Contains("\"role\":\"tool\"", StringComparison.Ordinal));
    }

    [Fact]
    public void Observed_transcript_preserves_all_calls_after_large_payloads_and_marks_capture_limits()
    {
        string largeText = new('x', 70000);
        string program = "print('retained-program')";
        string arguments = JsonSerializer.Serialize(new { code = program });
        foreach (var mode in new[] { PayloadCaptureMode.Full, PayloadCaptureMode.Metadata, PayloadCaptureMode.None })
        {
            var record = Record(program) with
            {
                Execution = new SterlingVale.Telemetry.ExecutionContext("comparison", "trial", "run-1", "correlation",
                    "trace", "span", "codeact", "small"),
                PayloadCaptureMode = mode,
                RawModelOutput = largeText,
                Transcript =
                [
                    new TranscriptMessage(0, "assistant", largeText, [], []),
                    new TranscriptMessage(1, "assistant", null,
                        [new TranscriptCall("python-call", "execute_code", arguments),
                         new TranscriptCall("large-call", "get_policy", largeText)], []),
                    new TranscriptMessage(2, "tool", null, [],
                        [new TranscriptResult("python-call", "execute_code", largeText)]),
                    new TranscriptMessage(3, "assistant", "final-message", [], []),
                ],
            };
            var files = RunArtifacts.Build(record, Model, Pricing);
            string[] lines = files[RunArtifacts.Files.Transcript].Split('\n', StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal(4, lines.Length);
            for (int i = 0; i < lines.Length; i++)
            {
                using var line = JsonDocument.Parse(lines[i]);
                Assert.Equal(i, line.RootElement.GetProperty("index").GetInt32());
                Assert.Equal("run-1", line.RootElement.GetProperty("runId").GetString());
            }

            using var first = JsonDocument.Parse(lines[0]);
            using var calls = JsonDocument.Parse(lines[1]);
            using var results = JsonDocument.Parse(lines[2]);
            using var last = JsonDocument.Parse(lines[3]);
            Assert.Equal(2, calls.RootElement.GetProperty("calls").GetArrayLength());
            Assert.Equal("python-call", calls.RootElement.GetProperty("calls")[0].GetProperty("id").GetString());
            Assert.Equal("execute_code", calls.RootElement.GetProperty("calls")[0].GetProperty("name").GetString());
            Assert.Equal("python-call", results.RootElement.GetProperty("results")[0].GetProperty("callId").GetString());
            var text = first.RootElement.GetProperty("text");
            var code = calls.RootElement.GetProperty("calls")[0].GetProperty("arguments");
            var largeArguments = calls.RootElement.GetProperty("calls")[1].GetProperty("arguments");
            var result = results.RootElement.GetProperty("results")[0].GetProperty("result");
            Assert.Equal(mode.ToString().ToLowerInvariant(), text.GetProperty("captureMode").GetString());
            Assert.Equal(mode == PayloadCaptureMode.None ? 0 : largeText.Length, text.GetProperty("originalLength").GetInt32());
            if (mode == PayloadCaptureMode.Full)
            {
                Assert.True(text.GetProperty("truncated").GetBoolean());
                Assert.Equal(65536, text.GetProperty("content").GetString()!.Length);
                Assert.Equal(arguments, code.GetProperty("content").GetString());
                Assert.True(largeArguments.GetProperty("truncated").GetBoolean());
                Assert.True(result.GetProperty("truncated").GetBoolean());
                Assert.Equal("final-message", last.RootElement.GetProperty("text").GetProperty("content").GetString());
            }
            else
            {
                Assert.Equal(JsonValueKind.Null, text.GetProperty("content").ValueKind);
                Assert.Equal(JsonValueKind.Null, code.GetProperty("content").ValueKind);
                Assert.Equal(JsonValueKind.Null, result.GetProperty("content").ValueKind);
                Assert.DoesNotContain(program, files[RunArtifacts.Files.Transcript], StringComparison.Ordinal);
            }

            using var raw = JsonDocument.Parse(files[RunArtifacts.Files.RawOutput]);
            Assert.Equal(mode == PayloadCaptureMode.Full, raw.RootElement.GetProperty("truncated").GetBoolean());
        }

        var sensitive = Record(null) with
        {
            Execution = new SterlingVale.Telemetry.ExecutionContext("comparison", "trial", "run-1", "correlation",
                "trace", "span", "classic", "small"),
            PayloadCaptureMode = PayloadCaptureMode.Full,
            Transcript = [new TranscriptMessage(0, "assistant", null,
                [new TranscriptCall("sensitive-call", "get_policy", "{\"apiKey\":\"synthetic-private-value\"}")], [])],
        };
        string redacted = RunArtifacts.Build(sensitive, Model, Pricing)[RunArtifacts.Files.Transcript];
        using var redactedLine = JsonDocument.Parse(redacted);
        Assert.True(redactedLine.RootElement.GetProperty("calls")[0].GetProperty("arguments").GetProperty("redacted").GetBoolean());
        Assert.DoesNotContain("synthetic-private-value", redacted, StringComparison.Ordinal);
    }
}
