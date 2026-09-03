# Findings

No **Critical** or **High** severity findings. Build, tests, format, and vulnerability scan all pass
(see `command-results.md`). Findings below are Medium and lower.

---

## F-01 — Prompts are not byte-identical across modes (disclosed, intentional)
- **Severity:** Low · **Category:** Fairness / benchmark validity
- **Evidence:** `src/SterlingVale.AgentShared/Prompting/Prompt.cs` — `Classic = Shared + ClassicAddendum`,
  `CodeAct = Shared + CodeActAddendum`; `FairnessFingerprints.Create` hashes only `Prompt.Shared`
  (`src/SterlingVale.AgentShared/Analysis/RunContext.cs`).
- **Why it matters:** The spec lists "same prompt wording" as a fairness control. The two agents
  receive different final instructions (a one-sentence mechanism note). This is defensible — the
  mechanism note *is* the orchestration variable — and is documented (ADR-0010), but it means the
  prompt hash proves only the shared body is identical, not the full instruction string.
- **Verification:** Read `Prompt.cs`; the addendum strings differ by design.
- **Remediation:** Keep as-is but make it explicit in the comparison report (record both full
  instruction hashes alongside the shared-body hash) so readers see exactly what differs.
- **Blocks release/demo:** No.

## F-02 — No `.gitattributes` while `.editorconfig` pins `end_of_line = crlf`
- **Severity:** Medium · **Category:** CI / portability
- **Evidence:** `.editorconfig` line `end_of_line = crlf`; no `.gitattributes` (file search returned
  none); CI runs `dotnet format --verify-no-changes` on `ubuntu-latest` (`.github/workflows/ci.yml`).
- **Why it matters:** Without enforced line endings, a Linux checkout (or a contributor committing
  with LF) can make `dotnet format --verify` fail in CI even though the code is otherwise correct —
  a spurious red build.
- **Verification:** Static (file search + editorconfig read). Not reproduced on Linux here.
- **Remediation:** Add `.gitattributes` with e.g. `* text=auto` and `*.cs text eol=crlf` (or switch
  `.editorconfig` to `end_of_line = lf` and renormalize). Then `git add --renormalize .`.
- **Blocks release/demo:** No; blocks reliable Linux CI.

## F-03 — Offline default benchmark is a plumbing control, not a model comparison
- **Severity:** Low · **Category:** Benchmark interpretation
- **Evidence:** `src/SterlingVale.AgentShared/Fakes/FakeAnalysisPlanner.cs` uses `ExposureOracle` to
  produce the fake answer for **both** modes; both therefore score `Exact` and timings are sub-ms
  (verified in the Phase-8 smoke run and `HarnessEndToEndTests`).
- **Why it matters:** Offline comparison numbers cannot reveal correctness or latency differences
  between Classic and CodeAct — they validate the harness/fairness plumbing only. This is symmetric
  (no mode advantage) and documented (ADR-0011, `benchmark-methodology.md`), but a reader could
  mistake the offline output for a model comparison.
- **Remediation:** The report already prints "offline fake model"; consider a bold banner in the
  Markdown when `Live == false`.
- **Blocks release/demo:** No.

## F-04 — `RetryCount` is always 0 (no retry logic exists)
- **Severity:** Low · **Category:** Metrics completeness
- **Evidence:** `src/SterlingVale.AgentShared/Analysis/AnalysisRunner.cs` sets `RetryCount = 0`
  unconditionally; no retry code anywhere.
- **Why it matters:** The spec asks to "record every attempt" and use the "same retry policy".
  Both are satisfied trivially (no retries, identical for both modes), but the field is vestigial and
  could mislead.
- **Remediation:** Either document "no automatic retries" explicitly, or remove the field if never
  populated.
- **Blocks release/demo:** No.

## F-05 — Fairness mismatch is reported but trials still run first
- **Severity:** Low · **Category:** Benchmark robustness
- **Evidence:** `BenchmarkHarness.RunAsync` computes fingerprints, then runs all trials, then reports
  `Fairness.Matched`. A mismatch marks the comparison failed (non-zero exit) but does not short-circuit.
- **Why it matters:** Wastes work on a comparison already known to be invalid; the outcome (failed)
  is still correct.
- **Remediation:** Optionally abort before the measured trials when `!Matched`.
- **Blocks release/demo:** No.

## F-06 — Hyperlight runtime behavior is not verifiable in this environment
- **Severity:** Low · **Category:** Verification gap
- **Evidence:** `HyperlightRuntimeTests` and `HyperlightEndToEndTests` skip without
  `HYPERLIGHT_PYTHON_GUEST_PATH`; confirmed skipped in `command-results.md`. Deep low-level
  assertions (execute_code stdout, `call_tool`, network/fs denial) are exercised via the API E2E
  path only, not a direct provider-invocation test (preview API uncertainty — `sdk-findings.md` §5).
- **Why it matters:** CodeAct sandbox correctness (isolation, denied network/fs, disposal) is
  asserted by construction/inspection but not runtime-proven here.
- **Remediation:** Run `dotnet test --filter Category=Hyperlight` on a virtualization-capable host
  with a Python guest before publishing any CodeAct runtime claims.
- **Blocks release/demo:** No for offline demo; **yes** before asserting CodeAct runtime results.

## F-07 — CI vulnerability grep may over-match
- **Severity:** Low · **Category:** CI
- **Evidence:** `.github/workflows/ci.yml` greps `-Eiq "(Critical|High|Moderate|Low)"` on
  `dotnet list package --vulnerable` output.
- **Why it matters:** Case-insensitive `Low` could match unrelated text and fail the build spuriously
  (or the reverse). Verified locally the clean output contains none of these words.
- **Remediation:** Match the severity column precisely, or key off the presence of a package table.
- **Blocks release/demo:** No.

## F-08 — README port assumption
- **Severity:** Info · **Category:** Docs
- **Evidence:** `README.md` curl example uses `localhost:5000`; no `launchSettings.json` or documented
  `ASPNETCORE_URLS`.
- **Why it matters:** `dotnet run` may bind a different URL; a new developer's curl could fail.
- **Remediation:** Document `ASPNETCORE_URLS=http://localhost:5000` or add `launchSettings.json`.
- **Blocks release/demo:** No.

## F-09 — Startup configuration validation is partial
- **Severity:** Medium · **Category:** Robustness
- **Evidence:** `Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs`
  `ParseInt`/`ParseDecimal` silently fall back to defaults for malformed `BENCHMARK_*`/`PRICING_*`;
  `/health/ready` validates datasets only, not model/pricing config.
- **Why it matters:** Spec §17 requires configuration validation at startup. A typo in a pricing or
  timeout env var is silently ignored rather than surfaced.
- **Remediation:** Fail fast (or log a warning + expose in `/health/ready`) when a provided
  `BENCHMARK_*`/`PRICING_*` value is present but unparseable.
- **Blocks release/demo:** No.

## Positive confirmations (no issue found)
- No hard-coded secrets, no `.Result`/`.Wait()` blocking calls, no `TODO/FIXME/HACK` in `src`
  (grep; the only `TODO` hits are false positives inside `ToDomain`).
- `AnalysisRunner` never repairs invalid output (schema-invalid ⇒ `Failed`, `report = null`).
- Token usage stays `null` when unknown; cost is `null` when usage is unknown.
- `ConfigureAwait(false)` used in library async paths; `AgentBundle`/chat client disposed via `using`.
- `FileRunStore` uses `ConcurrentDictionary` + per-run directories (unique GUIDs) — concurrency-safe.
- Telemetry tags are limited to `tool.name`/`tool.status`; no prompt/portfolio/secret data.
