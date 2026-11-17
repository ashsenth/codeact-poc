#!/usr/bin/env python3
"""Read-only evidence ledger for non-correct measured v3 CodeAct trials; no execution."""
import argparse
import json
from pathlib import Path


FIELDS = ("baseCurrency", "totalValue", "flagged", "drifts", "breaches", "proposedTrades")


def households(value):
    if isinstance(value, dict):
        if "householdId" in value:
            yield value
        for child in value.values():
            yield from households(child)
    elif isinstance(value, list):
        for child in value:
            yield from households(child)


def truncated(value):
    if isinstance(value, dict):
        return value.get("truncated") is True or any(truncated(child) for child in value.values())
    return isinstance(value, list) and any(truncated(child) for child in value)


def inspect(root, scope, trial):
    directory = root / scope / "runs" / trial["runId"]
    events = [json.loads(line) for line in (directory / "events.jsonl").read_text().splitlines()]
    samples, streams, errors = [], [], []
    for event in events:
        if event["eventType"] in ("CODE_EXECUTION_STDOUT", "CODE_EXECUTION_STDERR"):
            payload = event["data"]["payload"]
            text = payload["content"]
            if text is None:
                raise ValueError(f"Full stream capture unavailable: {directory}")
            streams.append({"stream": event["eventType"], "characters": payload["originalLength"],
                            "bytes": len(text.encode()), "truncated": payload["truncated"]})
            if event["eventType"] == "CODE_EXECUTION_STDERR" and text:
                errors.append(text.strip())
            if event["eventType"] == "CODE_EXECUTION_STDOUT":
                try:
                    value = json.loads(text)
                except json.JSONDecodeError:
                    continue
                samples.extend(households(value))
    final = None
    if trial["status"] != "TimedOut":
        final = json.loads((directory / "raw-output.txt").read_text())
    matches = {
        h["householdId"]: any(all(field in candidate and candidate[field] == h[field] for field in FIELDS)
                              for candidate in samples if candidate["householdId"] == h["householdId"])
        for h in (final or {}).get("households", [])
    }
    return {
        "scope": scope, "pair": trial["pairId"], "runId": trial["runId"], "status": trial["status"],
        "matchClass": trial["score"]["matchClass"],
        "executionStarts": sum(e["eventType"] == "CODE_EXECUTION_STARTED" for e in events),
        "executionCompleted": sum(e["eventType"] == "CODE_EXECUTION_COMPLETED" for e in events),
        "executionFailed": sum(e["eventType"] == "CODE_EXECUTION_FAILED" for e in events),
        "errors": errors,
        "timeout": trial["status"] == "TimedOut",
        "truncatedCapture": any(truncated(e["data"]) for e in events),
        "stdoutOrResultCaptureTruncated": any(truncated(e["data"]) for e in events
            if e["eventType"] in ("CODE_EXECUTION_STDOUT", "CODE_EXECUTION_STDERR",
                                 "CODE_EXECUTION_COMPLETED", "CODE_EXECUTION_FAILED")),
        "streams": streams, "finalHouseholdsMatchExecutedOutput": matches,
        "missingComponents": trial["score"].get("missingComponents", {}),
    }


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--sweep-root", required=True, type=Path)
    args = parser.parse_args()
    for scope in ("n1", "n3", "full-small"):
        paths = list((args.sweep_root / scope / "comparisons").glob("*-reclassified.json"))
        paths = paths or list((args.sweep_root / scope / "comparisons").glob("*.json"))
        if len(paths) != 1:
            parser.error(f"Expected one comparison (or reclassification) for {scope}.")
        report = json.loads(paths[0].read_text())
        if report["promptRevision"] != "exposure-analysis/v3":
            parser.error(f"Not v3: {paths[0]}")
        for trial in report["rawTrials"]:
            if trial["mode"] == "codeact" and trial["score"]["matchClass"] not in ("Exact", "WithinTolerance"):
                print(json.dumps(inspect(args.sweep_root, scope, trial), separators=(",", ":")))


if __name__ == "__main__":
    main()
