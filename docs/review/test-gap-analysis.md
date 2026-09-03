# Test gap analysis

Overall the suite is behavior-focused and strong: **146 passing, 5 gated-skipped, 0 failing** across
10 test projects, including deterministic oracle/invariant tests, a cross-mode contract test proving
identical tool fingerprints, and NetArchTest dependency-direction tests. Gaps below are about
**missing scenarios**, not test count.

## Gap matrix

| ID | Area | Gap | Why it matters | Priority | Proposed test |
| --- | --- | --- | --- | --- | --- |
| TG-01 | AnalysisRunner | No test drives an **invalid model output → `Failed`** through the runner (the fake always emits valid JSON) | Proves the "never repair invalid output" guarantee end-to-end | P1 | Fake plan that `Respond`s malformed JSON; assert `Status=Failed`, `Report=null`, `SchemaValid=false` |
| TG-02 | AnalysisRunner | No **timeout → `TimedOut`** test | Timeout is a core control; currently unproven | P1 | Fake client that delays beyond a 1-tick `ModelConfiguration.Timeout`; assert `TimedOut` |
| TG-03 | AnalysisRunner | No **turn-cap → `Capped`** test | Cap behavior unproven | P2 | Fake plan exceeding `MaxTurns`; assert `Capped`, `Report=null` |
| TG-04 | Metrics | No **missing-usage → null cost** end-to-end test (only client-level `reportUsage:false`) | Ensures "unknown stays unknown" flows to metrics/cost | P2 | Run with `reportUsage:false`; assert `TotalTokens=null`, `EstimatedCostUsd=null` |
| TG-05 | Telemetry | No test asserting telemetry emits **no sensitive data** (spec lists telemetry tests) | Redaction is a stated guarantee | P2 | `MeterListener`/`ActivityListener` capturing tags; assert only `tool.name`/`tool.status`, no args/results |
| TG-06 | Concurrency | No **`FileRunStore` concurrency** test | Artifact store must be concurrency-safe | P2 | Parallel `Save` of distinct records; assert all files written, no exception |
| TG-07 | Cancellation | No **caller-cancellation propagation** test at the API/service level | Distinguishes caller-cancel from timeout | P2 | Cancel the request token mid-run; assert `OperationCanceledException` propagates (not `TimedOut`) |
| TG-08 | Contract | No **runtime** HTTP schema-equality test between the two APIs (fingerprint/factory equality only) | Confirms wire-level parity, not just in-proc | P3 | Spin both APIs via `WebApplicationFactory`; compare `/api/v1/analyses` request/response shapes |
| TG-09 | Hyperlight | Deep runtime assertions (stdout, `call_tool`, network/fs denial, isolation) run only when a guest is present | CodeAct sandbox correctness unproven in CI | P1 (host-gated) | On a virtualization host: drive `execute_code` with code that prints, calls `call_tool`, and attempts network/file (expect denial) |
| TG-10 | Benchmark | No test that **fairness mismatch → failed comparison** (fingerprints always match in tests) | The fail-closed path is untested | P2 | Inject a differing fingerprint; assert `Fairness.Matched=false` and non-zero exit |
| TG-11 | Config | No test for **invalid env config** handling | §17 startup validation | P3 | Set `PRICING_INPUT_PER_MILLION=abc`; assert documented behavior |

## Coverage

Code coverage was **not measured** in this audit (the coverage collector is wired in CI via
`--collect:"XPlat Code Coverage"` but not run here). Per audit rules, no coverage percentage is
claimed. Recommend enabling the CI coverage upload and reviewing the report, treating uncovered
critical paths (TG-01, TG-02, TG-09) as the priority.

## Not present, correctly
- Live-model tests are excluded from the default run (trait-gated + `ENABLE_LIVE_TESTS`) — verified
  skipped, not silently passing.
- Hyperlight tests are gated on `HYPERLIGHT_PYTHON_GUEST_PATH` — verified skipped with reasons; they
  do **not** pass without exercising Hyperlight.
