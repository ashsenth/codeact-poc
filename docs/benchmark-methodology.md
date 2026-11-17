# Benchmark methodology

The benchmark performs a controlled A/B comparison whose **only intentional variable is
orchestration** (Classic vs CodeAct). The shared prompt body is fingerprinted; mechanism
addenda and model-facing tool surfaces intentionally differ. Matching hashes establish
configuration consistency, not scientific equivalence alone. This is the canonical source
for validity rules; dated reports retain the historical policy in force at execution.

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
| `model` | deployment + temperature + max turns + turn-limit enforcement version + timeout + output settings |
| `pricing` | input/output USD per million |
| `dataset` | dataset manifest hash |

If any fingerprint differs between modes, the comparison is marked **failed** (non-zero exit) and the
mismatch is surfaced in the report. A cross-mode contract test also asserts the tool fingerprint is
identical.

## In-flight request budget

`BENCHMARK_MAX_TURNS` limits dispatched model requests, identically in both modes,
warm-ups and Classic batch workers. The shared observed client blocks request N+1
before forwarding it; a final answer on request N is allowed. Blocking emits
`TURN_LIMIT_REACHED`, ends the run with existing status `Capped` and failure category
`TurnLimitReached`, and is a model outcome, not infrastructure. The blocked attempt
is not counted as a dispatched request. The same 120-second worker watchdog remains.

Execution configuration and the model fingerprint include
`turnLimitEnforcement=model-request-boundary/v1`; do not mix future enforced runs
with historical post-return-only cohorts. The [historical request audit and sandbox
capacity stop decision](infrastructure-gate-20261117.md) annotate existing v1/v2/v3
evidence without rewriting trial data, scores or summaries.

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

## Validity rules

- **Comparison INVALID / stop sweep** means infrastructure/harness failure: auth or model-call
  errors, worker crash, missing guest, fairness/config/prompt-hash mismatch, missing/unpaired
  trials or telemetry reconciliation failure. This includes infrastructure failures during
  the unmeasured warm-ups. Raw evidence remains; aggregate performance statistics are suppressed.
- Explicit tool input errors (`ToolArgumentException` / `ToolNotFoundException`) returned to
  the model with a subsequent model request are model behavior, not infrastructure. Their
  `toolArgumentErrors` count is recorded per trial, deduplicating host/model wrapper events;
  totals appear beside correctness in summaries. Tool implementation exceptions and actual
  sandbox/runtime faults remain infrastructure, with the narrowly evidenced returned-overflow
  exception below. Warm-up task outcomes/counts are recorded separately and do not stop the
  sweep. Errors without evidence of model continuation are not silently exempted.
- A `SandboxOutputOverflow` is a counted **model-visible environment constraint** only
  when the failure envelope is returned for that invocation and its matching function-result
  call ID is carried in a subsequent model request. The provider catches the guest
  serialization abort; this is not silent truncation. Recovery does not erase the overflow
  count. A host crash, other runtime fault, unreturned error or absent delivery/continuation
  evidence remains infrastructure. This rule applies equally to measured runs and warm-ups;
  warm-up counts are never mixed into measured totals.
- **Comparison VALID / recorded** does not mean model success. InvalidSchema, Partial,
  UnearnedCompleteness, declared incomplete, turn cap and TimedOut at the configured deadline are task outcomes;
  they do not stop the sweep when harness evidence reconciles.

`TurnLimitReached` uses existing wire status `Capped`: request N+1 is blocked before
dispatch, while a final answer on request N is allowed. Each analysis uses a separate
worker process. At the configured deadline the parent kills/reaps its process tree;
this watchdog contains native execution but is not native SDK cancellation. Standalone
API hosts have no equivalent worker watchdog. A worker crash unrelated to the deadline
is infrastructure.

Immutable derived reclassification must identify its source and preserve original
trials/statuses/scores/measurements. Zero measured pairs remain invalid for missing
pairs even if a warm-up infrastructure classification changes. Never erase/retry away
model outcomes or blend smoke, controls, aborted attempts and derived duplicates into
measured cohorts.

## Reporting

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
- Analysis duration is the service's run metric, excluding recorded retry wait in current
  Responses cohorts; retry waits and actual HTTP 429s are recorded separately.
  Process-inclusive wall time and the worker deadline include waiting and startup/transport.
  Both modes use the same watchdog and accounting.
- Cost is an estimate from observed tokens and configured retail USD rates, not an invoice.
  Pricing provenance/date/basis are recorded in run metadata. Historical Chat Completions
  estimates used uncached GPT-4o input/output rates (USD 2.50 / 10.00 per million).
  Current Responses cohorts use per-request cache-read/cache-write and context-tier rates;
  reasoning is already output, never an additional charge. See
  [verified meters and rate table](v3-enforced-cohorts.md#verified-retail-pricing).
  Negotiated discounts, unreported usage and infrastructure charges are excluded.
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
