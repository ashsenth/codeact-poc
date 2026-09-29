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
            new(1, 0, "classic", 5.0, "Completed", 1, 2, 0, 100, 0.001m, "run-c", Exact),
            new(1, 1, "codeact", 4.0, "Completed", 1, 2, 1, 120, 0.001m, "run-a", Exact),
        };
        var fingerprints = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["prompt"] = "aaaa",
            ["tools"] = "bbbb",
            ["dataset"] = "cccc",
        };
        var fairness = new FairnessReport(true, [], fingerprints, fingerprints);
        var classic = Aggregator.Summarize("classic", trials);
        var codeact = Aggregator.Summarize("codeact", trials);
        return new ComparisonReport(
            "cmp-test-1", "2026-01-01T00:00:00+00:00", "small", "cccc", 123L, 1, 1, 10, fairness,
            classic, codeact, Aggregator.ComputeDelta(classic, codeact), trials);
    }

    private static ComparisonReport ReportFor(string profile, int households)
    {
        var trials = new List<TrialResult>
        {
            new(1, 0, "classic", 5.0, "Completed", 3, 4, 0, 300, 0.003m, "run-c", Exact),
            new(1, 1, "codeact", 4.0, "Completed", 1, 2, 1, 120, 0.001m, "run-a", Exact),
        };
        var fp = new Dictionary<string, string>(StringComparer.Ordinal) { ["prompt"] = "a", ["tools"] = "b", ["dataset"] = "c" };
        var fairness = new FairnessReport(true, [], fp, fp);
        var classic = Aggregator.Summarize("classic", trials);
        var codeact = Aggregator.Summarize("codeact", trials);
        return new ComparisonReport(
            $"{profile}-x", "2026-01-01T00:00:00+00:00", profile, "c", 1L, 1, 1, households, fairness,
            classic, codeact, Aggregator.ComputeDelta(classic, codeact), trials);
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

    [Fact]
    public void Markdown_includes_headline_mechanism_and_new_summary_rows()
    {
        string md = ComparisonWriter.RenderMarkdown(SampleReport());
        Assert.Contains("## Headline", md, StringComparison.Ordinal);
        Assert.Contains("compute reduction", md, StringComparison.Ordinal);
        Assert.Contains("Cost median (USD)", md, StringComparison.Ordinal);
        Assert.Contains("Model turns median", md, StringComparison.Ordinal);
    }

    [Fact]
    public void Csv_has_header_and_one_row_per_trial()
    {
        var report = SampleReport();
        string csv = ComparisonWriter.RenderCsv(report);
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(1 + report.RawTrials.Count, lines.Length);
        Assert.StartsWith("comparison_id,profile,dataset_hash", lines[0], StringComparison.Ordinal);
        Assert.Contains("classic", csv, StringComparison.Ordinal);
        Assert.Contains("codeact", csv, StringComparison.Ordinal);
    }

    [Fact]
    public void Write_emits_csv_alongside_json_and_md()
    {
        var report = SampleReport();
        string dir = Path.Combine(Path.GetTempPath(), "svcmp-" + Guid.NewGuid().ToString("N"));
        try
        {
            ComparisonWriter.Write(report, dir);
            Assert.True(File.Exists(Path.Combine(dir, "comparisons", "cmp-test-1.csv")));
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
    public void Delta_summary_line_names_each_metric()
    {
        string line = ComparisonWriter.FormatDeltaSummary(SampleReport().Delta!);
        Assert.Contains("duration", line, StringComparison.Ordinal);
        Assert.Contains("tokens", line, StringComparison.Ordinal);
        Assert.Contains("cost", line, StringComparison.Ordinal);
        Assert.Contains("tools", line, StringComparison.Ordinal);
    }

    [Fact]
    public void Html_is_self_contained_with_inline_svg_and_no_external_requests()
    {
        string html = ComparisonWriter.RenderHtml(SampleReport());

        Assert.Contains("<svg", html, StringComparison.Ordinal);
        Assert.Contains("Classic vs CodeAct", html, StringComparison.Ordinal);
        Assert.Contains("fairness: MATCHED", html, StringComparison.Ordinal);
        Assert.Contains("cmp-test-1", html, StringComparison.Ordinal);
        // Offline guarantee: no network references of any kind.
        Assert.DoesNotContain("http://", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("https://", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Write_emits_html_alongside_json_and_md()
    {
        var report = SampleReport();
        string dir = Path.Combine(Path.GetTempPath(), "svcmp-" + Guid.NewGuid().ToString("N"));
        try
        {
            ComparisonWriter.Write(report, dir);
            Assert.True(File.Exists(Path.Combine(dir, "comparisons", "cmp-test-1.html")));
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
    public void Scaling_html_renders_each_profile_and_is_offline()
    {
        var reports = new List<ComparisonReport>
        {
            ReportFor("small", 10),
            ReportFor("medium", 40),
            ReportFor("large", 120),
        };

        string html = ComparisonWriter.RenderScalingHtml(reports);

        Assert.Contains("<svg", html, StringComparison.Ordinal);
        Assert.Contains("small (n=10)", html, StringComparison.Ordinal);
        Assert.Contains("large (n=120)", html, StringComparison.Ordinal);
        Assert.DoesNotContain("http://", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("https://", html, StringComparison.OrdinalIgnoreCase);
    }
}
