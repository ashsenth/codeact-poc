#!/usr/bin/env python3
"""Read-only measured Classic batch audit; explicit roots exclude smoke and aborted attempts."""
import argparse
import collections
import json
import statistics
from pathlib import Path


def audit(root):
    overall, scopes = collections.Counter(), []
    for scope in ("n1", "n3", "full-small"):
        paths = [path for path in (root / scope / "comparisons").glob("*.json")
                 if not path.stem.endswith("-reclassified")]
        if not paths:
            continue
        if len(paths) != 1:
            raise ValueError(f"Expected one original comparison: {root / scope}")
        report = json.loads(paths[0].read_text())
        histogram, counts = collections.Counter(), collections.Counter()
        for trial in report["rawTrials"]:
            if trial["mode"] != "classic":
                continue
            counts["trials"] += 1
            journal = root / scope / "runs" / trial["runId"] / "events.jsonl"
            events = [json.loads(line) for line in journal.read_text().splitlines()]
            requests = [event for event in events if event["eventType"] == "MODEL_REQUEST_STARTED"]
            seen = set()
            for event in events:
                if event["eventType"] == "RESPONSES_REQUEST_CONFIGURATION":
                    if event["data"]["parallelToolCalls"] is not True:
                        raise ValueError(f"Parallel calls disabled: {journal}")
                    counts["wireParallelTrue"] += 1
                if event["eventType"] != "MODEL_RESPONSE_RECEIVED":
                    continue
                captured = event["data"]["messages"]
                if captured["truncated"] or captured["redacted"]:
                    raise ValueError(f"Response identities unavailable: {journal}")
                calls = [call for message in json.loads(captured["content"]) for call in message["calls"]]
                histogram[len(calls)] += 1
                ids = collections.Counter(call["callId"] for call in calls)
                for call in calls:
                    signature = (call["name"], json.dumps(call["arguments"], sort_keys=True))
                    if signature in seen:
                        counts["duplicateCalls"] += 1
                        counts["singleCallDuplicates"] += len(calls) == 1
                    seen.add(signature)
                next_request = next((request for request in requests
                                     if request["sequenceNumber"] > event["sequenceNumber"]), None)
                boundary = next_request["sequenceNumber"] if next_request else float("inf")
                between = [item for item in events if event["sequenceNumber"] < item["sequenceNumber"] < boundary]
                for kind in ("MODEL_TOOL_CALL_STARTED", "MODEL_TOOL_CALL_COMPLETED"):
                    actual = collections.Counter(item["data"]["invocationId"] for item in between
                                                 if item["eventType"] == kind)
                    if actual != ids:
                        raise ValueError(f"Missing, split or duplicate {kind}: {journal}, turn {event['data']['modelTurn']}")
                if next_request is None:
                    counts["executedCallsWithoutNextRequest"] += len(calls)
                elif next_request["data"]["messages"]["truncated"]:
                    counts["truncatedNextRequests"] += 1
                    counts["callsWithTruncatedNextRequest"] += len(calls)
                else:
                    captured_request = next_request["data"]["messages"]
                    if captured_request["redacted"]:
                        raise ValueError(f"Request identities redacted: {journal}")
                    results = collections.Counter(result["callId"] for message in json.loads(captured_request["content"])
                                                  for result in message["results"] if result["callId"] in ids)
                    if results != ids:
                        raise ValueError(f"Results not returned together: {journal}, turn {event['data']['modelTurn']}")
                    counts["callsVerifiedInNextRequest"] += len(calls)
        if histogram:
            scopes.append(dict(scope=scope, responses=sum(histogram.values()),
                               calls=sum(number * count for number, count in histogram.items()),
                               callsPerResponse=dict(sorted(histogram.items())), **counts))
            overall.update(histogram)
    if not overall:
        raise ValueError(f"No measured Classic responses: {root}")
    return dict(responses=sum(overall.values()),
                calls=sum(number * count for number, count in overall.items()),
                medianCallsPerResponse=statistics.median(overall.elements()),
                callsPerResponse=dict(sorted(overall.items())), scopes=scopes)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--historical-root", required=True, type=Path)
    parser.add_argument("--gpt4o-root", required=True, type=Path)
    parser.add_argument("--sol-root", required=True, type=Path)
    args = parser.parse_args()
    print(json.dumps({label: audit(root) for label, root in (
        ("historical-gpt4o-chat", args.historical_root),
        ("gpt4o-enforced-responses", args.gpt4o_root),
        ("sol-responses", args.sol_root))}, indent=2))


if __name__ == "__main__":
    main()
