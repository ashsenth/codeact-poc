using SterlingVale.AgentShared.Analysis;
using SterlingVale.AgentShared.Artifacts;
using SterlingVale.AgentShared.Configuration;
using SterlingVale.Domain.Analysis;
using SterlingVale.Tools.Observability;
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
    public void Events_contain_start_and_completion()
    {
        var files = RunArtifacts.Build(Record(null), Model, Pricing);
        var lines = files[RunArtifacts.Files.Events].Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Contains(lines, l => l.Contains("run_started", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.Contains("run_completed", StringComparison.Ordinal));
    }

    [Fact]
    public void Metadata_includes_fingerprints_and_prompt_id()
    {
        var files = RunArtifacts.Build(Record(null), Model, Pricing);
        string metadata = files[RunArtifacts.Files.Metadata];
        Assert.Contains("fingerprints", metadata, StringComparison.Ordinal);
        Assert.Contains("exposure-analysis/v1", metadata, StringComparison.Ordinal);
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
}
