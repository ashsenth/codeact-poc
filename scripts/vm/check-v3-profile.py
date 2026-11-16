#!/usr/bin/env python3
"""Check captured v3 evidence and optionally regenerate the explicitly scoped summary."""
import argparse
import hashlib
import json
import os
import statistics
import subprocess
from decimal import Decimal
from pathlib import Path


def read(path):
    return json.loads(path.read_text(), parse_float=Decimal)


def select_comparison(root):
    candidates = list((root / "comparisons").glob("*.json"))
    derived = [path for path in candidates if read(path).get("reclassifiedFrom")]
    selected = derived or candidates
    if len(selected) != 1:
        raise ValueError(f"Expected one comparison (or one reclassification) in {root}, found {len(selected)}.")
    return selected[0]


def argument_errors(events):
    errors = [event for event in events
                 if event["eventType"] in ("TOOL_CALL_FAILED", "MODEL_TOOL_CALL_FAILED")
                 and event["errorCategory"] in ("ToolArgumentException", "ToolNotFoundException")]
    return sum(event["eventType"] == "TOOL_CALL_FAILED" or not any(
        child["eventType"] == "TOOL_CALL_FAILED"
        and (child["data"] or {}).get("parentInvocationId")
        == (event["data"] or {}).get("invocationId")
        and (child["data"] or {}).get("parentInvocationId") is not None
        for child in events) for event in errors)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("profile", choices=("n1", "n3", "full-small"))
    parser.add_argument("--reference-comparison", type=Path, required=True)
    parser.add_argument("--summarize", action="store_true")
    args = parser.parse_args()
    if not os.environ.get("ARTIFACTS_ROOT"):
        parser.error("ARTIFACTS_ROOT must name the v3 sweep directory.")
    root = Path(os.environ["ARTIFACTS_ROOT"]).resolve()
    path = select_comparison(root / args.profile)
    report, reference = read(path), read(args.reference_comparison.resolve())
    assert report["outcome"]["isValid"], report["outcome"]
    assert report["promptRevision"] == "exposure-analysis/v3"
    assert report["promptHash"] == reference["promptHash"]
    assert report["executionKind"] == "live-model-hyperlight"
    assert report["scopedHouseholdCount"] == {"n1": 1, "n3": 3, "full-small": 10}[args.profile]
    assert report["measuredTrials"] == 5 and report["warmupsPerMode"] == 1
    assert len(report["rawTrials"]) == 10 and len(report["warmupTrials"]) == 2
    original = reference["rawTrials"][0]
    ref_metadata = read(args.reference_comparison.resolve().parent.parent / "runs" / original["runId"] / "metadata.json")
    pricing = ref_metadata["pricingConfiguration"]
    number = lambda value: format(Decimal(value).normalize(), "f")
    price_hash = hashlib.sha256(
        f"in={number(pricing['inputPerMillion'])};out={number(pricing['outputPerMillion'])}".encode()).hexdigest()
    for side in ("classicFingerprints", "codeActFingerprints"):
        for key in ("prompt", "schema", "tools", "dataset", "model"):
            assert report["fairness"][side][key] == reference["fairness"][side][key], (side, key)
        assert report["fairness"][side]["pricing"] == price_hash
    for trial in report["rawTrials"] + report["warmupTrials"]:
        directory = path.parent.parent / "runs" / trial["runId"]
        metadata = read(directory / "metadata.json")
        assert metadata["modelConfiguration"] == ref_metadata["modelConfiguration"]
        assert metadata["pricingConfiguration"] == pricing
        events = [json.loads(line) for line in (directory / "events.jsonl").read_text().splitlines()]
        assert trial["toolArgumentErrors"] == argument_errors(events), trial["runId"]
        assert trial["telemetryValid"] and not trial["infrastructureFailed"], trial
        assert (directory / "timeline.md").is_file()
    print("VALID; raw evidence, counts, scope and unchanged model/schema/tools/dataset/numeric pricing verified.")
    for mode in ("classic", "codeact"):
        trials = [trial for trial in report["rawTrials"] if trial["mode"] == mode]
        assert len(trials) == 5 and {trial["pairId"] for trial in trials} == set(range(1, 6))
        correct = sum(trial["score"]["matchClass"] in ("Exact", "WithinTolerance") for trial in trials)
        unearned = sum(trial["score"]["matchClass"] == "UnearnedCompleteness" for trial in trials)
        def median(key):
            values = [trial[key] for trial in trials]
            return "unavailable" if any(value is None for value in values) else statistics.median(values)
        print(f"{mode}: correct {correct}/5; unearned {unearned}/5; toolArgumentErrors "
              f"{sum(trial['toolArgumentErrors'] for trial in trials)}; medians: tokens={median('totalTokens')}, "
              f"est. USD={median('estimatedCostUsd')}, analysis ms={median('analysisDurationMs')}")
        for trial in trials:
            print(f"  pair {trial['pairId']}: {trial['status']}/{trial['score']['matchClass']}; "
                  f"toolArgumentErrors={trial['toolArgumentErrors']}")
    if args.summarize:
        paths = [select_comparison(root / profile) for profile in ("n1", "n3", "full-small")
                 if (root / profile / "comparisons").is_dir()]
        repo = Path(__file__).resolve().parents[2]
        subprocess.run(["dotnet", str(repo / "src/SterlingVale.Benchmark/bin/Release/net9.0/SterlingVale.Benchmark.dll"),
                        "summarize", "--comparisons", ",".join(map(str, paths))], cwd=repo, check=True)


if __name__ == "__main__":
    main()
