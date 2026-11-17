#!/usr/bin/env python3
"""Read-only overflow audit of an explicitly selected historical live v3 sweep."""
import argparse
import json
from pathlib import Path


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--sweep-root", required=True, type=Path)
    args = parser.parse_args()
    seen, rows = set(), []
    for path in sorted(args.sweep_root.glob("**/comparisons/*.json")):
        comparison = json.loads(path.read_text())
        if comparison.get("reclassifiedFrom") or comparison.get("executionKind") != "live-model-hyperlight":
            continue
        if comparison["promptRevision"] != "exposure-analysis/v3":
            raise ValueError(f"Not an original live v3 comparison: {path}")
        directory = path.parent.parent / "runs"
        measured = {trial["runId"] for trial in comparison["rawTrials"]}
        for journal in sorted(directory.glob("*/events.jsonl")):
            events = [json.loads(line) for line in journal.read_text().splitlines()]
            first = events[0]
            if first["agentMode"] != "codeact" or first["runId"] in seen or first["comparisonId"] != comparison["comparisonId"]:
                continue
            seen.add(first["runId"])
            if first["runId"] not in measured and not first["isWarmup"]:
                raise ValueError(f"Unclassified run: {journal}")
            overflows, unavailable = set(), []
            for event in events:
                if event["eventType"] != "CODE_EXECUTION_STDERR":
                    continue
                payload = event["data"]["payload"]
                if payload["content"] is None or payload["truncated"]:
                    unavailable.append(event["eventId"])
                elif "Not enough space in shared output buffer" in payload["content"]:
                    overflows.add(event["data"]["invocationId"])
            if unavailable:
                raise ValueError(f"Cannot establish overflow count with incomplete stderr: {journal}: {unavailable}")
            rows.append({"runId": first["runId"], "warmup": first["isWarmup"],
                         "events": str(journal), "outputOverflowErrors": len(overflows)})
    print(json.dumps({"measuredTrials": sum(not row["warmup"] for row in rows),
                      "warmups": sum(row["warmup"] for row in rows),
                      "overflowErrors": sum(row["outputOverflowErrors"] for row in rows), "runs": rows}, indent=2))


if __name__ == "__main__":
    main()
