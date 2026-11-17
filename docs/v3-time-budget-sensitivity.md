# Time-budget sensitivity (120 s vs 600 s)

Separate Sol cohorts: `gpt-6.1-sol-v3` (120 s) and `gpt-6.1-sol-v3-t600` (600 s).
Only the timeout changes for both modes; the external worker watchdog uses that same timeout. Timeout is part of each model fingerprint. No pooled samples or combined medians across budgets.
Same deployment/version, pricing, prompt, schema, tools, dataset, 20-request turn budget, 16,384-token output cap, default medium reasoning and sequential local invocation.
Five measured pairs per completed scope; one warm-up per mode excluded. All measured outcomes remain in medians. Timeouts are deadline outcomes, not TurnLimitReached.
Analysis excludes retry wait; the watchdog includes waiting. Tokens include reasoning once; costs are retail estimates.
Missing rows are not run, never zero-success results. Archived attempts, smoke and N=1 are excluded.

## Per scope and mode

Deltas are (600 s / 120 s - 1) within Sol and the same scope/mode; equal correct counts alone permit a correctness-equivalent label. Equal zero counts are not an efficiency win.

| Scope | Mode | Budget s | Correct | Timeouts | TL | Median analysis s | Median tokens | Median USD | Delta tokens / cost / time vs 120 s | Correctness context |
|---|---|---:|---|---:|---:|---:|---:|---:|---|---|
| n3 | classic | 120 | 0/5 | 5 | 0 | 120.028 | 121,483 | 0.1108500 | -- | -- |
| n3 | classic | 600 | not run | -- | -- | -- | -- | -- | -- | -- |
| n3 | codeact | 120 | 5/5 | 0 | 0 | 50.546 | 30,504 | 0.0539617 | -- | -- |
| n3 | codeact | 600 | not run | -- | -- | -- | -- | -- | -- | -- |
| full-small | classic | 120 | 0/5 | 5 | 0 | 120.033 | 81,439 | 0.0348349 | -- | -- |
| full-small | classic | 600 | not run | -- | -- | -- | -- | -- | -- | -- |
| full-small | codeact | 120 | 5/5 | 0 | 0 | 100.498 | 44,606 | 0.1132577 | -- | -- |
| full-small | codeact | 600 | not run | -- | -- | -- | -- | -- | -- | -- |

## Within each budget: CodeAct versus Classic

Each arrow is Classic -> CodeAct; deltas are (CodeAct / Classic - 1). Correctness-equivalent means matching correct counts, not necessarily the same correct pairs.

| Cohort | Scope | Correct | Unearned | TurnLimitReached | Argument errors | Overflow errors | HTTP 429 | Median tokens | Median USD | Analysis s | Retry wait s | Delta tokens / cost / analysis | Correctness context |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| gpt-6.1-sol-v3 | n3 | 0/5 -> 5/5 | 0 -> 0 | 0 -> 0 | 0 -> 0 | 0 -> 1 | 0 -> 0 | 121,483 -> 30,504 | 0.1108500 -> 0.0539617 | 120.028 -> 50.546 | 0.000 -> 0.000 | -74.9% / -51.3% / -57.9% | not correctness-equivalent: correct counts differ |
| gpt-6.1-sol-v3-t600 | n3 | not run | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- |
| gpt-6.1-sol-v3 | full-small | 0/5 -> 5/5 | 0 -> 0 | 0 -> 0 | 0 -> 0 | 0 -> 1 | 0 -> 0 | 81,439 -> 44,606 | 0.0348349 -> 0.1132577 | 120.033 -> 100.498 | 0.000 -> 0.000 | -45.2% / +225.1% / -16.3% | not correctness-equivalent: correct counts differ |
| gpt-6.1-sol-v3-t600 | full-small | not run | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- |

## Reproduction

Use the [manual wrapper](../scripts/vm/run-sol-t600.sh) after a clean Release build, tests and push. It checks, reports, commits and pushes N=3 before launching full Small. Model failures continue; infrastructure or checker/publication failure prevents the next profile.
Set the documented Azure/guest/dataset environment; see [launch instructions](running-experiments.md#time-budget-sensitivity-600-seconds). The wrapper is prepared for manual launch, not automatically started.

Regenerate this report without any inference:

```bash
python3 scripts/vm/report-v3-cohorts.py --sol-root "$SOL_BASELINE_ROOT" \
  --sol-t600-root "$SOL_T600_ROOT" --output docs/v3-time-budget-sensitivity.md
```
