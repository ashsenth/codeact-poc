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
- Match classification: `Exact`, `WithinTolerance`, `Partial`, `InvalidSchema`, `Failed`,
  `TimedOut`, `Capped`

Collections are sorted by stable keys before comparison. Two outputs are equivalent only when their
normalized structured data matches — never because prose looks similar.

## Reporting

- `median`, `min`, `max`, and every individual trial are reported. **No trial is removed** (failed
  or slow trials are kept).
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
