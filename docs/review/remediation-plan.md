# Remediation plan

Priorities: **P0** = must fix before any release (correctness/security blocker). **P1** = fix before
publishing benchmark/CodeAct claims. **P2** = should fix. **P3** = nice-to-have. There are **no P0
items** — no Critical/High findings and all executed gates pass.

**Status update (post-audit remediation):** R1, R3, R5, R6, R8 (partial) are **Done** and verified —
full solution now **159 passed / 5 gated-skipped / 0 failed**, `dotnet format --verify-no-changes`
exit 0. R2 and R4 remain **blocked in this environment** (require a virtualization-capable host and
Azure credentials) and must be run on a capable host.

## P0 — Release blockers
_None._

## P1 — Before publishing benchmark or CodeAct runtime claims
| ID | Action | Owner effort | Verification | Status |
| --- | --- | --- | --- | --- |
| R1 (F-02) | Add `.gitattributes` (`* text=auto`, `*.cs text eol=crlf`); `git add --renormalize .` | XS | `dotnet format --verify-no-changes` passes | **Done** — `.gitattributes` added; format exit 0 |
| R2 (F-06/TG-09) | Run `dotnet test --filter Category=Hyperlight` on a virtualization-capable host with a Python guest; record results | M | Hyperlight suite passes (not skipped) | **Blocked** (no guest/virtualization here) |
| R3 (TG-01/TG-02) | Add negative runner tests: invalid-output→`Failed`, delay→`TimedOut` | S | New tests pass; guarantees proven | **Done** — `AnalysisRunnerTests` (5 tests) |
| R4 (live) | Run `ENABLE_LIVE_TESTS=1` live suite against a real Azure OpenAI deployment before quoting any Classic-vs-CodeAct numbers | M | Live suite passes; report shows `Live=true` | **Blocked** (no credentials here) |

## P2 — Should fix
| ID | Action | Effort | Verification | Status |
| --- | --- | --- | --- | --- |
| R5 (F-09/§17) | Fail-fast (or warn + surface in `/health/ready`) on unparseable `BENCHMARK_*`/`PRICING_*` env | S | Invalid value produces a clear error/health signal | **Done** — fail-fast + `ConfigurationValidationTests` (7 cases) |
| R6 (TG-05) | Add telemetry-redaction test (capture Activity/Meter tags; assert only `tool.name`/`tool.status`) | S | Test asserts no sensitive tags | **Done** — `TelemetryRedactionTests` |
| R7 (TG-06) | Add `FileRunStore` concurrency test | S | Parallel saves succeed | Open |
| R8 (TG-03/TG-04/TG-10) | Add `Capped`, null-cost e2e, and fairness-mismatch tests | S | Tests pass | **Partial** — `Capped` + null-cost done in `AnalysisRunnerTests`; fairness-mismatch open |
| R9 (F-01) | Record both full per-mode instruction hashes in the comparison report alongside the shared-body hash | XS | Report shows exactly what differs by mode |
| R10 (F-03) | Add a prominent "offline / fake model — not a model comparison" banner to the Markdown report when `Live=false` | XS | Banner visible |

## P3 — Nice-to-have
| ID | Action | Effort |
| --- | --- | --- |
| R11 (F-04) | Document "no automatic retries" or remove the vestigial `RetryCount` | XS |
| R12 (F-05) | Abort benchmark before measured trials when fairness fingerprints mismatch | XS |
| R13 (F-07) | Tighten CI vuln-scan match to the severity column | XS |
| R14 (F-08) | Document `ASPNETCORE_URLS` / add `launchSettings.json` | XS |
| R15 (runbook) | Add guest-acquisition + WHP/KVM enablement + clean-up notes to README | S |
| R16 (TG-08) | Add a runtime HTTP contract-equality test across both APIs | S |

## Sequencing
1. **R1** (unblocks reliable CI immediately).
2. **R3, R6, R7, R8** (fast, high-value test hardening — no production changes).
3. **R2, R4** on a capable host (prerequisite for any performance/runtime claim).
4. **R5, R9, R10**, then P3 polish.

All P1/P2 test additions are additive (new test files) and require **no production code change**
except R1 (repo-config file) and R5 (startup validation).
