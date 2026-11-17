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
from cohort_config import COHORTS, load_reference, validate_function_policy


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
    parser.add_argument("--reference-comparison", type=Path, help="Optional historical comparison and sibling run metadata.")
    parser.add_argument("--summarize", action="store_true")
    parser.add_argument("--cohort", choices=tuple(COHORTS))
    parser.add_argument("--smoke", action="store_true")
    args = parser.parse_args()
    if not args.reference_comparison and not args.cohort:
        args.cohort = "gpt-4o-v3-enforced"
    if not os.environ.get("ARTIFACTS_ROOT"):
        parser.error("ARTIFACTS_ROOT must name the v3 sweep directory.")
    root = Path(os.environ["ARTIFACTS_ROOT"]).resolve()
    if args.smoke and (not args.cohort or args.profile != "n1" or args.summarize):
        parser.error("--smoke requires a Responses N=1 cohort and cannot enter summaries.")
    count = 1 if args.smoke else 5
    path = select_comparison(root / ("smoke-n1" if args.smoke else args.profile))
    report, reference = read(path), load_reference(args.reference_comparison)
    assert report["outcome"]["isValid"], report["outcome"]
    assert report["promptRevision"] == "exposure-analysis/v3"
    assert report["promptHash"] == reference["promptHash"]
    assert report["executionKind"] == "live-model-hyperlight"
    assert report["scopedHouseholdCount"] == {"n1": 1, "n3": 3, "full-small": 10}[args.profile]
    assert report["measuredTrials"] == count and report["warmupsPerMode"] == 1
    assert len(report["rawTrials"]) == 2 * count and len(report["warmupTrials"]) == 2
    ref_metadata = reference
    pricing = ref_metadata["pricingConfiguration"]
    if args.cohort:
        first = report["rawTrials"][0]
        ref_metadata = read(path.parent.parent / "runs" / first["runId"] / "metadata.json")
        model, pricing = ref_metadata["modelConfiguration"], ref_metadata["pricingConfiguration"]
        spec = COHORTS[args.cohort]
        assert model["apiMode"] == "responses" and model["store"] is False
        assert (model["deploymentName"], model["modelName"], model["modelVersion"], model["defaultReasoningEffort"]) == (
            spec["deployment"], spec["model"], spec["version"], spec["reasoning"])
        assert model["contextPolicy"] == "full-context/no-previous-response-id/v1"
        assert model["turnLimitEnforcement"] == "model-request-boundary/v1"
        validate_function_policy(model, report["modelFingerprint"], spec)
        assert (model["maxTurns"], model["timeoutSeconds"], model["maxOutputTokens"]) == (20, 120, 16384)
        assert model["strict"] is False and model["parallel_tool_calls"] is True
        assert model["temperature"] == (None if spec["model"] == "gpt-6.1-sol" else 0)
        for key, value in spec["rates"].items():
            assert Decimal(str(pricing[key])) == Decimal(value), key
        verification = read(path.parent.parent / "deployment-pricing-verification.json")
        assert verification["cohort"] == args.cohort
    number = lambda value: format(Decimal(value).normalize(), "f")
    price_hash = hashlib.sha256(
        f"in={number(pricing['inputPerMillion'])};out={number(pricing['outputPerMillion'])}".encode()).hexdigest()
    for side in ("classicFingerprints", "codeActFingerprints"):
        for key in (("prompt", "schema", "tools", "dataset") if args.cohort else ("prompt", "schema", "tools", "dataset", "model")):
            assert report["fairness"][side][key] == reference["fingerprints"][key], (side, key)
        if not args.cohort:
            assert report["fairness"][side]["pricing"] == price_hash
    for trial in report["rawTrials"] + report["warmupTrials"]:
        directory = path.parent.parent / "runs" / trial["runId"]
        metadata = read(directory / "metadata.json")
        assert metadata["modelConfiguration"] == ref_metadata["modelConfiguration"]
        assert metadata["pricingConfiguration"] == pricing
        events = [json.loads(line) for line in (directory / "events.jsonl").read_text().splitlines()]
        assert trial["toolArgumentErrors"] == argument_errors(events), trial["runId"]
        assert trial["telemetryValid"] and not trial["infrastructureFailed"], trial
        if args.cohort:
            assert trial["modelTurnCount"] <= 20, trial["runId"]
            assert trial["http429Count"] == sum(e["eventType"] == "MODEL_HTTP_429" for e in events)
            assert trial["turnLimitReached"] == sum(e["eventType"] == "TURN_LIMIT_REACHED" for e in events)
            assert trial["sandboxOutputOverflowErrors"] == sum(e["eventType"] == "SANDBOX_OUTPUT_OVERFLOW" for e in events)
            waits = [e["durationMs"] for e in events if e["eventType"] == "MODEL_RETRY_WAIT_COMPLETED"]
            assert abs(float(trial["retryWaitMs"]) - sum(waits)) < .001
            wire = [e["data"] for e in events if e["eventType"] == "RESPONSES_REQUEST_CONFIGURATION"]
            assert len(wire) == sum(e["eventType"] == "MODEL_REQUEST_STARTED" for e in events)
            assert all(e["store"] is False and e["previousResponseId"] is None for e in wire)
        assert (directory / "timeline.md").is_file()
    print("VALID; raw evidence, counts, scope, model/configuration, schema/tools/dataset and pricing verified.")
    for mode in ("classic", "codeact"):
        trials = [trial for trial in report["rawTrials"] if trial["mode"] == mode]
        assert len(trials) == count and {trial["pairId"] for trial in trials} == set(range(1, count + 1))
        correct = sum(trial["score"]["matchClass"] in ("Exact", "WithinTolerance") for trial in trials)
        unearned = sum(trial["score"]["matchClass"] == "UnearnedCompleteness" for trial in trials)
        def median(key):
            values = [trial[key] for trial in trials]
            return "unavailable" if any(value is None for value in values) else statistics.median(values)
        print(f"{mode}: correct {correct}/{count}; unearned {unearned}/{count}; toolArgumentErrors "
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
