using SterlingVale.Benchmark;
using SterlingVale.DataGenerator;
using SterlingVale.Benchmark.Reporting;
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
