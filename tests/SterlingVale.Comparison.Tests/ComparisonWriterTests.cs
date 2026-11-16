using SterlingVale.Benchmark.Model;
using SterlingVale.Benchmark.Reporting;
using SterlingVale.Benchmark.Scoring;
using SterlingVale.AgentShared.Analysis;
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
        }
        Assert.True(report.Outcome.IsValid);
        Assert.Contains("| median |", ComparisonWriter.RenderMarkdown(report));
        Assert.Null(report.ForPresentation().Delta);
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
        const string message = "COMPARISON INVALID: infrastructure/harness failure (ClientResultException)";
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
        Assert.Contains("Estimated cost median (USD)", md, StringComparison.Ordinal);
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
        string line = ComparisonWriter.FormatDeltaSummary(SampleReport());
        Assert.Contains("duration", line, StringComparison.Ordinal);
        Assert.Contains("tokens", line, StringComparison.Ordinal);
        Assert.Contains("cost", line, StringComparison.Ordinal);
        Assert.Contains("tools", line, StringComparison.Ordinal);
        Assert.Contains("oracle correct classic 1/1, codeact 1/1", line, StringComparison.Ordinal);
    }

    private static ComparisonReport SummaryReport(int scope)
    {
        var sample = SampleReport();
        var trials = Enumerable.Range(1, 3).SelectMany(pair => sample.RawTrials.Select(trial => trial with
        {
            PairId = pair, DurationMs = 99999, AnalysisDurationMs = pair * (trial.Mode == "classic" ? 1000 : 500),
            TotalTokens = trial.Mode == "classic" ? pair * 110 : (pair - 1) * 30 + 10,
            EstimatedCostUsd = trial.Mode == "classic" ? pair : (pair - 1) * 0.3m + 0.1m,
            RunId = $"run-{trial.Mode}-{scope}-{pair}", Score = Exact with { MatchClass = "Partial" },
        })).ToArray();
        var fingerprints = new Dictionary<string, string>(sample.Fairness.ClassicFingerprints)
        {
            ["model"] = "model", ["pricing"] = "pricing",
        };
        return sample with
        {
            ComparisonId = $"summary-scope-{scope}", ScopedHouseholdCount = scope, MeasuredTrials = 3,
            RawTrials = trials, Fairness = new(true, [], fingerprints, fingerprints), ExecutionKind = "live-model-hyperlight",
        };
    }

    [Fact]
    public void Sweep_summary_retains_scopes_all_trials_analysis_medians_and_context_on_each_delta()
    {
        string root = Path.Combine(Path.GetTempPath(), "sv-sweep-" + Guid.NewGuid().ToString("N"));
        try
        {
            var reports = new[] { SummaryReport(1), SummaryReport(3) };
            string[] paths = reports.Select(report => ComparisonWriter.Write(report, root)).ToArray();
            SweepSummaryWriter.Write(paths, root);
            foreach (string format in new[] { "md", "html" })
            {
                string text = File.ReadAllText(Path.Combine(root, "summary." + format));
                Assert.Contains("small (N=1)", text);
                Assert.Contains("small (N=3)", text);
                Assert.Contains("-81.8% tokens; both Partial; not a correctness-equivalent win", text);
                Assert.Contains("-80.0% est. cost; both Partial; not a correctness-equivalent win", text);
                Assert.Contains("-50.0% analysis time; both Partial; not a correctness-equivalent win", text);
                Assert.DoesNotContain("99.999", text);
                foreach (var trial in reports.SelectMany(report => report.RawTrials))
                    Assert.Contains($"runs/{trial.RunId}/timeline.md", text);
            }
            Assert.Contains("| 220 | 2.0000000 | 2.000 |", File.ReadAllText(Path.Combine(root, "summary.md")));
            Assert.Contains("| 40 | 0.4000000 | 1.000 |", File.ReadAllText(Path.Combine(root, "summary.md")));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void Unearned_completeness_and_missing_components_survive_reporting_and_aggregation()
    {
        string root = Path.Combine(Path.GetTempPath(), "sv-completeness-" + Guid.NewGuid().ToString("N"));
        try
        {
            var sample = SummaryReport(1);
            var trials = sample.RawTrials.Select(trial => trial.Mode == "classic" ? trial with
            {
                Score = Exact with
                {
                    MatchClass = CorrectnessScorer.Match.UnearnedCompleteness,
                    CompletenessAssessed = true, AllocationMae = null, MaxAllocationError = null,
                    TradeNotionalMaeFraction = null,
                    MissingComponents = new Dictionary<string, IReadOnlyList<string>>
                    {
                        ["H1"] = ["allocation/drift:Equity", "trade:Equity"],
                    },
                },
            } : trial with { Score = Exact with { CompletenessAssessed = true } }).ToArray();
            var classic = Aggregator.Summarize("classic", trials);
            var codeact = Aggregator.Summarize("codeact", trials);
            var report = sample with { RawTrials = trials, Classic = classic, CodeAct = codeact };
            Assert.Equal(3, classic.Correctness.UnearnedCompletenessCount);
            Assert.Null(classic.Correctness.MeanAllocationMae);
            Assert.Null(classic.Correctness.MeanTradeNotionalMae);
            Assert.Null(classic.Correctness.MaxAllocationError);
            Assert.True(report.Outcome.IsValid);
            Assert.Null(report.ForPresentation().Delta);
            string path = ComparisonWriter.Write(report, root);
            var read = ComparisonWriter.Read(path);
            Assert.Null(read.RawTrials[0].Score.AllocationMae);
            Assert.Equal(["allocation/drift:Equity", "trade:Equity"], read.RawTrials[0].Score.MissingComponents["H1"]);
            foreach (string output in new[] { ComparisonWriter.RenderHtml(read), ComparisonWriter.RenderMarkdown(read) })
            {
                Assert.Contains("claimed complete but missing required components", output);
                Assert.Contains("H1", output);
                Assert.Contains("allocation/drift:Equity", output);
                Assert.Contains("trade:Equity", output);
                Assert.Contains("unavailable", output);
            }
            SweepSummaryWriter.Write([path], root);
            foreach (string format in new[] { "md", "html" })
            {
                string text = File.ReadAllText(Path.Combine(root, "summary." + format));
                Assert.Contains("UnearnedCompleteness", text);
                Assert.Contains("3/3 (assessed 3/3)", text);
                Assert.Contains("not a correctness-equivalent win", text);
                Assert.Contains("UnearnedCompleteness Classic 3/3", text);
            }
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
    }

    [Theory]
    [InlineData("prompt")]
    [InlineData("model")]
    [InlineData("pricing")]
    [InlineData("execution")]
    public void Sweep_summary_rejects_mixed_cohorts(string key)
    {
        string root = Path.Combine(Path.GetTempPath(), "sv-sweep-cohort-" + Guid.NewGuid().ToString("N"));
        try
        {
            var first = SummaryReport(1);
            var second = SummaryReport(3);
            var fingerprints = new Dictionary<string, string>(second.Fairness.ClassicFingerprints) { [key] = "different" };
            second = key == "execution" ? second with { ExecutionKind = "offline-fake-control" }
                : second with { Fairness = new(true, [], fingerprints, fingerprints) };
            string[] paths = [ComparisonWriter.Write(first, root), ComparisonWriter.Write(second, root)];
            Assert.Throws<InvalidOperationException>(() => SweepSummaryWriter.Write(paths, root));
            Assert.False(File.Exists(Path.Combine(root, "summary.html")));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void Sweep_summary_keeps_unknown_usage_and_analysis_unknown_instead_of_using_process_duration()
    {
        string root = Path.Combine(Path.GetTempPath(), "sv-sweep-unknown-" + Guid.NewGuid().ToString("N"));
        try
        {
            var report = SummaryReport(1);
            report = report with { RawTrials = report.RawTrials.Select(trial => trial with
                { TotalTokens = null, EstimatedCostUsd = null, AnalysisDurationMs = null }).ToArray() };
            SweepSummaryWriter.Write([ComparisonWriter.Write(report, root)], root);
            string text = File.ReadAllText(Path.Combine(root, "summary.md"));
            Assert.Contains("| unavailable | unavailable | unavailable |", text);
            Assert.Contains("unavailable tokens delta (unknown or zero baseline); both Partial", text);
            Assert.DoesNotContain("99.999", text);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
    }

    [Theory]
    [InlineData("Failed", "InvalidSchema", "InvalidModelOutput")]
    [InlineData("Completed", "Partial", null)]
    [InlineData("Incomplete", "Partial", "IncompleteEvidence")]
    [InlineData("TimedOut", "TimedOut", "TimedOut")]
    [InlineData("Capped", "Capped", "TurnLimit")]
    public void Model_task_outcomes_remain_valid_with_all_metrics_but_no_efficiency_win(string status, string match, string? category)
    {
        var original = SampleReport() with { ExecutionKind = "live-model-hyperlight" };
        var outcome = original.RawTrials[0] with
        {
            Status = status, Score = Exact with { MatchClass = match },
            Failure = category is null ? null : new(category, "Recorded model task outcome."),
            InputTokens = 80, OutputTokens = 20, AnalysisDurationMs = 3.25,
            Diagnostics = new("stop", 20, "captured", new Dictionary<string, int> { ["get_price"] = 13 }, 0, null),
        };
        var report = original with { RawTrials = [outcome, original.RawTrials[1]] };
        Assert.True(report.Outcome.IsValid);
        Assert.False(outcome.InfrastructureFailed);
        Assert.NotNull(report.ForPresentation().Classic.DurationMs.Median);
        Assert.Null(report.ForPresentation().Delta);
        foreach (string output in new[] { ComparisonWriter.RenderConsoleSummary(report), ComparisonWriter.RenderHtml(report),
            ComparisonWriter.RenderMarkdown(report), ComparisonWriter.RenderScalingHtml([report]) })
        {
            Assert.DoesNotContain("COMPARISON INVALID", output);
            Assert.DoesNotContain("faster", output);
        }
        string markdown = ComparisonWriter.RenderMarkdown(report);
        Assert.Contains($"{status} / {match}", markdown);
        Assert.Contains("| median | classic | correct 0/1", markdown);
        Assert.Contains("get_price=13", markdown);
        Assert.Contains("80 / 20", markdown);
        Assert.Contains("3.25", markdown);
        Assert.Contains("Est. USD", markdown);
        Assert.Contains("Callbacks", markdown);
    }

    [Theory]
    [InlineData("model-call")]
    [InlineData("worker-crash")]
    [InlineData("auth-with-invalid-schema")]
    [InlineData("prompt")]
    [InlineData("model")]
    [InlineData("telemetry")]
    [InlineData("missing-trial")]
    [InlineData("warmup-auth")]
    public void Infrastructure_or_harness_failures_invalidate_even_with_correct_or_model_task_output(string failure)
    {
        var report = SampleReport();
        report = failure switch
        {
            "model-call" => report with { RawTrials = [report.RawTrials[0] with { Failure = new("HttpRequestException", "Transport failed.") }, report.RawTrials[1]] },
            "worker-crash" => report with { RawTrials = [report.RawTrials[0] with { Failure = new("WorkerFailed", "Worker crashed.") }, report.RawTrials[1]] },
            "auth-with-invalid-schema" => report with { RawTrials = [report.RawTrials[0] with
            {
                Status = "Failed", Score = Exact with { MatchClass = "InvalidSchema" },
                Failure = new("ClientResultException", "Forbidden.", 403),
            }, report.RawTrials[1]] },
            "prompt" or "model" => report with { Fairness = report.Fairness with { Matched = false, Mismatches = [failure] } },
            "telemetry" => report with { RawTrials = [report.RawTrials[0] with { TelemetryValid = false }, report.RawTrials[1]] },
            "missing-trial" => report with { RawTrials = [report.RawTrials[0]] },
            "warmup-auth" => report with { WarmupFailures = [new("ClientResultException", "Forbidden in warm-up.", 403)] },
            _ => throw new InvalidOperationException("Unknown test failure."),
        };
        Assert.False(report.Outcome.IsValid);
        Assert.Contains("infrastructure/harness failure", report.Outcome.Message);
        Assert.Null(report.ForPresentation().Delta);
        Assert.Null(report.ForPresentation().Classic.DurationMs.Median);
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
