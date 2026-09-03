# Executive summary — release-readiness audit

**Auditor role:** independent principal engineer. **Scope:** entire repository, audit-only (no
production code modified). **Date basis:** .NET SDK 9.0.317, Release configuration.

## Verdict: **READY WITH CONDITIONS**

The repository is well-architected, deterministic, and its automated quality gates pass cleanly.
It is ready to demo and to release as an **offline, fairness-controlled reference**. Two conditions
must be met before publishing any **live model** or **CodeAct runtime** performance claim (see
Conditions). There are **no Critical or High severity findings**.

- **Confidence — offline build/test/fairness structure:** **High** (executed and verified).
- **Confidence — live-model & Hyperlight runtime:** **Medium** (not executable in this environment;
  verified by static inspection and correctly-gated skips, not by execution).

## What was executed (all passed — see `command-results.md`)
- `dotnet --info` → SDK 9.0.317.
- `dotnet restore --locked-mode` → exit 0 (lock files honored).
- `dotnet build -c Release` → **0 warnings, 0 errors** (warnings-as-errors + analyzers on).
- `dotnet format --verify-no-changes` → clean.
- `dotnet test -c Release` → **146 passed, 5 gated-skipped, 0 failed**.
- `dotnet list package --vulnerable` (22 projects) → **none**.
- `dotnet list package --outdated` → exit 0.
- Benchmark smoke (`validate-data`, `run --profile small`) → fairness **MATCHED**, artifacts written.

Per the audit rules, **no results were fabricated**: live-model and Hyperlight suites were **not**
run (no credentials, no guest, no virtualization) and are reported as skipped, not passing.

## Requirement completion (see `requirements-traceability.md`)
- **Met:** 26 of 32 requirements.
- **Partial:** #4 prompt byte-identity (shared body identical; 1-sentence mechanism addendum differs
  by design, ADR-0010), #24 sandbox restrictions (correct by inspection, not runtime-verified), #29
  CI (defined, not executed here), #32 startup config validation (datasets validated; env numerics
  fall back silently).
- **Not Verifiable here:** #23 Hyperlight integration runtime (no guest/virtualization).
- **Not Met:** none.

## Benchmark credibility verdict: **CREDIBLE (offline control); live comparison pending**
The comparison is genuinely fair and free of hidden bias:
- Identical dataset, tool set (proven by an equal tool fingerprint across modes), output schema,
  model config, pricing, timeout, and warm-up. The **only** intended variable is the `IAgentFactory`
  orchestration seam.
- No hard-coded winner, savings %, or post-processing; token usage and cost stay `null` when unknown;
  invalid model output is never repaired (marked `Failed`).
- The offline default is a **plumbing control**, not a model-quality measurement (both modes are
  driven by the same oracle-backed fake and both score `Exact`). This is disclosed (ADR-0011).
- **One disclosed asymmetry:** the per-mode prompt addendum (F-01) — minimal, intentional, and the
  addendum *is* the orchestration difference.

A meaningful Classic-vs-CodeAct performance/correctness comparison requires running the **live**
suite against a real model and the **Hyperlight** suite on a virtualization host — neither possible
in this environment.

## Conditions to clear before live/runtime claims
1. Add `.gitattributes` so the Linux CI `dotnet format` check is reliable (F-02).
2. Run the gated **Hyperlight** and **live-model** suites on a capable host and record results
   (F-06, R2/R4).

## Findings by severity
- Critical: 0 · High: 0 · **Medium: 2** (F-02 CRLF/CI, F-09 startup config validation) ·
  **Low/Info: 7** (F-01, F-03, F-04, F-05, F-06, F-07, F-08).
Full detail in `findings.md`; fixes sequenced in `remediation-plan.md`; test gaps in
`test-gap-analysis.md`; runbook check in `runbook-validation.md`.

---

## Appendix A — Independent manual oracle recalculation
Two results were re-derived by hand from the source fixtures and matched the oracle exactly
(`tests/SterlingVale.Application.Tests/OracleTests.cs`).

**A. Balanced household (no drift, no breach, zero trades).**
Positions: EQ 5×100=500, FI 3×100=300, CASH 100×1=100, ALT 7×10=70, CRY 1×30=30.
Total = 500+300+100+70+30 = **1000**. Allocations = 50% / 30% / 10% / 7% / 3%, which equal the policy
targets → drift 0 on every class → no breach → all proposed trade notionals = 0.
Oracle: `TotalValue=1000`, `Flagged=false`, equity `Actual=0.50`, `Drift=0`, trades all `0`. **Match.**

**B. Foreign-currency exposure breach.**
USD leg: 4×100 = 400 USD. EUR leg: 5×100 = 500 EUR × 1.1 = 550 USD. Total = 400+550 = 950 USD.
Non-base fraction = 550 / 950 = **57.9%** > 30% max FX exposure → FX breach.
Oracle: emits `RiskBreachKind.FxExposure`. **Match.**

These hand calculations, plus the 27 `Application.Tests` and the `OracleInvariantTests` run over all
three dataset profiles (rebalancing notionals net ≈ 0, allocations sum to 1, flagged ⇔ breaches>0),
give high confidence the oracle ground truth is correct — which is the foundation of the correctness
scoring the benchmark relies on.
