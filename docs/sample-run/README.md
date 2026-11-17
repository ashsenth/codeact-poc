# Sanitized full-Small sample

These are **captured live results**, not a new execution or an offline pass. Both modes in
measured pair 1 were Completed/**Partial**, not oracle-correct. The original experiment used
three measured pairs; this deliberately small HTML sample contains **only pair 1**.

Source checkout: `55dcaf075369d415abc38dfc59cdcedd42234712`.
Shared prompt hash: `630a3a841752d3b4475fadde5050b2884c11b59b28f87e4c43a694fc57ebbc86`.
Dataset hash: `f686013740692996ccae5dcc82d626b76ffba902d27a84e07842666100c7f684`.
Model fingerprint: `4f121c2cc54e6eac998ba721ae76462b5e9bfb04381f029bb417e6583dac468a`.
No prompts, limits, schema, oracle or original run artifacts were changed to produce this sample.

## Open the evidence

- [Paired comparison HTML](comparisons/full-small-pair-1.html)
- [Field-by-field diagnosis and independent hand check](diagnosis.md)
- [Unchanged C# oracle's H0001 output](oracle-h0001.json)
- Classic: [condensed timeline](runs/sample-classic-1/timeline.md),
  [actual report](runs/sample-classic-1/report.json),
  [tool-invocation excerpt](runs/sample-classic-1/tool-invocations.jsonl)
- CodeAct: [condensed timeline](runs/sample-codeact-1/timeline.md),
  [actual report](runs/sample-codeact-1/report.json),
  [tool-invocation excerpt](runs/sample-codeact-1/tool-invocations.jsonl),
  actual Python [001](runs/sample-codeact-1/codeact/generated-code-001.py),
  [002](runs/sample-codeact-1/codeact/generated-code-002.py),
  [003](runs/sample-codeact-1/codeact/generated-code-003.py),
  [004](runs/sample-codeact-1/codeact/generated-code-004.py).

Classic does not generate Python; no fabricated Classic program is supplied.
CodeAct's first program really failed with `NameError: name 'json' is not defined`.
Programs 002-004 returned successfully, but their analytical output was still Partial.
They are retained byte-for-byte, including the hard-coded empty analytical fields.

## Sanitization and excerpt boundaries

Run IDs in HTML links are aliases. Correlation/trace/invocation identifiers, configuration
payloads, credentials, resource addresses and host filesystem locations are not published.
Model-produced synthetic report IDs/timestamps and synthetic portfolio values are retained.
The model's 2023 report timestamps are its output, not the actual November 2026 run date.

Timelines retain original event sequence numbers, timestamps and model/code boundaries, but
omit individual granular TOOL_CALL rows for size: Classic had **68** invocations, CodeAct
**2,824**. Sequence gaps are intentional. Each invocation excerpt contains the **first unique**
retrieval of H0001 inputs: **35 Classic / 36 CodeAct** records, not all calls. The CodeAct
excerpt also includes its identity USD-to-USD FX query. `sourceInvocationOrdinal` refers to
the original invocation journal's one-based row, not a renumbered claim about execution order.
The excerpts whitelist tool name, parsed arguments/response, duration and status; capture
payloads were checked untruncated/unredacted before extraction.

The original six measured attempts, full journals and cross-profile summary remain separate.
See the [three-trial sweep record](../review-current/x64-validation.md#three-trial-sweep-and-revised-validity-november-16-2026)
and [README Results](../results.md#results-gpt-61-sol-v3).
