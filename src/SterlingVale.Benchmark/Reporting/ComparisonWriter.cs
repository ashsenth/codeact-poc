using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using SterlingVale.Benchmark.Model;

namespace SterlingVale.Benchmark.Reporting;

/// <summary>Serializes comparison reports to JSON and renders human-readable Markdown.</summary>
public static class ComparisonWriter
{
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    private static readonly JsonSerializerOptions Json = CreateJson();

    /// <summary>Writes {artifactsRoot}/comparisons/{id}.json and .md; returns the JSON path.</summary>
    public static string Write(ComparisonReport report, string artifactsRoot)
    {
        ArgumentNullException.ThrowIfNull(report);
        string dir = Path.Combine(artifactsRoot, "comparisons");
        Directory.CreateDirectory(dir);

        string jsonPath = Path.Combine(dir, report.ComparisonId + ".json");
        File.WriteAllText(jsonPath, JsonSerializer.Serialize(report, Json), Utf8);
        File.WriteAllText(Path.Combine(dir, report.ComparisonId + ".md"), RenderMarkdown(report), Utf8);
        return jsonPath;
    }

    /// <summary>Reads a comparison report from its JSON file.</summary>
    public static ComparisonReport Read(string jsonPath) =>
        JsonSerializer.Deserialize<ComparisonReport>(File.ReadAllText(jsonPath), Json)
        ?? throw new InvalidDataException($"Could not read comparison from {jsonPath}.");

    /// <summary>Renders a comparison report to Markdown.</summary>
    public static string RenderMarkdown(ComparisonReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        var sb = new StringBuilder();
        sb.Append("# Sterling Vale — Classic vs CodeAct comparison\n\n");
        sb.Append("> SYNTHETIC analysis only — not investment advice. This benchmark reports measured\n");
        sb.Append("> numbers for a controlled A/B of orchestration only. **No performance outcome is\n");
        sb.Append("> guaranteed**; results depend on the model, dataset, and environment.\n\n");

        sb.Append($"- **Comparison id:** `{report.ComparisonId}`\n");
        sb.Append($"- **Generated (UTC):** {report.GeneratedAtUtc}\n");
        sb.Append($"- **Profile:** {report.Profile}\n");
        sb.Append($"- **Dataset hash:** `{report.DatasetHash}`\n");
        sb.Append($"- **Randomization seed:** {report.RandomizationSeed}\n");
        sb.Append($"- **Warm-ups per mode:** {report.WarmupsPerMode}  •  **Measured trials:** {report.MeasuredTrials}\n\n");

        sb.Append("## Fairness\n\n");
        sb.Append(report.Fairness.Matched
            ? "All fairness fingerprints match across modes. ✔\n\n"
            : $"**FAIRNESS FAILURE** — mismatched: {string.Join(", ", report.Fairness.Mismatches)} ✘\n\n");
        sb.Append("| Fingerprint | Classic | CodeAct |\n|---|---|---|\n");
        foreach (var key in report.Fairness.ClassicFingerprints.Keys.OrderBy(k => k, StringComparer.Ordinal))
        {
            string classic = report.Fairness.ClassicFingerprints[key];
            string codeact = report.Fairness.CodeActFingerprints.GetValueOrDefault(key, "(missing)");
            string mark = classic == codeact ? "=" : "≠";
            sb.Append($"| {key} | `{Short(classic)}` | `{Short(codeact)}` {mark} |\n");
        }

        sb.Append('\n');
        sb.Append("## Summary\n\n");
        sb.Append("| Metric | Classic | CodeAct |\n|---|---|---|\n");
        AppendRow(sb, "Trials", report.Classic.Trials.ToString(), report.CodeAct.Trials.ToString());
        AppendRow(sb, "Duration median (ms)", F(report.Classic.DurationMs.Median), F(report.CodeAct.DurationMs.Median));
        AppendRow(sb, "Duration min (ms)", F(report.Classic.DurationMs.Min), F(report.CodeAct.DurationMs.Min));
        AppendRow(sb, "Duration max (ms)", F(report.Classic.DurationMs.Max), F(report.CodeAct.DurationMs.Max));
        AppendRow(sb, "Total tokens median", Tokens(report.Classic.TotalTokens), Tokens(report.CodeAct.TotalTokens));
        AppendRow(sb, "Flagged precision (mean)", F(report.Classic.Correctness.MeanFlaggedPrecision), F(report.CodeAct.Correctness.MeanFlaggedPrecision));
        AppendRow(sb, "Flagged recall (mean)", F(report.Classic.Correctness.MeanFlaggedRecall), F(report.CodeAct.Correctness.MeanFlaggedRecall));
        AppendRow(sb, "Breach precision (mean)", F(report.Classic.Correctness.MeanBreachPrecision), F(report.CodeAct.Correctness.MeanBreachPrecision));
        AppendRow(sb, "Breach recall (mean)", F(report.Classic.Correctness.MeanBreachRecall), F(report.CodeAct.Correctness.MeanBreachRecall));
        AppendRow(sb, "Allocation MAE (mean)", report.Classic.Correctness.MeanAllocationMae.ToString(), report.CodeAct.Correctness.MeanAllocationMae.ToString());
        AppendRow(sb, "Trade-notional MAE (mean)", report.Classic.Correctness.MeanTradeNotionalMae.ToString(), report.CodeAct.Correctness.MeanTradeNotionalMae.ToString());
        AppendRow(sb, "Max allocation error", report.Classic.Correctness.MaxAllocationError.ToString(), report.CodeAct.Correctness.MaxAllocationError.ToString());
        AppendRow(sb, "Exact / WithinTol / Partial / Failed",
            MatchCounts(report.Classic.Correctness), MatchCounts(report.CodeAct.Correctness));

        sb.Append('\n');
        sb.Append("## Raw trials (none removed)\n\n");
        sb.Append("| Pair | Order | Mode | Duration (ms) | Status | Tools | execute_code | Tokens | Match |\n");
        sb.Append("|---|---|---|---|---|---|---|---|---|\n");
        foreach (var t in report.RawTrials)
        {
            sb.Append($"| {t.PairId} | {t.OrderInPair} | {t.Mode} | {F(t.DurationMs)} | {t.Status} | {t.ToolCallCount} | {t.ExecuteCodeCallCount} | {(t.TotalTokens?.ToString() ?? "unknown")} | {t.Score.MatchClass} |\n");
        }

        return sb.ToString();
    }

    private static void AppendRow(StringBuilder sb, string label, string classic, string codeact) =>
        sb.Append($"| {label} | {classic} | {codeact} |\n");

    private static string MatchCounts(CorrectnessStats c) =>
        $"{c.ExactCount} / {c.WithinToleranceCount} / {c.PartialCount} / {c.FailedCount}";

    private static string Tokens(TokenStats t) =>
        t.Median is null ? "unknown" : $"{t.Median}{(t.Complete ? string.Empty : " (partial)")}";

    private static string F(double value) => value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);

    private static string Short(string hash) => hash.Length <= 12 ? hash : hash[..12];

    private static JsonSerializerOptions CreateJson()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
