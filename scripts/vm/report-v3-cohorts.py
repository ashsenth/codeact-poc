#!/usr/bin/env python3
"""Report within-model deltas first, cross-model second; exclude smoke and archived attempts."""
import argparse
import json
import statistics
from decimal import Decimal
from pathlib import Path
from cohort_config import COHORTS, validate_function_policy


def load_profile(root, scope, cohort):
    directory = root / scope
    if not directory.exists():
        return None
    paths = list((directory / "comparisons").glob("*.json"))
    if len(paths) != 1:
        raise ValueError(f"Expected exactly one original comparison: {directory}")
    launch = json.loads((directory / "launch.json").read_text())
    if launch["cohort"] != cohort or launch["smoke"] or launch["trials"] != 5:
        raise ValueError(f"Not a five-pair measured cohort: {directory}")
    if launch["timeoutSeconds"] != COHORTS[cohort]["timeoutSeconds"]:
        raise ValueError(f"Wrong cohort timeout: {directory}")
    report = json.loads(paths[0].read_text(), parse_float=Decimal)
    if report["promptRevision"] != "exposure-analysis/v3" or report["executionKind"] != "live-model-hyperlight":
        raise ValueError(f"Not a live v3 sandbox comparison: {directory}")
    spec = COHORTS[cohort]
    for trial in report["rawTrials"] + report.get("warmupTrials", []):
        metadata = json.loads((directory / "runs" / trial["runId"] / "metadata.json").read_text())
        model = metadata["modelConfiguration"]
        if model["timeoutSeconds"] != spec["timeoutSeconds"]:
            raise ValueError(f"Wrong model timeout: {directory}, {trial['runId']}")
        if (model["modelName"], model["modelVersion"], model["apiMode"]) != (spec["model"], spec["version"], "responses"):
            raise ValueError(f"Wrong model/version/API: {directory}, {trial['runId']}")
        validate_function_policy(model, report["modelFingerprint"], spec)
    return report


def median(trials, key):
    values = [trial.get(key) for trial in trials]
    return None if not values or any(value is None for value in values) else statistics.median(values)


def fmt(value, decimals=0):
    return "unavailable" if value is None else f"{value:,.{decimals}f}"


def total(trials, key):
    values = [trial.get(key) for trial in trials]
    return "unavailable" if not values or any(value is None for value in values) else str(sum(values))


def delta(baseline, value):
    if baseline in (None, 0) or value is None:
        return "unavailable"
    return f"{(Decimal(value) / Decimal(baseline) - 1) * 100:+.1f}%"


def within_model_cells(cohort, scope, report):
    if report is None:
        return [cohort, scope, "not run"] + ["--"] * 11
    groups = [[trial for trial in report["rawTrials"] if trial["mode"] == mode]
              for mode in ("classic", "codeact")]
    valid = report["outcome"]["isValid"] and all(groups)
    counts = [sum(t["score"]["matchClass"] in ("Exact", "WithinTolerance") for t in trials)
              for trials in groups]
    def pair(values):
        return " -> ".join(values)
    def medians(key):
        return [median(trials, key) if valid else None for trials in groups]
    def resources(key, decimals, divisor=1):
        return pair(fmt(None if value is None else value / divisor, decimals) for value in medians(key))
    correctness = pair(f"{count}/{len(trials)}" for count, trials in zip(counts, groups))
    context = "unavailable: INVALID or no measured trials"
    if valid:
        if counts[0] == counts[1] and len(groups[0]) == len(groups[1]):
            context = f"correctness-equivalent by count ({counts[0]}/{len(groups[0])} each)"
            if counts[0] == 0:
                context += "; neither mode correct, not an efficiency win"
        else:
            context = "not correctness-equivalent: correct counts differ"
    return [cohort, scope, correctness if valid else correctness + " INVALID",
            pair(str(sum(t["score"]["matchClass"] == "UnearnedCompleteness" for t in trials)) for trials in groups),
            *(pair(total(trials, key) for trials in groups)
              for key in ("turnLimitReached", "toolArgumentErrors", "sandboxOutputOverflowErrors", "http429Count")),
            resources("totalTokens", 0), resources("estimatedCostUsd", 7),
            resources("analysisDurationMs", 3, 1000), resources("retryWaitMs", 3, 1000),
            " / ".join(delta(*medians(key)) for key in ("totalTokens", "estimatedCostUsd", "analysisDurationMs")),
            context]


def sensitivity_cells(scope, mode, budget, report, baseline):
    if report is None:
        return [scope, mode, str(budget), "not run"] + ["--"] * 7
    trials = [trial for trial in report["rawTrials"] if trial["mode"] == mode]
    valid = report["outcome"]["isValid"] and bool(trials)
    correct = sum(trial["score"]["matchClass"] in ("Exact", "WithinTolerance") for trial in trials)
    base = [] if baseline is None else [trial for trial in baseline["rawTrials"] if trial["mode"] == mode]
    comparable = valid and bool(base) and baseline["outcome"]["isValid"]
    def measured(key):
        return median(trials, key) if valid else None
    context, deltas = "--", "--"
    if budget == 600:
        context = "unavailable: missing or INVALID evidence"
        deltas = "unavailable / unavailable / unavailable"
        if comparable:
            base_correct = sum(trial["score"]["matchClass"] in ("Exact", "WithinTolerance") for trial in base)
            if correct == base_correct and len(trials) == len(base):
                context = f"correctness-equivalent by count ({correct}/{len(trials)} each)"
                if correct == 0:
                    context += "; neither budget correct, not an efficiency win"
            else:
                context = "not correctness-equivalent: correct counts differ"
            deltas = " / ".join(delta(median(base, key), measured(key))
                               for key in ("totalTokens", "estimatedCostUsd", "analysisDurationMs"))
    analysis = measured("analysisDurationMs")
    return [scope, mode, str(budget), f"{correct}/{len(trials)}" + ("" if valid else " INVALID"),
            str(sum(trial["status"] == "TimedOut" for trial in trials)), total(trials, "turnLimitReached"),
            fmt(None if analysis is None else analysis / 1000, 3), fmt(measured("totalTokens")),
            fmt(measured("estimatedCostUsd"), 7), deltas, context]


def sensitivity_lines(baseline_root, t600_root):
    baseline_root, t600_root = baseline_root.resolve(), t600_root.resolve()
    if baseline_root.is_relative_to(t600_root) or t600_root.is_relative_to(baseline_root):
        raise ValueError("120-second and 600-second roots must be separate, non-nested directories.")
    cohorts = ("gpt-6.1-sol-v3", "gpt-6.1-sol-v3-t600")
    roots = (baseline_root, t600_root)
    reports = {(cohort, scope): load_profile(root, scope, cohort)
               for cohort, root in zip(cohorts, roots) for scope in ("n3", "full-small")}
    available = [report for report in reports.values() if report is not None]
    for key in ("prompt", "schema", "tools", "dataset", "pricing"):
        fingerprints = {report["fairness"][side][key] for report in available
                        for side in ("classicFingerprints", "codeActFingerprints")}
        if len(fingerprints) > 1:
            raise ValueError(f"Sensitivity {key} mismatch")
    lines = [
        "# Time-budget sensitivity (120 s vs 600 s)", "",
        "Separate Sol cohorts: `gpt-6.1-sol-v3` (120 s) and `gpt-6.1-sol-v3-t600` (600 s).",
        "Only the timeout changes for both modes; the external worker watchdog uses that same timeout. "
        "Timeout is part of each model fingerprint. No pooled samples or combined medians across budgets.",
        "Same deployment/version, pricing, prompt, schema, tools, dataset, 20-request turn budget, "
        "16,384-token output cap, default medium reasoning and sequential local invocation.",
        "Five measured pairs per completed scope; one warm-up per mode excluded. "
        "All measured outcomes remain in medians. Timeouts are deadline outcomes, not TurnLimitReached.",
        "Analysis excludes retry wait; the watchdog includes waiting. Tokens include reasoning once; costs are retail estimates.",
        "Missing rows are not run, never zero-success results. Archived attempts, smoke and N=1 are excluded.", "",
        "## Per scope and mode", "",
        "Deltas are (600 s / 120 s - 1) within Sol and the same scope/mode; equal correct counts alone "
        "permit a correctness-equivalent label. Equal zero counts are not an efficiency win.", "",
        "| Scope | Mode | Budget s | Correct | Timeouts | TL | Median analysis s | Median tokens | Median USD | Delta tokens / cost / time vs 120 s | Correctness context |",
        "|---|---|---:|---|---:|---:|---:|---:|---:|---|---|",
    ]
    for scope in ("n3", "full-small"):
        baseline = reports[(cohorts[0], scope)]
        for mode in ("classic", "codeact"):
            for cohort, budget in zip(cohorts, (120, 600)):
                lines.append("| " + " | ".join(sensitivity_cells(scope, mode, budget,
                                                                 reports[(cohort, scope)], baseline)) + " |")
    lines += [
        "", "## Within each budget: CodeAct versus Classic", "",
        "Each arrow is Classic -> CodeAct; deltas are (CodeAct / Classic - 1). "
        "Correctness-equivalent means matching correct counts, not necessarily the same correct pairs.", "",
        "| Cohort | Scope | Correct | Unearned | TurnLimitReached | Argument errors | Overflow errors | HTTP 429 | Median tokens | Median USD | Analysis s | Retry wait s | Delta tokens / cost / analysis | Correctness context |",
        "|---|---|---|---|---|---|---|---|---|---|---|---|---|---|",
    ]
    for scope in ("n3", "full-small"):
        for cohort in cohorts:
            lines.append("| " + " | ".join(within_model_cells(cohort, scope, reports[(cohort, scope)])) + " |")
    lines += [
        "", "## Reproduction", "",
        "Use the [manual wrapper](../scripts/vm/run-sol-t600.sh) after a clean Release build, tests and push. "
        "It checks, reports, commits and pushes N=3 before launching full Small. "
        "Model failures continue; infrastructure or checker/publication failure prevents the next profile.",
        "Set the documented Azure/guest/dataset environment; see [launch instructions](running-experiments.md#time-budget-sensitivity-600-seconds). "
        "The wrapper is prepared for manual launch, not automatically started.", "",
        "Regenerate this report without any inference:", "",
        "```bash",
        "python3 scripts/vm/report-v3-cohorts.py --sol-root \"$SOL_BASELINE_ROOT\" \\",
        "  --sol-t600-root \"$SOL_T600_ROOT\" --output docs/v3-time-budget-sensitivity.md",
        "```",
    ]
    return lines


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--gpt4o-root", type=Path)
    parser.add_argument("--sol-root", required=True, type=Path)
    parser.add_argument("--sol-t600-root", type=Path, help="Render only the separate time-budget sensitivity report.")
    parser.add_argument("--output", required=True, type=Path)
    args = parser.parse_args()
    if args.sol_t600_root:
        lines = sensitivity_lines(args.sol_root, args.sol_t600_root)
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text("\n".join(lines) + "\n")
        return
    if not args.gpt4o_root:
        parser.error("--gpt4o-root is required for the 120-second cross-model report.")
    cohorts = ("gpt-4o-v3-enforced", "gpt-6.1-sol-v3")
    roots = dict(zip(cohorts, (args.gpt4o_root, args.sol_root)))
    reports = {(cohort, scope): load_profile(root, scope, cohort)
               for cohort, root in roots.items() for scope in ("n1", "n3", "full-small")}
    available = [report for report in reports.values() if report is not None]
    for key in ("prompt", "schema", "tools", "dataset"):
        if len({report["fairness"]["classicFingerprints"][key] for report in available}) > 1:
            raise ValueError(f"Cross-model {key} mismatch")
    lines = [
        "# v3 enforced stateless Responses: results", "",
        "See [configuration, pricing, audit and gates](v3-enforced-cohorts.md) and [canonical validity rules](benchmark-methodology.md#validity-rules). Historical cohorts and isolated smoke pairs are excluded.",
        "Five measured pairs per completed scope. Correct = Exact/WithinTolerance. All measured outcomes remain in medians; warm-ups excluded.",
        "Output/total tokens include reasoning once; R = median reasoning, V = median visible output, C = median cached input.",
        "Analysis excludes recorded retry wait; the unchanged 120-second deadline includes waiting. Cost uses per-request Azure retail cache/context rates, not an invoice.",
        "Local function invocation is sequential within each response in both modes and APIs, as verified by the parallel-call audit.",
        "An explicit policy fingerprint marker was added after the initial profiles without changing that behavior. Original fingerprints/evidence are retained; profile rows remain separate.",
        "Correctness-equivalent means equal correct counts, not necessarily the same correct pairs or an efficiency win. In particular, equal zero counts do not establish useful performance.", "",
        "## Primary: within-model CodeAct versus Classic", "",
        "Every arrow is Classic -> CodeAct. Deltas are (CodeAct / Classic - 1), including every measured outcome.", "",
        "| Cohort | Scope | Correct | Unearned | TurnLimitReached | Argument errors | Overflow errors | HTTP 429 | Median tokens | Median USD | Analysis s | Retry wait s | Delta tokens / cost / analysis | Correctness context |",
        "|---|---|---|---|---|---|---|---|---|---|---|---|---|---|",
    ]
    for scope in ("n1", "n3", "full-small"):
        for cohort in cohorts:
            lines.append("| " + " | ".join(within_model_cells(cohort, scope, reports[(cohort, scope)])) + " |")
    lines += [
        "", "## Secondary: cross-model comparison", "",
        "Deltas compare Sol to gpt-4o for the same scope/mode, including all outcomes. These are descriptive, not efficiency wins when answers are not equally correct.", "",
        "| Cohort | Scope | Mode | Correct | Unearned | TurnLimitReached | Argument errors | Overflow errors | HTTP 429 | Median tokens (R / V / C) | Median USD | Analysis s | Retry wait s | Delta tokens / cost / analysis vs 4o |",
        "|---|---|---|---:|---:|---:|---:|---:|---:|---|---:|---:|---:|---|",
    ]
    for scope in ("n1", "n3", "full-small"):
        for mode in ("classic", "codeact"):
            baseline = reports[(cohorts[0], scope)]
            base_trials = [] if baseline is None else [trial for trial in baseline["rawTrials"] if trial["mode"] == mode]
            for cohort in cohorts:
                report = reports[(cohort, scope)]
                if report is None:
                    cells = [cohort, scope, mode, "not run"] + ["--"] * 10
                else:
                    trials = [trial for trial in report["rawTrials"] if trial["mode"] == mode]
                    valid = report["outcome"]["isValid"]
                    def measured(key):
                        return median(trials, key) if valid else None
                    tokens = measured("totalTokens")
                    token_details = " / ".join(fmt(measured(key)) for key in ("reasoningTokens", "visibleOutputTokens", "cachedInputTokens"))
                    descriptive = "--" if cohort == cohorts[0] else " / ".join(
                        delta(median(base_trials, key), measured(key)) if baseline and baseline["outcome"]["isValid"] else "unavailable"
                        for key in ("totalTokens", "estimatedCostUsd", "analysisDurationMs"))
                    analysis, wait = measured("analysisDurationMs"), measured("retryWaitMs")
                    cells = [cohort, scope, mode,
                             f"{sum(t['score']['matchClass'] in ('Exact', 'WithinTolerance') for t in trials)}/{len(trials)}" + ("" if valid else " INVALID"),
                             str(sum(t["score"]["matchClass"] == "UnearnedCompleteness" for t in trials)),
                             total(trials, "turnLimitReached"), total(trials, "toolArgumentErrors"),
                             total(trials, "sandboxOutputOverflowErrors"), total(trials, "http429Count"),
                             f"{fmt(tokens)} ({token_details})", fmt(measured("estimatedCostUsd"), 7),
                             fmt(None if analysis is None else analysis / 1000, 3),
                             fmt(None if wait is None else wait / 1000, 3), descriptive]
                lines.append("| " + " | ".join(cells) + " |")
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text("\n".join(lines) + "\n")


if __name__ == "__main__":
    main()
