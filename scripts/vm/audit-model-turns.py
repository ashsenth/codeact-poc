#!/usr/bin/env python3
"""Read-only live-trial request audit, using raw events rather than reported turn counts."""
import argparse
import json
from pathlib import Path


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--artifacts-root", required=True, type=Path)
    parser.add_argument("--threshold", type=int, default=20)
    args = parser.parse_args()
    seen, measured, warmups, sources = set(), [], [], []
    for path in sorted(args.artifacts_root.glob("**/comparisons/*.json")):
        report = json.loads(path.read_text())
        if report.get("executionKind") != "live-model-hyperlight" or report.get("reclassifiedFrom"):
            continue
        revision = report["promptRevision"]
        sources.append(str(path))
        runs = path.parent.parent / "runs"
        selected = [(runs / trial["runId"], False) for trial in report["rawTrials"]]
        for directory in sorted(runs.iterdir()):
            if not directory.is_dir():
                continue
            journal = directory / "events.jsonl"
            if journal.exists():
                with journal.open() as stream:
                    first = json.loads(next(stream))
                if first.get("isWarmup") and first["comparisonId"] == report["comparisonId"]:
                    selected.append((directory, True))
        for directory, warmup in selected:
            events = [json.loads(line) for line in (directory / "events.jsonl").read_text().splitlines()]
            first = events[0]
            key = first["runId"]
            if key in seen:
                continue
            seen.add(key)
            requests = sum(event["eventType"] == "MODEL_REQUEST_STARTED" for event in events)
            (warmups if warmup else measured).append({
                "revision": revision, "mode": first["agentMode"], "runId": key,
                "comparisonId": report["comparisonId"], "trialId": first["trialId"],
                "requests": requests, "exceeded": requests > args.threshold,
                "events": str(directory / "events.jsonl"),
            })
    summary = []
    for revision in ("exposure-analysis/v1", "exposure-analysis/v2", "exposure-analysis/v3"):
        for mode in ("classic", "codeact"):
            for label, rows in (("measured", measured), ("warmup", warmups)):
                cohort = [row for row in rows if row["revision"] == revision and row["mode"] == mode]
                summary.append({
                    "revision": revision, "mode": mode, "kind": label, "availableTrials": len(cohort),
                    "exceeded": sum(row["exceeded"] for row in cohort) if cohort else None,
                    "maximumRequests": max((row["requests"] for row in cohort), default=None),
                })
    print(json.dumps({"threshold": args.threshold, "sources": sources, "summary": summary,
                      "measuredTrials": measured, "warmups": warmups}, indent=2))


if __name__ == "__main__":
    main()
