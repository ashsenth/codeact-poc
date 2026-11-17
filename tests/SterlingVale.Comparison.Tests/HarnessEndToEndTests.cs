using System.Text.Json;
using Microsoft.Extensions.Options;
using SterlingVale.AgentShared;
using SterlingVale.AgentShared.Configuration;
using SterlingVale.AgentShared.Prompting;
using SterlingVale.Benchmark;
using SterlingVale.Benchmark.Reporting;
using SterlingVale.DataGenerator;
using SterlingVale.Infrastructure.Configuration;
using SterlingVale.Infrastructure.Data;
using SterlingVale.Tools;
using Xunit;

namespace SterlingVale.Comparison.Tests;

/// <summary>
/// End-to-end harness test using the offline fake model. Because the fake produces the oracle report,
/// both modes score exactly and the fairness fingerprints match — a control that proves the harness,
/// scoring, and fairness checks work.
/// </summary>
[Collection("environment")]
public sealed class HarnessEndToEndTests
{
    [Fact]
    public void Committed_reference_matches_current_prompt_schema_tools_dataset_and_execution_contract()
    {
        using var reference = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "v3-reference.json")));
        var root = reference.RootElement;
        var fingerprints = root.GetProperty("fingerprints");
        string tmp = Path.Combine(Path.GetTempPath(), "sv-reference-" + Guid.NewGuid().ToString("N"));
        try
        {
            new DatasetWriter().Write(new SyntheticDataGenerator().Generate(DatasetProfile.Small), Path.Combine(tmp, "small"));
            var snapshots = new DatasetSnapshotProvider(new DatasetLoader(),
                Options.Create(new DatasetOptions { DataRoot = tmp, Profiles = ["small"] }));
            var snapshot = snapshots.GetSnapshot("small");
            var tools = new ToolCatalog().CreateTools(new PortfolioToolService(snapshot));
            Assert.Equal(Prompt.PromptId, root.GetProperty("promptRevision").GetString());
            Assert.Equal(Hashing.Sha256Hex(Prompt.Shared), root.GetProperty("promptHash").GetString());
            Assert.Equal(Hashing.Sha256Hex(Prompt.Shared), fingerprints.GetProperty("prompt").GetString());
            Assert.Equal(Hashing.Sha256Hex(OutputSchema.SchemaText), fingerprints.GetProperty("schema").GetString());
            Assert.Equal(ToolCatalog.ComputeFingerprint(tools), fingerprints.GetProperty("tools").GetString());
            Assert.Equal(snapshots.GetDatasetHash("small"), fingerprints.GetProperty("dataset").GetString());
            var pricing = root.GetProperty("pricingConfiguration").Deserialize<PricingConfiguration>(JsonSerializerOptions.Web)!;
            Assert.Equal(pricing.Fingerprint(), fingerprints.GetProperty("pricing").GetString());
            var model = JsonSerializer.Deserialize<ModelConfiguration>(
                """{"deploymentName":"gpt-4o","maxTurns":20,"timeoutSeconds":120}""", JsonSerializerOptions.Web)!;
            var actualModel = JsonSerializer.SerializeToElement(model, JsonSerializerOptions.Web);
            foreach (var field in root.GetProperty("modelConfiguration").EnumerateObject())
                Assert.Equal(field.Value.GetRawText(), actualModel.GetProperty(field.Name).GetRawText());
            var policy = root.GetProperty("executionPolicy");
            var responses = JsonSerializer.SerializeToElement(model with { ApiMode = "responses" }, JsonSerializerOptions.Web);
            foreach (var field in policy.EnumerateObject().Where(field => field.Name != "classicBatchSize"))
                Assert.Equal(field.Value.GetRawText(), responses.GetProperty(field.Name).GetRawText());
            Assert.Equal(0, policy.GetProperty("classicBatchSize").GetInt32());
        }
        finally
        {
            Directory.Delete(tmp, recursive: true);
        }
    }

    private static readonly string[] EnvKeys =
        ["DATASET_ROOT", "ARTIFACTS_ROOT", "AZURE_OPENAI_ENDPOINT", "HYPERLIGHT_PYTHON_GUEST_PATH", "BENCHMARK_TRIALS", "CLASSIC_BATCH_SIZE"];

    [Theory]
    [InlineData("small", 0, null)]
    [InlineData("medium", 0, null)]
    [InlineData("large", 0, null)]
    [InlineData("small", 3, null)]
    [InlineData("small", 0, 1)]
    [InlineData("small", 0, 3)]
    public async Task Harness_produces_matched_comparison_with_exact_scores(string profile, int batchSize, int? households)
    {
        var dataset = profile switch { "medium" => DatasetProfile.Medium, "large" => DatasetProfile.Large, _ => DatasetProfile.Small };
        string tmp = Path.Combine(Path.GetTempPath(), "svcmp-e2e-" + Guid.NewGuid().ToString("N"));
        new DatasetWriter().Write(new SyntheticDataGenerator().Generate(dataset), Path.Combine(tmp, profile));

        var saved = EnvKeys.ToDictionary(k => k, Environment.GetEnvironmentVariable);
        try
        {
            Environment.SetEnvironmentVariable("DATASET_ROOT", tmp);
            Environment.SetEnvironmentVariable("ARTIFACTS_ROOT", Path.Combine(tmp, "artifacts"));
            Environment.SetEnvironmentVariable("AZURE_OPENAI_ENDPOINT", string.Empty);
            Environment.SetEnvironmentVariable("HYPERLIGHT_PYTHON_GUEST_PATH", string.Empty);
            Environment.SetEnvironmentVariable("CLASSIC_BATCH_SIZE", batchSize.ToString(System.Globalization.CultureInfo.InvariantCulture));

            using var composition = new Composition();
            var harness = new BenchmarkHarness(composition);
            var report = await harness.RunAsync(profile, trials: 2, CancellationToken.None, households);
            Assert.Equal(households ?? report.DatasetHouseholdCount, report.ScopedHouseholdCount);
            await Assert.ThrowsAsync<InvalidOperationException>(() => harness.RunAsync(profile, 1, CancellationToken.None, 0));
            await Assert.ThrowsAsync<InvalidOperationException>(() => harness.RunAsync(profile, 1, CancellationToken.None, report.DatasetHouseholdCount + 1));

            Assert.True(report.Fairness.Matched);
            Assert.Equal(2, report.Classic.Correctness.ExactCount);
            Assert.Equal(2, report.CodeAct.Correctness.ExactCount);
            Assert.Equal(4, report.RawTrials.Count);
            Assert.Equal(1, report.WarmupsPerMode);
            Assert.All(report.RawTrials, t => Assert.Equal("Completed", t.Status));
            Assert.All(report.RawTrials, trial => Assert.True(trial.TelemetryValid));
            Assert.Equal(4, report.RawTrials.Select(trial => trial.CorrelationId).Distinct().Count());
            Assert.All(report.RawTrials.GroupBy(trial => trial.PairId), pair => Assert.Single(pair.Select(trial => trial.TrialId).Distinct()));
            Assert.Contains(report.RawTrials, t => t.Mode == "codeact" && t.ExecuteCodeCallCount == 1);
            foreach (var trial in report.RawTrials)
            {
                string path = Path.Combine(composition.ArtifactsRoot, "runs", trial.RunId, "result.json");
                using var result = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));
                Assert.Equal(report.ScopedHouseholdCount, result.RootElement.GetProperty("report").GetProperty("households").GetArrayLength());
                var ids = result.RootElement.GetProperty("report").GetProperty("households").EnumerateArray()
                    .Select(household => household.GetProperty("householdId").GetString()).Order(StringComparer.Ordinal);
                Assert.Equal(composition.Snapshots.GetSnapshot(profile).Households.Take(report.ScopedHouseholdCount!.Value)
                    .Select(household => household.Id.Value).Order(StringComparer.Ordinal), ids);
                Assert.NotNull(trial.Diagnostics);
                Assert.True(trial.Diagnostics.ToolCallsByName.ContainsKey("get_price"));
                Assert.Contains("get_fx=0", ComparisonWriter.RenderConsoleSummary(report));
            }
            ComparisonWriter.Write(report, composition.ArtifactsRoot);
            string index = File.ReadAllText(Path.Combine(composition.ArtifactsRoot, "index.md"));
            Assert.Contains("COMPARISON VALID", index);
            Assert.Contains("warm-up (not measured)", index);
            Assert.Contains("timeline.md", index);
            Assert.Contains("tool invocations", index);
            foreach (string journal in Directory.GetFiles(Path.Combine(composition.ArtifactsRoot, "runs"), "events.jsonl", SearchOption.AllDirectories))
            {
                using var start = System.Text.Json.JsonDocument.Parse(File.ReadLines(journal).First());
                if (start.RootElement.GetProperty("isWarmup").GetBoolean())
                {
                    Assert.Equal("warmup", start.RootElement.GetProperty("trialId").GetString());
                    Assert.DoesNotContain(start.RootElement.GetProperty("runId").GetString(), report.RawTrials.Select(trial => trial.RunId));
                }
            }
            if (batchSize > 0)
            {
                foreach (var trial in report.RawTrials.Where(trial => trial.Mode == "classic"))
                {
                    Assert.True(trial.ChildRunIds.Count > 1);
                    Assert.DoesNotContain(trial.RunId, trial.ChildRunIds);
                    foreach (string childId in trial.ChildRunIds)
                    {
                        Assert.True(File.Exists(Path.Combine(tmp, "artifacts", "runs", childId, "classic/final-response.json")));
                    }
                }
            }
        }
        finally
        {
            foreach (var (key, value) in saved)
            {
                Environment.SetEnvironmentVariable(key, value);
            }

            try
            {
                Directory.Delete(tmp, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }
}
