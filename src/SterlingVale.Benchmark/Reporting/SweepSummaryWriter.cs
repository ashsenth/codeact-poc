using System.Globalization;
using System.Net;
using System.Text;
using SterlingVale.Benchmark.Model;
using SterlingVale.Benchmark.Scoring;

namespace SterlingVale.Benchmark.Reporting;

/// <summary>Summarizes explicitly selected comparisons without collapsing household scopes.</summary>
public static class SweepSummaryWriter
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public static void Write(IReadOnlyList<string> comparisonPaths, string artifactsRoot)
    {
        ArgumentNullException.ThrowIfNull(comparisonPaths);
        ArgumentException.ThrowIfNullOrWhiteSpace(artifactsRoot);
        if (comparisonPaths.Count == 0) throw new InvalidOperationException("Select at least one comparison for the summary.");
        if (comparisonPaths.Any(path => !File.Exists(path)))
            throw new InvalidOperationException("Comparison JSON file not found. Supply existing paths; no analysis will be started.");
        var reports = comparisonPaths.Select(ComparisonWriter.Read).ToArray();
        ComparisonWriter.ValidateCohort(reports);
        if (reports.Any(report => report.PromptHash is null || report.ModelFingerprint is null ||
                report.Fairness.ClassicFingerprints.GetValueOrDefault("pricing") is null) ||
            reports.Select(report => report.Fairness.ClassicFingerprints["pricing"]).Distinct(StringComparer.Ordinal).Count() != 1 ||
            reports.Select(report => report.ExecutionKind).Distinct(StringComparer.Ordinal).Count() != 1)
            throw new InvalidOperationException("A summary requires one recorded model, pricing and execution-kind cohort.");

        var markdown = new StringBuilder("# Cross-profile results\n\n");
        markdown.AppendLine($"Shared prompt hash: `{reports[0].PromptHash}`. Model fingerprint: `{reports[0].ModelFingerprint}`.");
        markdown.AppendLine($"\nExecution kind: {reports[0].ExecutionKind}. Correct means Exact/WithinTolerance, not merely Completed.");
        markdown.AppendLine("\nMedians include every measured task outcome; warm-ups excluded. Unknown stays unavailable. Analysis time excludes worker startup/transport.");
        markdown.AppendLine("Costs are retail token estimates, not invoices. Configured cache rates apply to reported usage; reasoning is included once in output/total tokens. Analysis excludes recorded retry wait, which is reported separately. Unreported usage and infrastructure charges are excluded.");
        markdown.AppendLine("\nUnearnedCompleteness means claimed complete but missing required components. Detection only: no feedback/retries. Unassessed trials are disclosed; historical v2 reports are not rescored.");
        markdown.AppendLine("\n| Profile / scope | Mode | Correctness | UnearnedCompleteness | toolArgumentErrors (total) | TurnLimitReached | Output overflows | HTTP 429 | Outcomes | Median tokens | Median est. USD | Median analysis s | Median retry wait s | Individual trials (timelines) |");
        markdown.AppendLine("|---|---|---|---|---:|---:|---:|---:|---|---:|---:|---:|---:|---|");
        var html = new StringBuilder("<!DOCTYPE html><html lang=\"en\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width, initial-scale=1\"><title>Cross-profile results</title><style>body{font-family:system-ui;margin:2rem;color:#172033}table{border-collapse:collapse;font-size:.9rem}th,td{border:1px solid #bbb;padding:.5rem;text-align:left}code{overflow-wrap:anywhere}a{color:#0645ad}</style></head><body><h1>Cross-profile results</h1>");
        html.Append($"<p>Shared prompt hash: <code>{Enc(reports[0].PromptHash!)}</code>. Execution kind: {Enc(reports[0].ExecutionKind)}.</p>");
        html.Append("<p>Correct means Exact/WithinTolerance, not merely Completed. All measured outcomes remain in medians; warm-ups excluded. Unknown stays unavailable. Analysis excludes worker startup/transport and recorded retry wait. Costs use configured cache/context rates for reported usage; reasoning is included once in output tokens. Estimates are not invoices; unreported usage and infrastructure charges are excluded.</p>");
        html.Append("<p>UnearnedCompleteness means claimed complete but missing required components. Detection only: no feedback/retries. Unassessed trials are disclosed; historical v2 reports are not rescored.</p>");
        html.Append("<table><tr><th>Profile / scope</th><th>Mode</th><th>Correctness</th><th>UnearnedCompleteness</th><th>toolArgumentErrors (total)</th><th>TurnLimitReached</th><th>Output overflows</th><th>HTTP 429</th><th>Outcomes</th><th>Median tokens</th><th>Median est. USD</th><th>Median analysis s</th><th>Median retry wait s</th><th>Individual trials (timelines)</th></tr>");
        var deltas = new List<string[]>();
        var warmups = new List<string[]>();
        for (int index = 0; index < reports.Length; index++)
        {
            var report = reports[index];
            string scope = Scope(report);
            string comparisonLink = Link(artifactsRoot, Path.ChangeExtension(comparisonPaths[index], ".html"));
            var classic = Measure(report, "classic");
            var codeact = Measure(report, "codeact");
            foreach (var row in new[] { classic, codeact })
            {
                var trials = report.RawTrials.Where(trial => trial.Mode == row.Mode).OrderBy(trial => trial.PairId).ToArray();
                string Total(Func<TrialResult, int?> select) => trials.Length == 0 || trials.Any(trial => select(trial) is null)
                    ? "unavailable" : trials.Sum(trial => select(trial)!.Value).ToString(Invariant);
                string[] cells = [scope, row.Mode, row.Trials == 0 ? "not measured" : $"{row.Correct}/{row.Trials} ({Format(100m * row.Correct / row.Trials, "0.0")}%)",
                    row.Unearned, row.ToolErrors, Total(trial => trial.TurnLimitReached), Total(trial => trial.SandboxOutputOverflowErrors),
                    Total(trial => trial.Http429Count), row.Outcomes, Format(row.Tokens, "0"), Format(row.Cost, "0.0000000"),
                    Format(row.AnalysisMs / 1000m, "0.000"), Format(Median(trials.Select(trial => (decimal?)trial.RetryWaitMs)) / 1000m, "0.000")];
                string Timeline(TrialResult trial) => Link(artifactsRoot, Path.Combine(Path.GetDirectoryName(comparisonPaths[index])!,
                    "..", "runs", trial.RunId, "timeline.md"));
                string mdTrials = string.Join(", ", trials.Select(trial => $"[{trial.PairId}]({Timeline(trial)})"));
                markdown.AppendLine($"| [{scope}]({comparisonLink}) | {string.Join(" | ", cells.Skip(1))} | {mdTrials} |");
                html.Append($"<tr><td><a href=\"{Enc(comparisonLink)}\">{Enc(scope)}</a></td>" +
                    string.Concat(cells.Skip(1).Select(value => $"<td>{Enc(value)}</td>")) +
                    $"<td>{string.Join(", ", trials.Select(trial => $"<a href=\"{Enc(Timeline(trial))}\">{trial.PairId}</a>"))}</td></tr>");
            }
            string context = !report.Outcome.IsValid ? "INVALID infrastructure/harness evidence"
                : classic.Correct == classic.Trials && codeact.Correct == codeact.Trials
                    ? $"both oracle-correct ({classic.Correct}/{classic.Trials}, {codeact.Correct}/{codeact.Trials}); descriptive, not causal evidence"
                    : classic.Outcomes == $"Partial={classic.Trials}" && codeact.Outcomes == $"Partial={codeact.Trials}"
                        ? "both Partial; not a correctness-equivalent win"
                        : $"Classic {classic.Outcomes} ({classic.Correct}/{classic.Trials} correct), CodeAct {codeact.Outcomes} ({codeact.Correct}/{codeact.Trials} correct); not a correctness-equivalent win";
            context += $"; UnearnedCompleteness Classic {classic.Unearned}, CodeAct {codeact.Unearned}";
            context += $"; toolArgumentErrors Classic {classic.ToolErrors}, CodeAct {codeact.ToolErrors}";
            deltas.Add([scope, Delta(classic.Tokens, codeact.Tokens, "tokens", context),
                Delta(classic.Cost, codeact.Cost, "est. cost", context),
                Delta(classic.AnalysisMs, codeact.AnalysisMs, "analysis time", context)]);
            foreach (var trial in report.WarmupTrials)
                warmups.Add([scope, trial.Mode, $"{trial.Status} / {trial.Score.MatchClass}",
                    trial.ToolArgumentErrors?.ToString(Invariant) ?? "unavailable"]);
        }
        html.Append("</table><h2>Descriptive deltas: CodeAct relative to Classic</h2><p>Each delta carries its correctness context. Lower resource use for an incorrect answer is not an efficiency win.</p><table><tr><th>Profile / scope</th><th>Tokens</th><th>Est. cost</th><th>Analysis time</th></tr>");
        markdown.AppendLine("\n## Descriptive deltas: CodeAct relative to Classic\n\nEach delta carries its correctness context. Lower resource use for an incorrect answer is not an efficiency win.\n");
        markdown.AppendLine("| Profile / scope | Tokens | Est. cost | Analysis time |\n|---|---|---|---|");
        foreach (var row in deltas)
        {
            markdown.AppendLine("| " + string.Join(" | ", row) + " |");
            html.Append("<tr>" + string.Concat(row.Select(value => $"<td>{Enc(value)}</td>")) + "</tr>");
        }
        html.Append("</table><h2>Warm-up outcomes (not measured)</h2><table><tr><th>Scope</th><th>Mode</th><th>Status / oracle</th><th>toolArgumentErrors</th></tr>");
        markdown.AppendLine("\n## Warm-up outcomes (not measured)\n\n| Scope | Mode | Status / oracle | toolArgumentErrors |\n|---|---|---|---:|");
        foreach (var row in warmups)
        {
            markdown.AppendLine("| " + string.Join(" | ", row) + " |");
            html.Append("<tr>" + string.Concat(row.Select(value => $"<td>{Enc(value)}</td>")) + "</tr>");
        }
        html.Append("</table></body></html>");
        Directory.CreateDirectory(artifactsRoot);
        File.WriteAllText(Path.Combine(artifactsRoot, "summary.md"), markdown.ToString(), new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(artifactsRoot, "summary.html"), html.ToString(), new UTF8Encoding(false));
    }

    private sealed record Measurements(string Mode, int Correct, int Trials, string Unearned, string ToolErrors, string Outcomes,
        decimal? Tokens, decimal? Cost, decimal? AnalysisMs);

    private static Measurements Measure(ComparisonReport report, string mode)
    {
        var trials = report.RawTrials.Where(trial => trial.Mode == mode).ToArray();
        bool valid = report.Outcome.IsValid;
        string outcomes = string.Join(", ", trials.GroupBy(trial => trial.Score.MatchClass)
            .OrderBy(group => group.Key, StringComparer.Ordinal).Select(group => $"{group.Key}={group.Count()}"));
        return new(mode, trials.Count(trial => trial.Score.MatchClass is CorrectnessScorer.Match.Exact or CorrectnessScorer.Match.WithinTolerance),
            trials.Length, trials.Length == 0 ? "unassessed (no measured trials)" :
                $"{trials.Count(trial => trial.Score.MatchClass == CorrectnessScorer.Match.UnearnedCompleteness)}/{trials.Length} (assessed {trials.Count(trial => trial.Score.CompletenessAssessed)}/{trials.Length})",
            trials.Length == 0 || trials.Any(trial => trial.ToolArgumentErrors is null) ? "unavailable" :
                trials.Sum(trial => trial.ToolArgumentErrors!.Value).ToString(Invariant),
            valid ? outcomes : $"{report.Outcome.Message}; {outcomes}",
            valid ? Median(trials.Select(trial => (decimal?)trial.TotalTokens)) : null,
            valid ? Median(trials.Select(trial => trial.EstimatedCostUsd)) : null,
            valid ? Median(trials.Select(trial => (decimal?)trial.AnalysisDurationMs)) : null);
    }

    private static decimal? Median(IEnumerable<decimal?> source)
    {
        var values = source.ToArray();
        if (values.Length == 0 || values.Any(value => value is null)) return null;
        var sorted = values.Select(value => value!.Value).Order().ToArray();
        int middle = sorted.Length / 2;
        return sorted.Length % 2 == 1 ? sorted[middle] : (sorted[middle - 1] + sorted[middle]) / 2m;
    }

    private static string Delta(decimal? baseline, decimal? value, string metric, string context) =>
        baseline is > 0 && value is not null
            ? $"{((value.Value - baseline.Value) / baseline.Value * 100m).ToString("+0.0;-0.0;0.0", Invariant)}% {metric}; {context}"
            : $"unavailable {metric} delta (unknown or zero baseline); {context}";

    private static string Scope(ComparisonReport report) =>
        $"{report.Profile} (N={report.ScopedHouseholdCount?.ToString(Invariant) ?? report.DatasetHouseholdCount?.ToString(Invariant) ?? "unavailable"}" +
        (report.ScopedHouseholdCount is not null && report.ScopedHouseholdCount == report.DatasetHouseholdCount ? ", full)" : ")");

    private static string Format(decimal? value, string format) => value?.ToString(format, Invariant) ?? "unavailable";
    private static string Enc(string text) => WebUtility.HtmlEncode(text);
    private static string Link(string root, string path) =>
        string.Join("/", Path.GetRelativePath(Path.GetFullPath(root), Path.GetFullPath(path))
            .Replace('\\', '/').Split('/').Select(Uri.EscapeDataString));
}
