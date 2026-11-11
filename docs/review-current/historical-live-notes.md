# Historical live-run notes from the previous README

**Archived claims, not current validation evidence.** This section was removed from the README
to avoid presenting historical model/guest results as verification of the observability branch.
No live model or real Hyperlight execution was reproduced in the November 5 POC review.
The table and observations below preserve the former README's record; they are not endorsed
as a causal explanation, general model limitation, fair performance comparison or reproducible
measurement of the current code.

## Previously reported results

The old README described Azure OpenAI gpt-4o, three trials per profile on an x64 Linux VM with
a real Hyperlight sandbox, with these approximate totals:

| Profile | Households | Classic completed | CodeAct completed | Classic median tokens / estimated USD | CodeAct median tokens / estimated USD |
| --- | ---: | --- | --- | --- | --- |
| Small | 10 | 0/3 | 2/3 | approximately 40.7k / 0.11 | approximately 55.8k / 0.23 |
| Medium | 40 | 0/3 | 3/3 | approximately 33.3k / 0.09 | approximately 62.0k / 0.24 |
| Large | 120 | 0/3 | 3/3 | approximately 60.5k / 0.16 | approximately 65.8k / 0.28 |

It said Classic made 54-81 tool calls over 4-5 turns and emitted an empty `households` array
rather than the expected report. A reproduced placeholder was described as:

```json
{ "runId": "run_20231010_001", "generatedAt": "2023-10-10T00:00:00Z", "households": [] }
```

It also described an anti-placeholder prompt experiment returning the same placeholder and
subsequently being reverted. The old prose inferred that prompting could not solve the output
problem and ruled out tool/context caps. Those broad conclusions are **not established by the
current review**. Historical empty output alone cannot establish cause or a model-wide limit.
Failed completion is not an efficiency baseline.

The old CodeAct explanation described generated Python assembling reports, with a roughly 16 KB
guest output constraint requiring several small `execute_code` chunks and final model assembly.
The current prompt still requests chunked output; actual guest limits and behavior must be
measured on the matching x64 guest.

## Historical fairness caveat and proposed work

The prior README explicitly warned that Classic's failures made token/cost/latency comparisons
not like-for-like. It also noted that the old tool counter did not observe CodeAct's callbacks.
This branch adds separate model-facing/granular/callback counts, but that does not retrospectively
correct old metrics or certify new live results.

Ideas previously proposed for making Classic completable included a `submit_report` function,
structured outputs, or incremental household emission. They remain possible future experiments,
not changes authorized or implemented in this review.

`CLASSIC_BATCH_SIZE` was added as an explicitly different variant: run Classic over small household
subsets, merge results in the application and sum metrics, leaving CodeAct unchanged. Offline
batch behavior is tested; live quality is not certified.

## Where to go next

- [Current README](../../README.md): verified offline demo and prioritized next steps.
- [POC demo review](poc-demo-review.md): actual current controls and remaining bugs.
- [Original observability review](execution-observability.md): historical acceptance matrix,
  Phase A records, TRX forensics and SKU findings.
- [Optional x64 workflow](x64-validation.md) and [older VM helpers](../../scripts/vm/):
  unverified setup/run paths, not permission to provision.

Historical comparison files may exist in a developer's ignored `artifacts/` directory. They are
not prerequisites, clean-clone inputs, or replacements for fresh live trial evidence.
