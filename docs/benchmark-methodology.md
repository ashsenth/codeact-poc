# Benchmark methodology

The benchmark performs a controlled A/B comparison whose **only intentional variable is
orchestration** (Classic vs CodeAct). This document explains how it stays scientifically defensible.

## Paired trials

For each dataset profile the harness runs, per measured trial, **both** modes on the **same**
dataset snapshot. Results are stored with a `pairId` so paired analysis is possible. The default is
**at least 3** measured paired trials.

## Warm-up

Exactly **one unmeasured warm-up per mode** runs before the measured trials to absorb first-call JIT
and initialization costs. Warm-up runs are never included in the reported numbers.

## Run-order randomization

Within each pair, the execution order of the two modes is randomized. The random seed is **derived
from the dataset hash and recorded** in the report, so the run is both randomized and reproducible.

## Fairness — fail closed

Before aggregating, the harness computes fairness fingerprints for each mode and compares them:

| Fingerprint | Source |
| --- | --- |
| `prompt` | SHA-256 of the shared prompt body |
| `schema` | SHA-256 of the output JSON schema |
| `tools` | hash of the 7 tool names + descriptions + schemas |
| `model` | deployment + temperature + max turns + timeout |
| `pricing` | input/output USD per million |
| `dataset` | dataset manifest hash |

If any fingerprint differs between modes, the comparison is marked **failed** (non-zero exit) and the
mismatch is surfaced in the report. A cross-mode contract test also asserts the tool fingerprint is
identical.

## Usage aggregation

Token usage is summed across a run's turns. When the provider does not report usage, the value stays
**unknown (null)** and is never coerced to zero or invented. Aggregates flag whether usage was
complete across trials.

## Correctness scoring

Each run's structured output is compared to the deterministic **oracle** (never prose):

- Flagged-household precision & recall
- Risk-breach precision & recall
- Allocation mean absolute error; maximum allocation error
- Trade-notional mean absolute error (as a fraction of household total)
- Match classification: `Exact`, `WithinTolerance`, `Partial`, `UnearnedCompleteness`, `InvalidSchema`, `Failed`,
  `TimedOut`, `Capped`

Completeness detection uses the same in-scope household set as the oracle, including N=1/N=3
subsets; tools and datasets are not filtered or changed. It reports missing households,
allocation/drift entries for policy target classes, oracle-required breach counts by kind,
and trades for oracle-breached classes (when the oracle produces trades). Flags must follow
reported breaches. No absent breach is required when the oracle finds none; detection cannot
prove that a non-breached limit was actually evaluated.

A schema-valid report claiming complete with missing components is `UnearnedCompleteness`;
a report declaring incomplete stays `Partial`. Missing allocation/trade components yield null
error metrics, propagated to aggregate means/maxima rather than treated as zero error.
Unusable reports and historical trials without this assessment are explicitly unassessed.
Out-of-scope households prevent an oracle pass but do not count as missing in-scope components.
These checks happen after execution for detection/reporting only, with no feedback or retry loop.

Collections are sorted by stable keys before comparison. Two outputs are equivalent only when their
normalized structured data matches — never because prose looks similar.

## Reporting

- **Comparison INVALID / stop sweep** means infrastructure/harness failure: auth or model-call
  errors, worker crash, missing guest, fairness/config/prompt-hash mismatch, missing/unpaired
  trials or telemetry reconciliation failure. This includes infrastructure failures during
  the unmeasured warm-ups. Raw evidence remains; aggregate performance statistics are suppressed.
- Explicit tool input errors (`ToolArgumentException` / `ToolNotFoundException`) returned to
  the model with a subsequent model request are model behavior, not infrastructure. Their
  `toolArgumentErrors` count is recorded per trial, deduplicating host/model wrapper events;
  totals appear beside correctness in summaries. Other tool implementation exceptions and
  actual sandbox/runtime faults remain infrastructure even when the agent later continues.
  Warm-up task outcomes/counts are recorded separately and do not stop the sweep. Errors
  without evidence of model continuation are not silently exempted.
- **Comparison VALID / recorded** does not mean model success. InvalidSchema, Partial,
  UnearnedCompleteness, declared incomplete, turn cap and TimedOut at the configured deadline are task outcomes;
  they do not stop the sweep when harness evidence reconciles.
- `median`, `min`, `max`, and every individual trial are reported. **No trial is removed** (failed
  or slow model task outcomes are kept). Correctness is shown first with both modes' full
  per-trial metrics and medians. Unknown measurements stay unavailable, not zero.
- Efficiency deltas are withheld unless every measured trial is Exact/WithinTolerance.
  When displayed, both modes' correctness appears alongside token/latency/cost deltas.
  Lower usage for an incorrect answer is not an efficiency win.
- The cross-profile `summarize --comparisons <json-paths>` report additionally permits
  **descriptive** changes between all-outcome medians, with an explicit correctness context
  on **each** delta (for example, both Partial / not a correctness-equivalent win).
  It does not populate the suppressed per-comparison win cards. Explicit files preserve
  separate household scopes of one profile; prompt/model/pricing/execution cohorts cannot mix.
  The output is `summary.md` / `summary.html` under ARTIFACTS_ROOT, with direct links to every
  individual trial timeline and each full comparison. No analysis is rerun.
- Summary token medians are medians of each trial's total usage; analysis duration is not
  substituted with process-inclusive duration. A missing measurement makes that median
  unavailable rather than dropping the trial.
- Analysis duration is the service's run metric; process-inclusive wall time additionally
  includes worker startup/transport. Both modes use the same watchdog and accounting.
- Cost is an estimate from observed tokens and configured retail USD rates, not an invoice.
  Pricing provenance/date/basis are recorded in run metadata when configured. The verified
  GPT-4o 2024-11-20 GlobalStandard East US rates on November 16, 2026 are USD 2.50 input /
  10.00 output per million, from the official Azure Retail Prices API (see README and
  `.env.example`). Cached-input discounts, negotiated rates, unreported usage and infrastructure
  charges are not included.
- Pricing fingerprints normalize decimal numeric values (`2.50` equals `2.5`); changing a
  numeric rate still changes the hash. Historical raw hashes/data are never rewritten.
  `reclassify --comparison <json> --source-root <root>` derives a separate comparison from raw
  events and pricing metadata, preserving the original comparison, statuses, correctness scores
  and measurements. An explicit reference prevents mixing prompt cohorts.
- Outputs: per-run artifacts under `artifacts/runs/{runId}/`, and
  `artifacts/comparisons/{comparisonId}.{json,md}`.

## Why no performance outcome is guaranteed

This repository **never** hard-codes an expected winner or savings. With the deterministic fake
model (offline default), both modes emit the oracle result, so correctness is identical and timings
are tiny — a control that validates the harness, not a performance claim. Real differences require a
live model and will vary by model, dataset, and environment.

## Threats to validity

- Provider-side caching or nondeterminism (mitigated: temperature 0 where supported, randomized
  order, paired trials).
- Token-usage gaps (mitigated: unknown stays unknown).
- Environment variance (mitigated: warm-up, median reporting, retained raw trials).
- Preview SDK behavior for CodeAct (documented in `sdk-findings.md`).
