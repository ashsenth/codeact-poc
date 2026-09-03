using SterlingVale.Benchmark.Model;
using SterlingVale.Benchmark.Reporting;
using SterlingVale.Benchmark.Scoring;
using Xunit;

namespace SterlingVale.Comparison.Tests;

public sealed class ComparisonWriterTests
{
    private static readonly RunScore Exact =
        new(true, "Completed", 1d, 1d, 1d, 1d, 0m, 0m, 0m, CorrectnessScorer.Match.Exact);

    private static ComparisonReport SampleReport()
    {
        var trials = new List<TrialResult>
        {
            new(1, 0, "classic", 5.0, "Completed", 1, 0, 100, 0.001m, "run-c", Exact),
            new(1, 1, "codeact", 4.0, "Completed", 1, 1, 120, 0.001m, "run-a", Exact),
        };
        var fingerprints = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["prompt"] = "aaaa",
            ["tools"] = "bbbb",
            ["dataset"] = "cccc",
        };
        var fairness = new FairnessReport(true, [], fingerprints, fingerprints);
        return new ComparisonReport(
            "cmp-test-1", "2026-01-01T00:00:00+00:00", "small", "cccc", 123L, 1, 1, fairness,
            Aggregator.Summarize("classic", trials), Aggregator.Summarize("codeact", trials), trials);
    }

    [Fact]
    public void Write_then_read_round_trips()
    {
        var report = SampleReport();
        string dir = Path.Combine(Path.GetTempPath(), "svcmp-" + Guid.NewGuid().ToString("N"));
        try
        {
            string jsonPath = ComparisonWriter.Write(report, dir);
            Assert.True(File.Exists(jsonPath));
            Assert.True(File.Exists(Path.Combine(dir, "comparisons", "cmp-test-1.md")));

            var read = ComparisonWriter.Read(jsonPath);
            Assert.Equal(report.ComparisonId, read.ComparisonId);
            Assert.Equal(report.Profile, read.Profile);
            Assert.True(read.Fairness.Matched);
            Assert.Equal(report.RawTrials.Count, read.RawTrials.Count);
        }
        finally
        {
            try
            {
                Directory.Delete(dir, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }

    [Fact]
    public void Markdown_includes_disclaimer_and_fairness()
    {
        string md = ComparisonWriter.RenderMarkdown(SampleReport());
        Assert.Contains("SYNTHETIC", md, StringComparison.Ordinal);
        Assert.Contains("No performance outcome", md, StringComparison.Ordinal);
        Assert.Contains("## Fairness", md, StringComparison.Ordinal);
    }
}
