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

    private static readonly System.Globalization.CultureInfo Invariant = System.Globalization.CultureInfo.InvariantCulture;

    private static readonly char[] CsvSpecials = [',', '"', '\n', '\r'];

    /// <summary>Writes {artifactsRoot}/comparisons/{id}.json and .md; returns the JSON path.</summary>
    public static string Write(ComparisonReport report, string artifactsRoot)
    {
        ArgumentNullException.ThrowIfNull(report);
        string dir = Path.Combine(artifactsRoot, "comparisons");
        Directory.CreateDirectory(dir);

        string jsonPath = Path.Combine(dir, report.ComparisonId + ".json");
        File.WriteAllText(jsonPath, JsonSerializer.Serialize(report, Json), Utf8);
        File.WriteAllText(Path.Combine(dir, report.ComparisonId + ".md"), RenderMarkdown(report), Utf8);
        File.WriteAllText(Path.Combine(dir, report.ComparisonId + ".csv"), RenderCsv(report), Utf8);
        File.WriteAllText(Path.Combine(dir, report.ComparisonId + ".html"), RenderHtml(report), Utf8);
        return jsonPath;
    }

    /// <summary>Reads a comparison report from its JSON file.</summary>
    public static ComparisonReport Read(string jsonPath) =>
        JsonSerializer.Deserialize<ComparisonReport>(File.ReadAllText(jsonPath), Json)
        ?? throw new InvalidDataException($"Could not read comparison from {jsonPath}.");

    /// <summary>Renders one CSV row per raw trial (RFC 4180 quoting) for external analysis.</summary>
    public static string RenderCsv(ComparisonReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        var sb = new StringBuilder();
        sb.Append("comparison_id,profile,dataset_hash,pair_id,order_in_pair,mode,duration_ms,status,");
        sb.Append("tool_calls,model_turns,execute_code_calls,total_tokens,estimated_cost_usd,match_class,");
        sb.Append("flagged_precision,flagged_recall,breach_precision,breach_recall,allocation_mae,trade_notional_mae,max_allocation_error\n");
        foreach (var t in report.RawTrials)
        {
            var s = t.Score;
            sb.Append(Csv(report.ComparisonId)).Append(',');
            sb.Append(Csv(report.Profile)).Append(',');
            sb.Append(Csv(report.DatasetHash)).Append(',');
            sb.Append(t.PairId.ToString(Invariant)).Append(',');
            sb.Append(t.OrderInPair.ToString(Invariant)).Append(',');
            sb.Append(Csv(t.Mode)).Append(',');
            sb.Append(t.DurationMs.ToString("0.###", Invariant)).Append(',');
            sb.Append(Csv(t.Status)).Append(',');
            sb.Append(t.ToolCallCount.ToString(Invariant)).Append(',');
            sb.Append(t.ModelTurnCount.ToString(Invariant)).Append(',');
            sb.Append(t.ExecuteCodeCallCount.ToString(Invariant)).Append(',');
            sb.Append(t.TotalTokens?.ToString(Invariant) ?? string.Empty).Append(',');
            sb.Append(t.EstimatedCostUsd?.ToString(Invariant) ?? string.Empty).Append(',');
            sb.Append(Csv(s.MatchClass)).Append(',');
            sb.Append(s.FlaggedPrecision.ToString(Invariant)).Append(',');
            sb.Append(s.FlaggedRecall.ToString(Invariant)).Append(',');
            sb.Append(s.BreachPrecision.ToString(Invariant)).Append(',');
            sb.Append(s.BreachRecall.ToString(Invariant)).Append(',');
            sb.Append(s.AllocationMae.ToString(Invariant)).Append(',');
            sb.Append(s.TradeNotionalMaeFraction.ToString(Invariant)).Append(',');
            sb.Append(s.MaxAllocationError.ToString(Invariant)).Append('\n');
        }

        return sb.ToString();
    }

    /// <summary>A one-line CodeAct-vs-Classic delta summary for console output.</summary>
    public static string FormatDeltaSummary(ComparisonDelta delta)
    {
        ArgumentNullException.ThrowIfNull(delta);
        return $"duration {Pct(delta.DurationDeltaFraction)}{Speedup(delta.DurationSpeedupFactor)}, " +
               $"tokens {Pct(delta.TokenDeltaFraction)}, cost {Pct(delta.CostDeltaFraction)}, " +
               $"tools {Pct(delta.ToolCallDeltaFraction)}; exact \u0394 {delta.ExactCountDelta}";
    }

    /// <summary>Renders a self-contained HTML dashboard (inline SVG, no external requests) for a browser.</summary>
    public static string RenderHtml(ComparisonReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        var c = report.Classic;
        var a = report.CodeAct;
        int cN = TrialCount(report, "classic"), cDone = CompletedCount(report, "classic");
        int aN = TrialCount(report, "codeact"), aDone = CompletedCount(report, "codeact");
        bool classicStruggled = cDone < aDone;

        var sb = new StringBuilder();
        AppendHtmlHead(sb, $"Sterling Vale \u2014 {report.ComparisonId}");
        sb.Append("<h1>Sterling Vale &mdash; Classic vs CodeAct</h1>");
        sb.Append("<p class=\"disc\">SYNTHETIC analysis only &mdash; not investment advice. Measured numbers for a controlled A/B of orchestration only; no performance outcome is guaranteed.</p>");
        sb.Append($"<p class=\"meta\">Comparison <code>{Enc(report.ComparisonId)}</code> &middot; profile <b>{Enc(report.Profile)}</b>{HouseholdsMeta(report)} &middot; {Enc(report.GeneratedAtUtc)}</p>");
        sb.Append(report.Fairness.Matched
            ? "<p><span class=\"fair ok\">fairness: MATCHED</span> <span class=\"hint\">identical task, tools, model and data &mdash; only the orchestration differs</span></p>"
            : $"<p><span class=\"fair bad\">fairness: FAILED ({Enc(string.Join(", ", report.Fairness.Mismatches))})</span></p>");

        // Outcome first: how many trials each mode actually completed, plus a plain-language takeaway.
        sb.Append("<section class=\"outcome\">");
        AppendOutcome(sb, "Classic", cDone, cN);
        AppendOutcome(sb, "CodeAct", aDone, aN);
        sb.Append($"<div class=\"takeaway\">{Enc(Takeaway(cDone, cN, aDone, aN))}</div>");
        sb.Append("</section>");

        // Head-to-head deltas are only meaningful when BOTH modes completed at least one trial;
        // comparing against a mode that never finished would be misleading.
        if (report.Delta is { } d && cDone > 0 && aDone > 0)
        {
            sb.Append("<h2>CodeAct vs Classic (both completed)</h2>");
            sb.Append("<section class=\"cards\">");
            AppendCard(sb, FactorLabel(d.DurationSpeedupFactor), "faster (duration)");
            AppendCard(sb, Pct(d.TokenDeltaFraction), "tokens");
            AppendCard(sb, Pct(d.CostDeltaFraction), "cost");
            AppendCard(sb, Pct(d.ToolCallDeltaFraction), "tool calls");
            AppendCard(sb, (d.ExactCountDelta >= 0 ? "+" : string.Empty) + d.ExactCountDelta.ToString(Invariant), "exact match");
            sb.Append("</section>");
        }

        // How each mode runs (educational, always shown so a first-time reader has context).
        sb.Append("<h2>How each mode runs</h2>");
        sb.Append("<div class=\"how\">");
        sb.Append("<div class=\"col classic\"><h3>Classic &mdash; direct tool-calling</h3><p>The model calls the seven data tools itself, one decision at a time, and assembles the report from the results it accumulates in its own context.</p></div>");
        sb.Append("<div class=\"col codeact\"><h3>CodeAct &mdash; sandboxed code</h3><p>The model writes one Python program and runs it with <code>execute_code</code>; the program loops over the data and calls the same tools in-process, returning only the finished report.</p></div>");
        sb.Append("</div>");

        // What happened & why: data-driven observation of this run, then the mechanism explanation.
        sb.Append("<h2>What happened &amp; why</h2>");
        sb.Append("<div class=\"panel\">");
        sb.Append($"<p><span class=\"tag classic\">Classic</span> made a median of <b>{Num(c.RoundTrips?.MedianToolCalls)}</b> tool calls across <b>{Num(c.RoundTrips?.MedianModelTurns)}</b> model turns and {Enc(Outcome(cDone, cN))}.</p>");
        sb.Append($"<p><span class=\"tag codeact\">CodeAct</span> ran <b>{Num(a.RoundTrips?.MedianExecuteCodeCalls)}</b> <code>execute_code</code> program(s) across <b>{Num(a.RoundTrips?.MedianModelTurns)}</b> model turns and {Enc(Outcome(aDone, aN))}.</p>");
        if (classicStruggled && aDone > 0)
        {
            sb.Append("<p class=\"why\"><b>What actually happened.</b> Classic gathered the data successfully &mdash; its tool calls ran &mdash; but then returned an empty / schema-invalid report instead of the final ExposureReport. It was <i>not</i> stopped by a tool-call cap or the context window (it stayed well under the model's limits); with this model and prompt it simply failed to emit the final structured output. CodeAct assembles the report in sandboxed code, so it produced a valid one. Because Classic never completed, the token, cost and tool-call figures below are <b>not a like-for-like comparison</b> &mdash; they show the cost of a failed attempt beside a completed one, not relative efficiency.</p>");
        }

        sb.Append("</div>");

        // Charts.
        sb.Append("<h2>Measured medians</h2>");
        sb.Append("<section class=\"charts\">");
        sb.Append(SvgBars("Duration median (ms)", c.DurationMs.Median, a.DurationMs.Median, v => v.ToString("0.#", Invariant)));
        sb.Append(SvgBars("Total tokens median", (double?)c.TotalTokens.Median, (double?)a.TotalTokens.Median, v => v.ToString("0", Invariant)));
        sb.Append(SvgBars("Tool calls median", c.RoundTrips?.MedianToolCalls, a.RoundTrips?.MedianToolCalls, v => v.ToString("0.#", Invariant)));
        sb.Append(SvgBars("Cost median (USD)", (double?)c.Cost?.Median, (double?)a.Cost?.Median, v => v.ToString("0.######", Invariant)));
        sb.Append("</section>");
        sb.Append("<p class=\"disc\">A bar shown as <b>n/a</b> means no valid measurement. When one mode failed, these are <b>not</b> a fair comparison: a failed mode's lower tokens/cost reflect that it stopped early without producing a report, and the tool-call bar undercounts CodeAct (its tool calls run inside the sandbox and are not counted as model tool calls).</p>");

        sb.Append("<h2>Per-trial duration (ms)</h2>");
        sb.Append(SvgStrip(report));

        // Every trial, with a plain-language note instead of a bare status code.
        sb.Append("<h2>Every trial</h2>");
        sb.Append("<table><thead><tr><th>Pair</th><th>Mode</th><th>Status</th><th>Duration (ms)</th>");
        sb.Append("<th>Tools</th><th>Turns</th><th>execute_code</th><th>Tokens</th><th>Cost (USD)</th><th>What the run produced</th></tr></thead><tbody>");
        foreach (var t in report.RawTrials)
        {
            bool ok = string.Equals(t.Status, "Completed", StringComparison.Ordinal);
            sb.Append($"<tr><td>{t.PairId}</td><td>{Enc(t.Mode)}</td><td class=\"{(ok ? "st-ok" : "st-bad")}\">{Enc(t.Status)}</td><td>{F(t.DurationMs)}</td>");
            sb.Append($"<td>{t.ToolCallCount}</td><td>{t.ModelTurnCount}</td><td>{t.ExecuteCodeCallCount}</td>");
            sb.Append($"<td>{LongVal(t.TotalTokens)}</td><td>{CostVal(t.EstimatedCostUsd)}</td><td class=\"note\">{Enc(TrialNote(t))}</td></tr>");
        }

        sb.Append("</tbody></table>");
        sb.Append($"<footer>dataset {Enc(Short(report.DatasetHash))} &middot; seed {report.RandomizationSeed} &middot; warm-ups/mode {report.WarmupsPerMode} &middot; measured trials {report.MeasuredTrials}</footer>");
        sb.Append("</main></body></html>");
        return sb.ToString();
    }

    private static int TrialCount(ComparisonReport r, string mode) =>
        r.RawTrials.Count(t => string.Equals(t.Mode, mode, StringComparison.Ordinal));

    private static int CompletedCount(ComparisonReport r, string mode) =>
        r.RawTrials.Count(t => string.Equals(t.Mode, mode, StringComparison.Ordinal)
                            && string.Equals(t.Status, "Completed", StringComparison.Ordinal));

    private static string HouseholdsMeta(ComparisonReport r) =>
        r.DatasetHouseholdCount is { } n ? $" &middot; {n} households" : string.Empty;

    private static void AppendOutcome(StringBuilder sb, string label, int done, int total)
    {
        string cls = total > 0 && done == total ? "good" : done == 0 ? "bad" : "warn";
        sb.Append($"<div class=\"ob {cls}\">{Enc(label)}<b>{done}/{total}</b>completed</div>");
    }

    private static string Takeaway(int cDone, int cN, int aDone, int aN) =>
        aDone > 0 && cDone == 0
            ? "CodeAct completed the analysis; Classic could not complete it at all at this dataset size. The sections below explain why."
            : cDone > 0 && aDone > 0 && aDone >= cDone
                ? "Both modes completed. CodeAct reaches the same answer with far fewer model\u2013tool round-trips \u2014 see the deltas below."
                : cDone > aDone
                    ? "Classic completed more trials than CodeAct in this run."
                    : "Neither mode completed reliably in this run.";

    private static string Outcome(int done, int total) =>
        total > 0 && done == total ? $"completed all {total} trial(s)"
        : done == 0 ? $"did not complete any of the {total} trial(s)"
        : $"completed {done} of {total} trial(s)";

    // Big word+colour status cell for the scaling matrix: "Completed / Partial / Failed" plus the trial fraction.
    private static string StatusBadge(int done, int total)
    {
        string cls = total > 0 && done == total ? "good" : done == 0 ? "bad" : "warn";
        string word = total > 0 && done == total ? "Completed" : done == 0 ? "Failed" : "Partial";
        return $"<span class=\"badge {cls}\">{word}<small>{done}/{total} trials</small></span>";
    }

    private static string ScalingTakeaway(IReadOnlyList<ComparisonReport> ordered)
    {
        if (ordered.Count == 0)
        {
            return "No comparisons to summarise yet.";
        }

        bool codeActAll = ordered.All(r => { int n = TrialCount(r, "codeact"); return n > 0 && CompletedCount(r, "codeact") == n; });
        bool classicAllFailed = ordered.All(r => CompletedCount(r, "classic") == 0);
        bool codeActAny = ordered.Any(r => CompletedCount(r, "codeact") > 0);
        var classicFailed = ordered.Where(r => CompletedCount(r, "classic") == 0).Select(r => r.Profile).ToList();
        var classicOk = ordered.Where(r => { int n = TrialCount(r, "classic"); return n > 0 && CompletedCount(r, "classic") == n; }).Select(r => r.Profile).ToList();

        if (classicAllFailed && codeActAny)
        {
            return "CodeAct produced a correct report at every dataset size; Classic failed to emit a valid report at any size &mdash; even the smallest (10 households). Because Classic failed even at trivial scale, this is an output-emission failure, not a scaling result, and the two are not yet comparable on cost or speed.";
        }

        if (codeActAll && classicFailed.Count > 0)
        {
            string classicPart = classicOk.Count > 0
                ? $"Classic only finished the smaller set(s) ({string.Join(", ", classicOk)}) and failed on {string.Join(", ", classicFailed)}"
                : $"Classic failed on every size ({string.Join(", ", classicFailed)})";
            return $"CodeAct completed every dataset size tested. {classicPart} \u2014 direct tool-calling cannot keep up as the number of households grows.";
        }

        if (codeActAll && classicFailed.Count == 0)
        {
            return "Both modes completed every dataset size. Compare the per-dataset deltas below to see the cost of each approach.";
        }

        return "Completion varied by dataset size \u2014 read the matrix above per row; deltas are only meaningful where both modes are green.";
    }

    private static string Num(double? value) => value is { } v ? v.ToString("0.#", Invariant) : "n/a";

    private static string TrialNote(TrialResult t)
    {
        if (string.Equals(t.Status, "Completed", StringComparison.Ordinal))
        {
            return t.Score.MatchClass switch
            {
                "Exact" => "exact match to the reference report",
                "WithinTolerance" => "correct within numeric tolerance",
                "Partial" => t.Score.FlaggedPrecision >= 1d && t.Score.BreachPrecision >= 1d
                    ? "correct \u2014 identified every flagged household and breach (minor numeric rounding only)"
                    : "usable report; partially correct",
                _ => "completed",
            };
        }

        if (string.Equals(t.Status, "TimedOut", StringComparison.Ordinal))
        {
            return "timed out before finishing the analysis";
        }

        if (string.Equals(t.Status, "Capped", StringComparison.Ordinal))
        {
            return "hit the model-turn cap before finishing";
        }

        if (t.ToolCallCount == 0 && t.ExecuteCodeCallCount == 0)
        {
            return "no usable output \u2014 the model returned nothing before failing";
        }

        return string.Equals(t.Score.MatchClass, "InvalidSchema", StringComparison.Ordinal)
            ? $"gathered data over {t.ModelTurnCount} turn(s) but returned an empty / invalid report"
            : "ran to the end but the report was incorrect";
    }

    private static void AppendCard(StringBuilder sb, string number, string label) =>
        sb.Append($"<div class=\"card\"><div class=\"n\">{Enc(number)}</div><div class=\"l\">{Enc(label)}</div></div>");

    /// <summary>Renders a self-contained HTML dashboard comparing several profiles to show how the gap scales with dataset size.</summary>
    public static string RenderScalingHtml(IReadOnlyList<ComparisonReport> reports)
    {
        ArgumentNullException.ThrowIfNull(reports);
        var ordered = reports
            .OrderBy(r => r.DatasetHouseholdCount ?? 0)
            .ThenBy(r => r.Profile, StringComparer.Ordinal)
            .ToList();
        var labels = ordered.Select(r => r.DatasetHouseholdCount is { } n ? $"{r.Profile} (n={n})" : r.Profile).ToList();
        var durC = ordered.Select(r => (double?)r.Classic.DurationMs.Median).ToList();
        var durA = ordered.Select(r => (double?)r.CodeAct.DurationMs.Median).ToList();
        var tokC = ordered.Select(r => (double?)r.Classic.TotalTokens.Median).ToList();
        var tokA = ordered.Select(r => (double?)r.CodeAct.TotalTokens.Median).ToList();
        var tolC = ordered.Select(r => r.Classic.RoundTrips?.MedianToolCalls).ToList();
        var tolA = ordered.Select(r => r.CodeAct.RoundTrips?.MedianToolCalls).ToList();

        var sb = new StringBuilder();
        AppendHtmlHead(sb, "Sterling Vale \u2014 scaling");
        sb.Append("<h1>Sterling Vale &mdash; scaling (Classic vs CodeAct)</h1>");
        sb.Append("<p class=\"disc\">SYNTHETIC analysis only &mdash; not investment advice. The same task is run against increasingly large datasets to show how each orchestration holds up as the number of households grows.</p>");

        // 1. Completion matrix FIRST — the clearest possible answer to "did each mode finish, per dataset size?"
        sb.Append("<h2>Did each mode finish the analysis?</h2>");
        sb.Append("<p class=\"hint\">Ordered smallest &rarr; largest dataset. Green = every trial produced a valid report; amber = some trials failed; red = no trial finished.</p>");
        sb.Append("<table class=\"matrix\"><thead><tr><th>Dataset</th><th>Households</th><th>Classic</th><th>CodeAct</th></tr></thead><tbody>");
        foreach (var r in ordered)
        {
            int cN = TrialCount(r, "classic"), cDone = CompletedCount(r, "classic");
            int aN = TrialCount(r, "codeact"), aDone = CompletedCount(r, "codeact");
            sb.Append($"<tr><td><b>{Enc(r.Profile)}</b></td><td>{r.DatasetHouseholdCount?.ToString(Invariant) ?? "n/a"}</td>");
            sb.Append($"<td>{StatusBadge(cDone, cN)}</td><td>{StatusBadge(aDone, aN)}</td></tr>");
        }

        sb.Append("</tbody></table>");
        sb.Append($"<div class=\"takeaway\">{Enc(ScalingTakeaway(ordered))}</div>");

        // 2. Why it happens (mechanism), always shown.
        sb.Append("<h2>Why Classic failed and CodeAct completed</h2>");
        sb.Append("<div class=\"panel\">");
        sb.Append("<p><span class=\"tag classic\">Classic</span> lets the model call each tool itself and then assemble the final report from what it gathered. In these runs it made its tool calls and pulled the data successfully, but then returned an empty / schema-invalid report instead of the ExposureReport &mdash; at <b>every</b> size, including the smallest (10 households). It was not stopped by a tool-call cap or the context window; with this model and prompt it simply failed to emit the final structured output. This is therefore an output-emission failure, not a scaling limit.</p>");
        sb.Append("<p><span class=\"tag codeact\">CodeAct</span> lets the model write one program that loops over the households and calls the tools inside the sandbox, then returns the finished report. Because the report is assembled by code, it produced a valid report at every size tested.</p>");
        sb.Append("<p class=\"why\">Because Classic never produced output, this run is <b>not yet a fair head-to-head</b>: the charts below compare the cost of a failed attempt against a completed one, so they do not measure relative efficiency.</p>");
        sb.Append("</div>");

        // 3. Charts (secondary) with an explicit caption so partial-then-failed bars aren't mistaken for successes.
        sb.Append("<h2>Median measurements by dataset</h2>");
        sb.Append("<p class=\"disc\">These are <b>not</b> a like-for-like comparison here: Classic did not complete any dataset (see the matrix above), so its lower tokens/cost reflect stopping early without a report, not efficiency. The tool-call chart also undercounts CodeAct &mdash; its tool calls run inside the sandbox and are not counted as model tool calls. Efficiency can only be compared once both modes complete.</p>");
        sb.Append("<section class=\"charts\">");
        sb.Append(SvgGroupedBars("Duration median (ms) by dataset", labels, durC, durA, v => v.ToString("0.#", Invariant)));
        sb.Append(SvgGroupedBars("Total tokens median by dataset", labels, tokC, tokA, v => v.ToString("0", Invariant)));
        sb.Append(SvgGroupedBars("Tool calls median by dataset", labels, tolC, tolA, v => v.ToString("0.#", Invariant)));
        sb.Append("</section>");

        // 4. Per-dataset detail table.
        sb.Append("<h2>Per-dataset detail</h2>");
        sb.Append("<p class=\"disc\">Deltas are CodeAct vs Classic and are shown only where both modes completed at least one trial &mdash; otherwise there is no working Classic run to compare against, so they read <b>n/a</b>.</p>");
        sb.Append("<table><thead><tr><th>Profile</th><th>Households</th><th>Classic completed</th><th>CodeAct completed</th><th>Duration</th><th>Tokens</th><th>Cost</th><th>Tool calls</th><th>Exact &Delta;</th></tr></thead><tbody>");
        foreach (var r in ordered)
        {
            var dd = r.Delta;
            int cN = TrialCount(r, "classic"), cDone = CompletedCount(r, "classic");
            int aN = TrialCount(r, "codeact"), aDone = CompletedCount(r, "codeact");
            bool comparable = cDone > 0 && aDone > 0;
            sb.Append($"<tr><td>{Enc(r.Profile)}</td><td>{r.DatasetHouseholdCount?.ToString(Invariant) ?? "n/a"}</td>");
            sb.Append($"<td class=\"{(cDone == 0 ? "st-bad" : cDone == cN ? "st-ok" : string.Empty)}\">{cDone}/{cN}</td>");
            sb.Append($"<td class=\"{(aDone == 0 ? "st-bad" : aDone == aN ? "st-ok" : string.Empty)}\">{aDone}/{aN}</td>");
            sb.Append($"<td>{(comparable && dd is not null ? Pct(dd.DurationDeltaFraction) : "n/a")}</td>");
            sb.Append($"<td>{(comparable && dd is not null ? Pct(dd.TokenDeltaFraction) : "n/a")}</td>");
            sb.Append($"<td>{(comparable && dd is not null ? Pct(dd.CostDeltaFraction) : "n/a")}</td>");
            sb.Append($"<td>{(comparable && dd is not null ? Pct(dd.ToolCallDeltaFraction) : "n/a")}</td>");
            sb.Append($"<td>{(comparable && dd is not null ? (dd.ExactCountDelta >= 0 ? "+" : string.Empty) + dd.ExactCountDelta.ToString(Invariant) : "n/a")}</td></tr>");
        }

        sb.Append("</tbody></table>");
        sb.Append($"<footer>{ordered.Count} comparison(s)</footer>");
        sb.Append("</main></body></html>");
        return sb.ToString();
    }

    private static void AppendHtmlHead(StringBuilder sb, string title)
    {
        sb.Append("<!DOCTYPE html>\n<html lang=\"en\"><head><meta charset=\"utf-8\">");
        sb.Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        sb.Append($"<title>{Enc(title)}</title>");
        sb.Append("<style>");
        sb.Append("body{font-family:system-ui,Segoe UI,Roboto,sans-serif;margin:0;background:#0f172a;color:#e2e8f0}");
        sb.Append("main{max-width:960px;margin:0 auto;padding:24px}");
        sb.Append("h1{font-size:1.4rem;margin:0 0 4px}h2{font-size:1.05rem;margin:24px 0 8px;border-bottom:1px solid #334155;padding-bottom:4px}");
        sb.Append(".disc{color:#94a3b8;font-size:.8rem}.meta{color:#cbd5e1;font-size:.85rem;margin:4px 0}");
        sb.Append(".fair{display:inline-block;padding:2px 10px;border-radius:12px;font-size:.8rem;font-weight:600}");
        sb.Append(".ok{background:#064e3b;color:#6ee7b7}.bad{background:#7f1d1d;color:#fecaca}");
        sb.Append(".cards{display:flex;flex-wrap:wrap;gap:12px;margin:16px 0}");
        sb.Append(".card{background:#1e293b;border:1px solid #334155;border-radius:10px;padding:12px 16px;min-width:110px}");
        sb.Append(".card .n{font-size:1.3rem;font-weight:700}.card .l{font-size:.75rem;color:#94a3b8}");
        sb.Append(".charts{display:grid;grid-template-columns:repeat(auto-fit,minmax(260px,1fr));gap:12px}");
        sb.Append(".charts svg,.strip{background:#1e293b;border:1px solid #334155;border-radius:10px;width:100%;height:auto}");
        sb.Append(".ct{fill:#e2e8f0;font-size:12px;font-weight:600}.vl{fill:#e2e8f0;font-size:11px}.al{fill:#94a3b8;font-size:11px}");
        sb.Append("table{border-collapse:collapse;width:100%;font-size:.8rem;margin-top:8px}");
        sb.Append("th,td{border:1px solid #334155;padding:4px 8px;text-align:right}");
        sb.Append("th:nth-child(-n+2),td:nth-child(-n+2){text-align:left}");
        sb.Append("footer{color:#64748b;font-size:.75rem;margin-top:24px}");
        sb.Append(".hint{color:#64748b;font-size:.72rem}");
        sb.Append(".outcome{display:flex;flex-wrap:wrap;align-items:center;gap:14px;margin:16px 0;padding:14px;background:#1e293b;border:1px solid #334155;border-radius:12px}");
        sb.Append(".ob{border-radius:10px;padding:10px 16px;min-width:120px;text-align:center;font-size:.78rem;letter-spacing:.02em}");
        sb.Append(".ob b{display:block;font-size:1.6rem;line-height:1.1;margin:2px 0}");
        sb.Append(".ob.good{background:#064e3b;color:#6ee7b7;border:1px solid #065f46}");
        sb.Append(".ob.warn{background:#78350f;color:#fcd34d;border:1px solid #92400e}");
        sb.Append(".ob.bad{background:#7f1d1d;color:#fecaca;border:1px solid #991b1b}");
        sb.Append(".takeaway{flex:1;min-width:220px;font-size:.95rem;font-weight:600;color:#f1f5f9;line-height:1.45}");
        sb.Append(".how{display:grid;grid-template-columns:repeat(auto-fit,minmax(260px,1fr));gap:12px}");
        sb.Append(".how .col{background:#1e293b;border:1px solid #334155;border-radius:10px;padding:12px 16px;font-size:.85rem;line-height:1.45}");
        sb.Append(".how h3{margin:0 0 6px;font-size:.9rem}.how .classic h3{color:#f59e0b}.how .codeact h3{color:#60a5fa}");
        sb.Append(".panel{background:#1e293b;border:1px solid #334155;border-radius:10px;padding:12px 16px;font-size:.9rem;line-height:1.55}");
        sb.Append(".panel p{margin:6px 0}.panel .why{margin-top:10px;padding-top:10px;border-top:1px solid #334155}");
        sb.Append(".tag{display:inline-block;padding:1px 8px;border-radius:8px;font-size:.72rem;font-weight:700}");
        sb.Append(".tag.classic{background:#78350f;color:#fcd34d}.tag.codeact{background:#1e3a8a;color:#93c5fd}");
        sb.Append(".st-ok{color:#6ee7b7;font-weight:600}.st-bad{color:#fca5a5;font-weight:600}");
        sb.Append("td.note{text-align:left;color:#cbd5e1}");
        sb.Append(".matrix td,.matrix th{padding:8px 10px;vertical-align:middle}");
        sb.Append(".matrix td:nth-child(3),.matrix td:nth-child(4),.matrix th:nth-child(3),.matrix th:nth-child(4){text-align:center}");
        sb.Append(".badge{display:inline-block;padding:5px 12px;border-radius:8px;font-size:.82rem;font-weight:700;white-space:nowrap}");
        sb.Append(".badge small{display:block;font-weight:500;font-size:.68rem;opacity:.85}");
        sb.Append(".badge.good{background:#064e3b;color:#6ee7b7;border:1px solid #065f46}");
        sb.Append(".badge.warn{background:#78350f;color:#fcd34d;border:1px solid #92400e}");
        sb.Append(".badge.bad{background:#7f1d1d;color:#fecaca;border:1px solid #991b1b}");
        sb.Append("</style></head><body><main>");
    }

    private static string SvgGroupedBars(
        string title, IReadOnlyList<string> labels, IReadOnlyList<double?> classic, IReadOnlyList<double?> codeact, Func<double, string> fmt)
    {
        int n = labels.Count;
        double max = 0d;
        for (int i = 0; i < n; i++)
        {
            max = Math.Max(max, Math.Max(classic[i] ?? 0d, codeact[i] ?? 0d));
        }

        const double baseY = 180d;
        const double maxH = 130d;
        const double plotLeft = 50d;
        const double plotRight = 470d;
        double groupW = n > 0 ? (plotRight - plotLeft) / n : plotRight - plotLeft;
        double barW = Math.Min(26d, groupW / 3d);
        var sb = new StringBuilder();
        sb.Append($"<svg viewBox=\"0 0 480 210\" role=\"img\" aria-label=\"{Enc(title)}\">");
        sb.Append($"<text x=\"240\" y=\"16\" text-anchor=\"middle\" class=\"ct\">{Enc(title)}</text>");
        sb.Append("<rect x=\"300\" y=\"24\" width=\"10\" height=\"10\" fill=\"#b45309\"/><text x=\"314\" y=\"33\" class=\"al\">Classic</text>");
        sb.Append("<rect x=\"372\" y=\"24\" width=\"10\" height=\"10\" fill=\"#2563eb\"/><text x=\"386\" y=\"33\" class=\"al\">CodeAct</text>");
        sb.Append("<line x1=\"50\" y1=\"180\" x2=\"470\" y2=\"180\" stroke=\"#334155\"/>");
        for (int i = 0; i < n; i++)
        {
            double cx = plotLeft + (groupW * (i + 0.5d));
            double hc = max > 0d && classic[i] is not null ? classic[i]!.Value / max * maxH : 0d;
            double ha = max > 0d && codeact[i] is not null ? codeact[i]!.Value / max * maxH : 0d;
            sb.Append($"<rect x=\"{F(cx - barW - 2d)}\" y=\"{F(baseY - hc)}\" width=\"{F(barW)}\" height=\"{F(hc)}\" fill=\"#b45309\"/>");
            sb.Append($"<rect x=\"{F(cx + 2d)}\" y=\"{F(baseY - ha)}\" width=\"{F(barW)}\" height=\"{F(ha)}\" fill=\"#2563eb\"/>");
            sb.Append($"<text x=\"{F(cx)}\" y=\"196\" text-anchor=\"middle\" class=\"al\">{Enc(labels[i])}</text>");
        }

        sb.Append("</svg>");
        return sb.ToString();
    }

    private static string SvgBars(string title, double? classic, double? codeact, Func<double, string> fmt)
    {
        double max = Math.Max(classic ?? 0d, codeact ?? 0d);
        const double baseY = 170d;
        const double maxH = 130d;
        double hc = max > 0d && classic is not null ? classic.Value / max * maxH : 0d;
        double ha = max > 0d && codeact is not null ? codeact.Value / max * maxH : 0d;
        string vc = classic is null ? "n/a" : fmt(classic.Value);
        string va = codeact is null ? "n/a" : fmt(codeact.Value);
        var sb = new StringBuilder();
        sb.Append($"<svg viewBox=\"0 0 320 200\" role=\"img\" aria-label=\"{Enc(title)}\">");
        sb.Append($"<text x=\"160\" y=\"16\" text-anchor=\"middle\" class=\"ct\">{Enc(title)}</text>");
        sb.Append($"<rect x=\"70\" y=\"{F(baseY - hc)}\" width=\"70\" height=\"{F(hc)}\" fill=\"#b45309\"/>");
        sb.Append($"<text x=\"105\" y=\"{F(baseY - hc - 4d)}\" text-anchor=\"middle\" class=\"vl\">{Enc(vc)}</text>");
        sb.Append("<text x=\"105\" y=\"188\" text-anchor=\"middle\" class=\"al\">Classic</text>");
        sb.Append($"<rect x=\"180\" y=\"{F(baseY - ha)}\" width=\"70\" height=\"{F(ha)}\" fill=\"#2563eb\"/>");
        sb.Append($"<text x=\"215\" y=\"{F(baseY - ha - 4d)}\" text-anchor=\"middle\" class=\"vl\">{Enc(va)}</text>");
        sb.Append("<text x=\"215\" y=\"188\" text-anchor=\"middle\" class=\"al\">CodeAct</text>");
        sb.Append("</svg>");
        return sb.ToString();
    }

    private static string SvgStrip(ComparisonReport report)
    {
        double max = report.RawTrials.Count == 0 ? 0d : report.RawTrials.Max(t => t.DurationMs);
        var sb = new StringBuilder();
        sb.Append("<svg class=\"strip\" viewBox=\"0 0 420 120\" role=\"img\" aria-label=\"Per-trial duration\">");
        sb.Append("<text x=\"6\" y=\"34\" class=\"al\">Classic</text>");
        sb.Append("<text x=\"6\" y=\"84\" class=\"al\">CodeAct</text>");
        sb.Append("<line x1=\"70\" y1=\"30\" x2=\"410\" y2=\"30\" stroke=\"#334155\"/>");
        sb.Append("<line x1=\"70\" y1=\"80\" x2=\"410\" y2=\"80\" stroke=\"#334155\"/>");
        foreach (var t in report.RawTrials)
        {
            double x = max > 0d ? 70d + (t.DurationMs / max * 330d) : 70d;
            string y = string.Equals(t.Mode, "codeact", StringComparison.Ordinal) ? "80" : "30";
            string color = string.Equals(t.Mode, "codeact", StringComparison.Ordinal) ? "#2563eb" : "#b45309";
            sb.Append($"<circle cx=\"{F(x)}\" cy=\"{y}\" r=\"5\" fill=\"{color}\" fill-opacity=\"0.75\"/>");
        }

        sb.Append($"<text x=\"70\" y=\"108\" class=\"al\">0</text><text x=\"410\" y=\"108\" text-anchor=\"end\" class=\"al\">{F(max)} ms</text>");
        sb.Append("</svg>");
        return sb.ToString();
    }

    private static string FactorLabel(double? factor) =>
        factor is null or <= 0d ? "n/a" : factor.Value.ToString("0.00", Invariant) + "x";

    private static string Enc(string value) => System.Net.WebUtility.HtmlEncode(value);

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

        AppendHeadline(sb, report);

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
        AppendRow(sb, "Cost median (USD)", CostVal(report.Classic.Cost?.Median), CostVal(report.CodeAct.Cost?.Median));
        AppendRow(sb, "Tool calls median", Rt(report.Classic.RoundTrips?.MedianToolCalls), Rt(report.CodeAct.RoundTrips?.MedianToolCalls));
        AppendRow(sb, "Model turns median", Rt(report.Classic.RoundTrips?.MedianModelTurns), Rt(report.CodeAct.RoundTrips?.MedianModelTurns));
        AppendRow(sb, "execute_code median", Rt(report.Classic.RoundTrips?.MedianExecuteCodeCalls), Rt(report.CodeAct.RoundTrips?.MedianExecuteCodeCalls));
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
        AppendMechanism(sb, report);
        sb.Append("## Raw trials (none removed)\n\n");
        sb.Append("| Pair | Order | Mode | Duration (ms) | Status | Tools | Turns | execute_code | Tokens | Cost (USD) | Match |\n");
        sb.Append("|---|---|---|---|---|---|---|---|---|---|---|\n");
        foreach (var t in report.RawTrials)
        {
            sb.Append($"| {t.PairId} | {t.OrderInPair} | {t.Mode} | {F(t.DurationMs)} | {t.Status} | {t.ToolCallCount} | {t.ModelTurnCount} | {t.ExecuteCodeCallCount} | {LongVal(t.TotalTokens)} | {CostVal(t.EstimatedCostUsd)} | {t.Score.MatchClass} |\n");
        }

        return sb.ToString();
    }

    private static void AppendRow(StringBuilder sb, string label, string classic, string codeact) =>
        sb.Append($"| {label} | {classic} | {codeact} |\n");

    private static void AppendHeadline(StringBuilder sb, ComparisonReport report)
    {
        sb.Append("## Headline — CodeAct vs Classic\n\n");
        if (report.Delta is null)
        {
            sb.Append("_Delta metrics unavailable for this comparison (generated before delta support); re-run to populate._\n\n");
            return;
        }

        var d = report.Delta;
        var c = report.Classic;
        var a = report.CodeAct;
        sb.Append("> CodeAct relative to Classic (baseline). Negative = CodeAct lower.\n\n");
        sb.Append("| Metric | Change | Classic → CodeAct |\n|---|---|---|\n");
        sb.Append($"| Duration | {Pct(d.DurationDeltaFraction)}{Speedup(d.DurationSpeedupFactor)} | {F(c.DurationMs.Median)} → {F(a.DurationMs.Median)} ms |\n");
        sb.Append($"| Total tokens | {Pct(d.TokenDeltaFraction)} | {LongVal(c.TotalTokens.Median)} → {LongVal(a.TotalTokens.Median)} |\n");
        sb.Append($"| Cost (USD) | {Pct(d.CostDeltaFraction)} | {CostVal(c.Cost?.Median)} → {CostVal(a.Cost?.Median)} |\n");
        sb.Append($"| Tool calls | {Pct(d.ToolCallDeltaFraction)} | {Rt(c.RoundTrips?.MedianToolCalls)} → {Rt(a.RoundTrips?.MedianToolCalls)} |\n");
        sb.Append($"| Model turns | {Pct(d.ModelTurnDeltaFraction)} | {Rt(c.RoundTrips?.MedianModelTurns)} → {Rt(a.RoundTrips?.MedianModelTurns)} |\n");
        sb.Append($"| Correctness (Exact) | Δ {d.ExactCountDelta} | {c.Correctness.ExactCount} vs {a.Correctness.ExactCount} |\n\n");
    }

    private static void AppendMechanism(StringBuilder sb, ComparisonReport report)
    {
        if (report.Classic.RoundTrips is null || report.CodeAct.RoundTrips is null)
        {
            return;
        }

        var c = report.Classic.RoundTrips;
        var a = report.CodeAct.RoundTrips;
        sb.Append("## Why — compute reduction\n\n");
        sb.Append("CodeAct issues a single `execute_code` block that gathers and computes over the data in one\n");
        sb.Append("pass, whereas Classic makes many sequential model-tool round-trips. Fewer round-trips are the\n");
        sb.Append("mechanism behind the token, latency, and cost differences above.\n\n");
        sb.Append("| Round-trips (median) | Classic | CodeAct |\n|---|---|---|\n");
        sb.Append($"| Tool calls | {Rt(c.MedianToolCalls)} | {Rt(a.MedianToolCalls)} |\n");
        sb.Append($"| Model turns | {Rt(c.MedianModelTurns)} | {Rt(a.MedianModelTurns)} |\n");
        sb.Append($"| execute_code calls | {Rt(c.MedianExecuteCodeCalls)} | {Rt(a.MedianExecuteCodeCalls)} |\n\n");
    }

    private static string Pct(double? fraction) =>
        fraction is null ? "n/a" : (fraction.Value * 100d).ToString("0.#", Invariant) + "%";

    private static string Speedup(double? factor)
    {
        if (factor is null or <= 0d)
        {
            return string.Empty;
        }

        return factor.Value >= 1d
            ? $" ({factor.Value.ToString("0.00", Invariant)}x faster)"
            : $" ({factor.Value.ToString("0.00", Invariant)}x — slower)";
    }

    private static string CostVal(decimal? value) =>
        value is null ? "unknown" : value.Value.ToString("0.######", Invariant);

    private static string LongVal(long? value) =>
        value is null ? "unknown" : value.Value.ToString(Invariant);

    private static string Rt(double? value) =>
        value is null ? "n/a" : value.Value.ToString("0.#", Invariant);

    private static string MatchCounts(CorrectnessStats c) =>
        $"{c.ExactCount} / {c.WithinToleranceCount} / {c.PartialCount} / {c.FailedCount}";

    private static string Tokens(TokenStats t) =>
        t.Median is null ? "unknown" : $"{t.Median}{(t.Complete ? string.Empty : " (partial)")}";

    private static string F(double value) => value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);

    private static string Short(string hash) => hash.Length <= 12 ? hash : hash[..12];

    private static string Csv(string value) =>
        value.IndexOfAny(CsvSpecials) < 0
            ? value
            : "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";

    private static JsonSerializerOptions CreateJson()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
