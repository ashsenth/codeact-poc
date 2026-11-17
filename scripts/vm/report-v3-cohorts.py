#!/usr/bin/env python3
"""Generate one explicitly separated cross-model table; never read smoke or historical cohorts."""
import argparse
import json
import statistics
from decimal import Decimal
from pathlib import Path
from cohort_config import COHORTS


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
    report = json.loads(paths[0].read_text(), parse_float=Decimal)
    if report["promptRevision"] != "exposure-analysis/v3" or report["executionKind"] != "live-model-hyperlight":
        raise ValueError(f"Not a live v3 sandbox comparison: {directory}")
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


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--gpt4o-root", required=True, type=Path)
    parser.add_argument("--sol-root", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    args = parser.parse_args()
    cohorts = list(COHORTS)
    roots = dict(zip(cohorts, (args.gpt4o_root, args.sol_root)))
    reports = {(cohort, scope): load_profile(root, scope, cohort)
               for cohort, root in roots.items() for scope in ("n1", "n3", "full-small")}
    available = [report for report in reports.values() if report is not None]
    for key in ("prompt", "schema", "tools", "dataset"):
        if len({report["fairness"]["classicFingerprints"][key] for report in available}) > 1:
            raise ValueError(f"Cross-model {key} mismatch")
    lines = [
        "# v3 enforced stateless Responses: cross-model results", "",
        "See [configuration, pricing, audit and gates](v3-enforced-cohorts.md). Historical cohorts and isolated smoke pairs are excluded.",
        "Five measured pairs per completed scope. Correct = Exact/WithinTolerance. All measured outcomes remain in medians; warm-ups excluded.",
        "Output/total tokens include reasoning once; R = median reasoning, V = median visible output, C = median cached input.",
        "Analysis excludes recorded retry wait; the unchanged 120-second deadline includes waiting. Cost uses per-request Azure retail cache/context rates, not an invoice.",
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
