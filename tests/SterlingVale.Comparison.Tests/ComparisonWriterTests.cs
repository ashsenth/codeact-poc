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
            new(1, 0, "classic", 5.0, "Completed", 1, 2, 0, 100, 0.001m, "run-c", Exact) { TelemetryValid = true },
            new(1, 1, "codeact", 4.0, "Completed", 1, 2, 1, 120, 0.001m, "run-a", Exact) { TelemetryValid = true },
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
            new(1, 0, "classic", 5.0, "Completed", 3, 4, 0, 300, 0.003m, "run-c", Exact) { TelemetryValid = true },
            new(1, 1, "codeact", 4.0, "Completed", 1, 2, 1, 120, 0.001m, "run-a", Exact) { TelemetryValid = true },
        };
        var fp = new Dictionary<string, string>(StringComparer.Ordinal) { ["prompt"] = "a", ["tools"] = "b", ["dataset"] = "c" };
        var fairness = new FairnessReport(true, [], fp, fp);
        var classic = Aggregator.Summarize("classic", trials);
        var codeact = Aggregator.Summarize("codeact", trials);
        return new ComparisonReport(
            $"{profile}-x", "2026-01-01T00:00:00+00:00", profile, "c", 1L, 1, 1, households, fairness,
            classic, codeact, Aggregator.ComputeDelta(classic, codeact), trials);
    }

    [Theory]
    [InlineData("prompt", "shared-prompt hashes")]
    [InlineData("model", "model-configuration fingerprints")]
    public void Mixed_configurations_cannot_be_compared_in_scaling_report(string key, string reason)
    {
        var old = ReportFor("small", 10);
        var current = ReportFor("medium", 40);
        var fingerprints = new Dictionary<string, string>(current.Fairness.ClassicFingerprints) { [key] = "new-revision" };
        current = current with { Fairness = new(true, [], fingerprints, fingerprints) };
        var error = Assert.Throws<InvalidOperationException>(() => ComparisonWriter.RenderScalingHtml([old, current]));
        Assert.Contains(reason, error.Message);
        string html = ComparisonWriter.RenderScalingHtml([old, ReportFor("medium", 40)]);
        Assert.Contains("small", html);
        Assert.Contains("medium", html);
    }

    [Fact]
    public void Incomplete_evidence_and_prompt_boundary_are_recorded_in_all_report_formats()
    {
        var sample = SampleReport();
        var trials = sample.RawTrials.Select(trial => trial with
        {
            Status = "Incomplete", ReportStatus = "incomplete", MissingItems = ["price for SYN001"],
            Score = trial.Score with { MatchClass = CorrectnessScorer.Match.Partial },
        }).ToArray();
        var report = sample with
        {
            RawTrials = trials, PreviousPromptHash = "old-prompt", PromptRevision = "exposure-analysis/v2",
        };
        foreach (string output in new[] { ComparisonWriter.RenderConsoleSummary(report), ComparisonWriter.RenderMarkdown(report),
            ComparisonWriter.RenderHtml(report), ComparisonWriter.RenderCsv(report) })
        {
            Assert.Contains("incomplete", output);
            Assert.Contains("SYN001", output);
            Assert.Contains("aaaa", output);
            Assert.Contains("old-prompt", output);
            Assert.DoesNotContain("duration median", output);
        }
    }

    [Fact]
    public void Generation_only_tokens_are_labelled_in_reports_and_never_described_as_complete_totals()
    {
        var original = SampleReport();
        var report = original with
        {
            ExecutionKind = "live-model-code-generation-only",
            RawTrials = [original.RawTrials[0], original.RawTrials[1] with
            {
                Status = "NotExecuted", ModelTurnCount = 1, InputTokens = 1911, OutputTokens = 78,
                Score = RunScore.ForFailure("NotExecuted", false, CorrectnessScorer.Match.InvalidSchema),
            }],
        };
        const string notice = "first turn only, code not executed: not comparable to Classic totals";
        foreach (string output in new[] { ComparisonWriter.RenderHtml(report), ComparisonWriter.RenderScalingHtml([report]),
            ComparisonWriter.RenderConsoleSummary(report), ComparisonWriter.RenderMarkdown(report) })
            Assert.Contains(notice, output);
        Assert.Contains("1911 / 78", ComparisonWriter.RenderHtml(report));
        Assert.DoesNotContain(notice, ComparisonWriter.RenderHtml(original with { ExecutionKind = "live-model-hyperlight" }));
    }

    [Fact]
    public void Failed_measured_run_invalidates_every_renderer_and_suppresses_aggregate_statistics()
    {
        var original = SampleReport() with { ExecutionKind = "live-model-hyperlight" };
        var failed = original.RawTrials[1] with
        {
            Status = "Failed",
            Score = RunScore.ForFailure("Failed", false, CorrectnessScorer.Match.Failed),
            Failure = new("ClientResultException", "Model request failed: HTTP 403, error code Forbidden.", 403, "Forbidden"),
            Diagnostics = new("stop", 34, "{\"households\":[]}",
                new Dictionary<string, int> { ["get_price"] = 0, ["get_fx"] = 0, ["asset_class"] = 0 }, 0, "INCOMPLETE: model stopped before pricing"),
        };
        var report = original with { RawTrials = [original.RawTrials[0], failed] };
        const string message = "COMPARISON INVALID: 1 of 2 runs failed (ClientResultException)";
        foreach (string output in new[] { ComparisonWriter.RenderConsoleSummary(report), ComparisonWriter.RenderHtml(report),
            ComparisonWriter.RenderMarkdown(report), ComparisonWriter.RenderScalingHtml([report]) })
        {
            Assert.Contains(message, output);
            Assert.DoesNotContain("median", output, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("finish_reason=stop", output);
            Assert.Contains("get_price=0", output);
        }
        string directory = Path.Combine(Path.GetTempPath(), "sv-invalid-" + Guid.NewGuid().ToString("N"));
        try
        {
            string path = ComparisonWriter.Write(report, directory);
            using var json = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));
            Assert.Equal("outcome", json.RootElement.EnumerateObject().First().Name);
            Assert.Equal(message, json.RootElement.GetProperty("outcome").GetProperty("message").GetString());
            Assert.Equal(System.Text.Json.JsonValueKind.Null, json.RootElement.GetProperty("classic").GetProperty("durationMs").GetProperty("median").ValueKind);
            Assert.Equal(System.Text.Json.JsonValueKind.Null, json.RootElement.GetProperty("codeAct").GetProperty("roundTrips").ValueKind);
            Assert.Null(ComparisonWriter.Read(path).Delta);
            Assert.Contains(message, File.ReadAllText(Path.Combine(directory, "index.md")));
            Assert.Contains("HTTP 403", File.ReadAllText(Path.ChangeExtension(path, ".html")));
        }
        finally { Directory.Delete(directory, recursive: true); }
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
        Assert.Contains("## Calls and turns", md, StringComparison.Ordinal);
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
    public void Html_notes_classic_batching_only_when_configured()
    {
        Assert.DoesNotContain("batches of", ComparisonWriter.RenderHtml(SampleReport()), StringComparison.Ordinal);

        var batched = SampleReport() with { ClassicBatchSize = 5 };
        Assert.Contains("batches of <b>5</b>", ComparisonWriter.RenderHtml(batched), StringComparison.Ordinal);
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

    [Fact]
    public void Reports_label_controls_and_do_not_invent_performance_or_failures()
    {
        var report = SampleReport() with { ExecutionKind = "offline-fake-control" };
        string html = ComparisonWriter.RenderHtml(report);
        string scaling = ComparisonWriter.RenderScalingHtml([report]);
        string markdown = ComparisonWriter.RenderMarkdown(report);
        foreach (string output in new[] { html, scaling, markdown })
        {
            Assert.Contains("OFFLINE FAKE-MODEL CONTROL", output, StringComparison.Ordinal);
            Assert.Contains("NOT live performance", output, StringComparison.Ordinal);
            Assert.DoesNotContain("far fewer", output, StringComparison.Ordinal);
            Assert.DoesNotContain("Why Classic failed", output, StringComparison.Ordinal);
            Assert.DoesNotContain("Classic did not complete any dataset", output, StringComparison.Ordinal);
            Assert.DoesNotContain("undercounts CodeAct", output, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("CodeAct vs Classic (both completed)", html, StringComparison.Ordinal);
        Assert.Contains(",execution_kind,finish_reason,tool_calls_by_name,", ComparisonWriter.RenderCsv(report).Split('\n')[0], StringComparison.Ordinal);
        Assert.Contains(",offline-fake-control,", ComparisonWriter.RenderCsv(report), StringComparison.Ordinal);
        Assert.Contains("EXECUTION KIND UNAVAILABLE", ComparisonWriter.RenderHtml(SampleReport()), StringComparison.Ordinal);
        Assert.Contains("LIVE MODEL WITH OFFLINE CODE STAND-IN",
            ComparisonWriter.RenderHtml(report with { ExecutionKind = "live-model-offline-code-stand-in" }), StringComparison.Ordinal);
        Assert.Contains("CodeAct vs Classic (both completed)",
            ComparisonWriter.RenderHtml(report with { ExecutionKind = "live-model-hyperlight" }), StringComparison.Ordinal);
    }
}
