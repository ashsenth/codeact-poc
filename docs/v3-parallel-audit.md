# Classic parallel-call audit

Read-only gate, November 17, 2026; performed before the overflow-policy change
or any resumed live run. Counts below include measured Classic trials only:
historical v3 Chat Completions, enforced 4o Responses, and completed Sol N=1.
Smoke, warm-ups, aborted attempts and derived comparisons are excluded.

## Result: same-turn batching is intact

The approved requirement is complete same-turn batching, not overlapping local
execution. Both APIs and both modes use the same `ObservedFunctionClient`.
Its pinned framework default is sequential local function execution within a
response. That policy is now explicit and recorded as
`functionInvocationPolicy=sequential-within-response/v1` in model configuration
and its fingerprint. No execution-policy change or baseline rerun is needed.
Existing raw fingerprints are not rewritten to add the new explicit marker.

Calls per response -> number of responses:

| Classic cohort | Measured runs | Responses | Function calls | Distribution |
|---|---:|---:|---:|---|
| Historical gpt-4o v3 Chat Completions | 15 | 220 | 768 | 0:15, 1:133, 2:1, 3:29, 4:1, 10:15, 11:5, 13:5, 17:16 |
| gpt-4o v3-enforced Responses | 15 | 286 | 1,191 | 0:1, 1:190, 2:7, 3:19, 4:2, 6:6, 10:10, 11:5, 12:1, 13:17, 14:1, 16:2, 17:10, 18:9, 20:6 |
| Sol v3 Responses | 5 | 30 | 375 | 0:5, 1:5, 2:5, 3:5, 17:4, 26:1, 43:1, 52:4 |

Every response call ID has exactly one local start and completion before the next
request or terminal cap. There are no dropped calls or batches split across model
turns. All 768 historical and 375 Sol calls are present together in their next
captured request. For enforced 4o, 1,098 are directly verified in the next captured
request; 79 calls have truncated next-request history captures (24 requests), and
14 completed immediately before the budget blocked another request. Those are
evidence limitations, not assumed successful deliveries.

The production Responses wire policy parses the actual outgoing JSON and rejects
`parallel_tool_calls` other than true. All 316 measured Classic Responses requests
(286 4o, 30 Sol) recorded that verified setting. The SDK preserves all response
items, including separate reasoning messages. An offline 52-call regression covers
both model identities and both modes, verifying exactly two requests and all 52
distinct `function_call_output` items together in the second request.

## Why 4o hit the request cap

The data do not support a blanket claim that Responses always emits smaller
batches. Repeated calls after useful initial batches explain the N=1 cap.

| Scope | Historical responses per trial | Responses responses per trial | Mean calls/response: Chat -> Responses | Repeated calls: Chat -> Responses |
|---|---|---|---|---|
| N=1 | 6, 6, 6, 5, 41 | 20, 20, 20, 20, 20 | 2.188 -> 3.050 | 22 -> 135 |
| N=3 | 7, 41, 41, 6, 21 | 20, 20, 20, 20, 6 | 2.474 -> 3.547 | 36 -> 86 |
| Full Small | 8, 8, 8, 8, 8 | 20, 20, 20, 20, 20 | 8.525 -> 5.810 | 0 -> 139 |

A repeated call has the same tool name and JSON arguments as an earlier call
within that trial. At N=1, **all 70 single-call Responses responses repeated
previous calls**. In pair 1, turns 1-6 batched policy/accounts, three position
requests, 13 prices, three FX rates, 13 asset classes, and 13 repeated prices.
Turns 7-20 then repeatedly requested already-returned FX/asset-class data.
All these invocations completed before the following request/cap.

This is observed model/API behavior, not an adapter-imposed one-call limit or
causal proof of an API effect. Historical runs lacked in-flight enforcement and
sometimes reached 41 requests, so their early/late exits are not a clean enforced
performance baseline.

## Reproduction

The [committed auditor](../scripts/vm/audit-parallel-calls.py) only reads explicitly
selected roots and raises on missing/split/duplicate invocations or missing results
in a complete capture:

```bash
python3 scripts/vm/audit-parallel-calls.py \
  --historical-root "$HISTORICAL_V3_ROOT" \
  --gpt4o-root "$GPT4O_ENFORCED_ROOT" \
  --sol-root "$SOL_ROOT"
```

Its results are scope-specific, so later Sol profiles do not alter this pre-resume
gate's N=1 evidence. See [cohort configuration and execution status](v3-enforced-cohorts.md)
and the [primary within-model results](v3-responses-results.md).
