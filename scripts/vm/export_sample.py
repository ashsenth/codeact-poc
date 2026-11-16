"""Export an explicitly selected pair; review all generated content before sharing."""
import argparse
import json
import re
from pathlib import Path

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("--comparison", type=Path, required=True)
parser.add_argument("--dataset-profile", type=Path, required=True)
parser.add_argument("--output", type=Path, required=True)
parser.add_argument("--household", required=True)
parser.add_argument("--pair", type=int, default=1)
args = parser.parse_args()
report = json.loads(args.comparison.read_text())
source = args.comparison.resolve().parent.parent / "runs"
target = args.output.resolve()
if target.exists():
    parser.error("Choose a new output directory; existing evidence is never overwritten.")
accounts = json.loads((args.dataset_profile / "accounts.json").read_text())
account_ids = {account["id"] for account in accounts if account["householdId"] == args.household}
positions = json.loads((args.dataset_profile / "positions.json").read_text())
symbols = {position["symbol"] for position in positions if position["accountId"] in account_ids}
households = json.loads((args.dataset_profile / "households.json").read_text())
base_currency = next(household["baseCurrency"] for household in households if household["id"] == args.household)
trials = [trial for trial in report["rawTrials"] if trial["pairId"] == args.pair]
if len(trials) != 2 or {trial["mode"] for trial in trials} != {"classic", "codeact"}:
    parser.error("Select a pair with both recorded modes.")

def safe_text(text):
    if re.search(r"https?://|/home/|/root/|[A-Za-z]:[\\/]|api[_-]?key|Bearer\s", text, re.I):
        raise ValueError("Potential endpoint, local path or credential in sample content; review source first.")
    return text

for trial in trials:
    mode, run_id = trial["mode"], trial["runId"]
    original = source / run_id
    output = target / "runs" / f"sample-{mode}-1"
    output.mkdir(parents=True, exist_ok=True)
    events = [json.loads(line) for line in (original / "events.jsonl").read_text().splitlines()]
    invocations = [json.loads(line) for line in (original / "tools/invocations.jsonl").read_text().splitlines()]
    selected = []
    seen = set()
    for ordinal, invocation in enumerate(invocations, 1):
        name = invocation["toolName"]
        arguments = json.loads(invocation["arguments"]["content"])
        keep = (
            name == "list_households"
            or name in {"get_policy", "list_accounts"} and arguments.get("householdId") == args.household
            or name == "list_positions" and arguments.get("accountId") in account_ids
            or name in {"get_price", "asset_class"} and arguments.get("symbol") in symbols
            or name == "get_fx" and arguments.get("toCurrency") == base_currency
        )
        key = (name, json.dumps(arguments, sort_keys=True))
        if not keep or key in seen:
            continue
        seen.add(key)
        assert all(not invocation[field]["truncated"] and not invocation[field]["redacted"]
                   for field in ("arguments", "response"))
        selected.append({
            "sourceInvocationOrdinal": ordinal,
            "toolName": name,
            "arguments": arguments,
            "response": json.loads(invocation["response"]["content"]),
            "durationMs": invocation["durationMs"],
            "status": invocation["status"],
        })
    (output / "tool-invocations.jsonl").write_text(
        safe_text("".join(json.dumps(item, separators=(",", ":"), ensure_ascii=True) + "\n" for item in selected)))
    result = json.loads((original / "result.json").read_text())
    (output / "report.json").write_text(safe_text(json.dumps(result["report"], indent=2, ensure_ascii=True) + "\n"))
    lines = [
        f"# {mode.title()} {report['profile']} pair {args.pair}: condensed recorded timeline",
        "",
        "Derived from captured evidence, not a replay. Original ordering, sequence numbers and UTC timestamps are retained.",
        f"The {len(invocations)} granular tool invocations' individual TOOL_CALL_* rows are omitted for size; "
        f"the [invocation excerpt](tool-invocations.jsonl) contains {len(selected)} first-unique household input retrievals.",
        "Invocation identifiers, traces, correlations and configuration payloads are omitted. Gaps in sequence numbers are intentional.",
        "",
        "| Source sequence | UTC timestamp | Event | Status | Duration ms | Code reference |",
        "|---:|---|---|---|---:|---|",
    ]
    for event in events:
        if event["eventType"].startswith("TOOL_CALL_"):
            continue
        details = event.get("data") or {}
        reference = details.get("reference") or details.get("generatedCodeReference") or ""
        if not reference.startswith("codeact/generated-code-"):
            reference = ""
        if reference:
            reference = f"[{reference}]({reference})"
        duration = event.get("durationMs")
        lines.append(
            f"| {event['sequenceNumber']} | {event['timestamp']} | {event['eventType']} | "
            f"{event['status'] or ''} | {'' if duration is None else format(duration, '.3f')} | {reference} |")
    if mode == "codeact":
        lines += ["", "Captured programs are retained unchanged below codeact/. Execution success is not an oracle pass."]
        code = output / "codeact"
        code.mkdir(exist_ok=True)
        for program in sorted((original / "codeact").glob("generated-code-*.py")):
            (code / program.name).write_text(safe_text(program.read_text()))
    else:
        lines += ["", "Classic did not generate Python; consult the recorded outcome for correctness."]
    (output / "timeline.md").write_text(safe_text("\n".join(lines) + "\n"))
    print(mode, "source invocations", len(invocations), "excerpt", len(selected), "timeline rows", len(lines) - 9)

(target / "comparisons").mkdir(exist_ok=True)
