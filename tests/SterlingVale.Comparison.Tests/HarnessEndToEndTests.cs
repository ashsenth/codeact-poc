using SterlingVale.Benchmark;
using SterlingVale.DataGenerator;
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
        ["DATASET_ROOT", "ARTIFACTS_ROOT", "AZURE_OPENAI_ENDPOINT", "HYPERLIGHT_PYTHON_GUEST_PATH", "BENCHMARK_TRIALS"];

    [Fact]
    public async Task Harness_produces_matched_comparison_with_exact_scores()
    {
        string tmp = Path.Combine(Path.GetTempPath(), "svcmp-e2e-" + Guid.NewGuid().ToString("N"));
        new DatasetWriter().Write(new SyntheticDataGenerator().Generate(DatasetProfile.Small), Path.Combine(tmp, "small"));

        var saved = EnvKeys.ToDictionary(k => k, Environment.GetEnvironmentVariable);
        try
        {
            Environment.SetEnvironmentVariable("DATASET_ROOT", tmp);
            Environment.SetEnvironmentVariable("ARTIFACTS_ROOT", Path.Combine(tmp, "artifacts"));
            Environment.SetEnvironmentVariable("AZURE_OPENAI_ENDPOINT", string.Empty);
            Environment.SetEnvironmentVariable("HYPERLIGHT_PYTHON_GUEST_PATH", string.Empty);

            var composition = new Composition();
            var report = await new BenchmarkHarness(composition).RunAsync("small", trials: 2, CancellationToken.None);

            Assert.True(report.Fairness.Matched);
            Assert.Equal(2, report.Classic.Correctness.ExactCount);
            Assert.Equal(2, report.CodeAct.Correctness.ExactCount);
            Assert.Equal(4, report.RawTrials.Count);
            Assert.Equal(1, report.WarmupsPerMode);
            Assert.All(report.RawTrials, t => Assert.Equal("Completed", t.Status));
            Assert.Contains(report.RawTrials, t => t.Mode == "codeact" && t.ExecuteCodeCallCount == 1);
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
