# POC branch review and verified offline demo

Reviewed November 5, 2026 (final control artifacts timestamped November 6 UTC).
Scope: `main...9cde519` on `feature/observability-validation`, all six branch commits,
source/test/script changes and dependency manifests/lock-file changes. The corrective commit
`f44d9f2` was then validated in a separate clean local-source clone.
This is a demo-focused review, not production certification or a live performance study.
The attempted background review was cancelled without results; findings below come from direct
diff/source inspection and execution, not an independent second review.

## What the application does

The generator creates three reproducible synthetic wealth portfolios. The shared analysis service
loads a portfolio snapshot and exposes seven read-only functions for households, policies, accounts,
positions, prices, FX and classification. A Classic agent calls those functions directly; a CodeAct
agent requests Python execution, which should call the same functions through a Hyperlight guest.
Both return an exposure/rebalancing report through the same schema and HTTP contract.

The benchmark runs a warm-up per mode, then paired trials in seeded randomized order. It retains
all measured attempts, scores reports against a deterministic oracle, checks configuration
fingerprints and reconciles telemetry. JSON, Markdown, CSV and self-contained HTML reports accompany
run-level results, metrics, model/tool journals, generated-code captures and ordered timelines.
The benchmark CLI is the simplest complete demo; separate API hosting is unnecessary for it.

Offline, the answer comes from the oracle-backed fake planner after one representative tool call.
CodeAct's no-guest function does not interpret Python or invoke portfolio tools. Exact offline
scores therefore establish harness integration, not independent reasoning or sandbox correctness.

## What this branch added

| Commit | Change |
| --- | --- |
| `595b114` | Correlation-ID logging, model transcript and host invocation capture. |
| `03b9833` | Optional Classic household batching, application merging and summed metrics. |
| `ef74f25` | Run/comparison/trial/trace identity, boundary journals, capture policy, timelines, observed model/function clients, reconciliation, real-guest tests and richer reports. |
| `d11ffcd` | Isolated validation gates, test evidence counting, approval-gated x64 provisioning and bootstrap scripts. |
| `9fb037d` | Detailed x64 workflow and corrected Phase A evidence, including SKU/TRX findings. |
| `9cde519` | Documentation sanitization with placeholders rather than environment-specific values. |

Important qualifications:

- The prompt fingerprint covers the shared task body, not the differing mechanism addenda.
- Classic batching is an additional experimental variant: a child-run collection merged by the
  application is not the same orchestration experiment as one unbatched model run.
- Model-facing tool requests, granular data-tool invocations and sandbox callbacks are different.
- Telemetry validity, schema completion and oracle accuracy are separate checks.
- HTTP SDK-internal retries and Hyperlight internal CPU/memory/initialization/cleanup are not
  separately measured. The shared runner does not use streaming.
- Guest configuration is not proof of actual execution, and the CLI's exit status checks fairness
  and reconciliation, not exact oracle correctness. Inspect the raw trial scores.

## Confirmed bugs and demo impact

| ID | Location | Confirmed issue | Blocks offline demo? | Disposition |
| --- | --- | --- | --- | --- |
| D1 | [ComparisonWriter](../../src/SterlingVale.Benchmark/Reporting/ComparisonWriter.cs), report rendering | Generated HTML/Markdown/CSV lacked consistent execution-kind disclosure. The scaling page unconditionally claimed Classic failed, even when its matrix showed successful controls; other prose asserted fewer calls or inferred failure causes without evidence. | **Yes:** the main visual deliverable misrepresented the demonstrated results. | Fixed in `f44d9f2`; labels cover fake, stand-in, live-configured and historical/unknown results. Offline efficiency headlines are suppressed, CSV carries execution kind, and completion text no longer invents performance or failure causes. |
| D2 | [RunArtifacts.cs](../../src/SterlingVale.AgentShared/Artifacts/RunArtifacts.cs) | Full capture bounded the entire combined transcript string, discarding the capture envelope. Medium/Large files ended inside a JSON record and could omit later Python arguments/tool calls. | **No** for the original result demo, but unsafe for a complete transcript walkthrough. | Fixed in the November 11 follow-up: retain every message/call/result identity; capture individual payload fields with explicit limits/redaction metadata. Regression coverage includes oversized text, Python arguments, results, later messages and all capture modes. |
| D3 | [provision-vm.sh](../../scripts/provision-vm.sh), line 30 | The public-key regex accepts the literal token `ecdsa-sha2-` followed immediately by a space, not real tokens such as `ecdsa-sha2-nistp256`. Valid ECDSA public keys are rejected. | **No:** provisioning is paused and is not part of this demo. | Confirmed against a synthetic algorithm/key-line fixture without executing the script. Ed25519/RSA are unaffected. Fix and test ECDSA acceptance during the approved Linux-script follow-up. |

No larger runner, prompt, sandbox or batching redesign was performed. No other crash or wrong
result blocked the tested offline sequence. The new report test adds one pass to the 197-test
baseline; the optional helper's exact-count gate was correspondingly updated to 198, not weakened.

## Execution evidence

Two new local-source clones were used, neither containing the original checkout's generated data
or build output. Both used the same installed SDK and existing package cache/feed configuration.
The first tested `9cde519`; the second tested `f44d9f2`. This is not a remote clone, empty package
cache, clean operating-system installation, or Linux/macOS test.

The final clone ran the README's environment isolation and:

1. Locked restore.
2. Release build without restore.
3. Release DataGenerator into a new data directory.
4. `validate-data` against all generated profiles.
5. `compare --trials 1 --log-level normal --payload-capture full`.
6. `visualize` over those new comparisons.
7. All non-live/non-Hyperlight tests.
8. The separately filtered Hyperlight test project, with guest unset.

Every command returned zero. Build: **0 warnings, 0 errors**.
The offline comparisons had exactly two measured trials per profile, both Completed/Exact,
matching fingerprints and valid telemetry. Independent inspection of comparison JSON, not just
the benchmark exit code, confirmed those conditions.

### Tests, literally parsed from fresh TRX files

| Project suffix | Before correction | After correction |
| --- | ---: | ---: |
| AgentShared.Tests | 39 | 39 |
| Application.Tests | 27 | 27 |
| Architecture.Tests | 6 | 6 |
| Classic.IntegrationTests | 16 | 16 |
| CodeAct.IntegrationTests | 7 | 7 |
| Comparison.Tests | 27 | 28 |
| Contract.Tests | 5 | 5 |
| Data.Tests | 11 | 11 |
| Domain.Tests | 20 | 20 |
| Tools.Tests | 39 | 39 |
| **Total passed** | **197** | **198** |

Both non-live runs: zero failed and zero skipped across ten projects.
Separate Hyperlight run: **0 passed, 0 failed, 6 skipped**, not six passes.
Fresh results directories and `Get-Content -LiteralPath` avoided the previous bracketed-filename
recount bug. Targeted report tests also passed before the full suite.

### Final offline controls: not live performance

| Profile | Classic / CodeAct ms | Fake tokens each | Formula USD each | Exact / fairness / telemetry |
| --- | ---: | ---: | ---: | --- |
| Small | 20.1399 / 17.7399 | 4,174 | 0.041425 | Both Exact / MATCHED / VALID |
| Medium | 40.9000 / 40.1731 | 16,230 | 0.161970 | Both Exact / MATCHED / VALID |
| Large | 113.1979 / 106.6385 | 49,208 | 0.491765 | Both Exact / MATCHED / VALID |

These are real harness wall times with fake-model inputs, not model latency or sandbox execution.
Synthetic usage and price arithmetic are not spending. All profiles had two turns per mode,
one model-facing invocation per mode, granular calls 1/0, execute_code calls 0/1 and callbacks 0/0
(Classic/CodeAct). Allocation/trade errors were zero and flagged/breach precision/recall were one.

Final comparison IDs (sanitized; exact values remain in local ignored evidence):

- `<SMALL_COMPARISON_ID>`
- `<MEDIUM_COMPARISON_ID>`
- `<LARGE_COMPARISON_ID>`
- Scaling report: `scaling-20261106003012.html`.

Local ignored evidence is under
`artifacts/demo-validation/20261105-demo/verified/`: clone/restore/build/generation/validation/
comparison/visualization/test logs, fresh TRX files, and `demo-artifacts/` containing all report
and run files. It is intentionally not committed. The requested README commands reproduce the
same shapes with new timestamps/IDs. The sanitized result table above is committed.

## What is not verified

- No Azure authentication, model invocation, billing, quotas or live correctness was checked.
- No real Python, Hyperlight guest, host callback, guest exception, cancellation or isolation ran.
- No provisioning/bootstrap command, including plan modes, was executed. No Azure CLI was used.
- No execution-policy or global Git configuration was changed.
- A browser-open request for the generated scaling HTML timed out without returning a page handle.
  CLI generation and report tests passed; visual inspection is not claimed.
- Separate HTTP processes were not launched; existing in-process offline API integration tests passed.
- Remote-origin clone, uncached restore, Linux/macOS commands and installation scripts remain pending.
- Historical live numbers do not certify these changes; see [historical notes](historical-live-notes.md).

## Best demo path and what x64 adds

Use the [README offline quickstart](../../README.md#5-quickstart-offline-demo), then inspect the
new scaling report, a profile's correctness table and the corresponding event journal. Small is
the simplest source/transcript walkthrough. The November 11 fix preserves transcript JSONL framing
and later call records even when earlier payloads exceed the capture cap.
Medium/Large demonstrate deterministic scaling of data volume and artifact generation, not agent
performance.

An authorized live Azure OpenAI model plus a supported x64 Hyperlight guest would add actual
model decisions, provider usage, generated Python execution, real data callbacks, failure behavior
and an independently scored answer. Repeated live Small/Medium/Large pairs can then address the
POC's accuracy/token/latency/cost/call-count question. First pass all six real-guest tests.

Prioritized follow-up is in [README Next steps](../../README.md#10-next-steps).
### November 11 pre-push follow-up

- Historical live claims now identify source commits `03cb30a` and `5a65c6e` (September 29, 2026),
  separately from archive snapshot `9cde519` (November 5), and explicitly say **NOT RE-VERIFIED**.
  Commit dates do not certify execution dates.
- Transcript cutoff could lose Python arguments and later tool calls. The fix preserves every
  message/call/result identity with per-field payload envelopes rather than a whole-file substring.
  Oversized payloads remain bounded and explicitly truncated; separate exact Python files retain
  their existing safe-capture rules. This is a capture fix, not an execution change.
- Release build: zero warnings/errors. The prior 198 tests plus one new regression test all passed:
  **199 passed, 0 failed, 0 skipped across ten projects**. Fresh TRX files were read literally.
- All six measured Small/Medium/Large controls completed with Exact scores and valid telemetry.
  All 18 measured transcript messages parsed as JSONL; two oversized text fields explicitly
  reported truncation without losing later records. This remains fake-model evidence.
- The ECDSA validation bug is explicitly retained in README Next steps; provisioning is still paused.
- Scanned all 18 README/docs files and replaced literal run/comparison IDs and local bind endpoints
  with placeholders. No machine-specific absolute paths, Azure resource endpoints, identity GUIDs
  or internal URLs remained. Public source links, package names, source commit hashes and relative
  artifact locations are intentionally retained. Every relative documentation link resolved.
- Fresh ignored evidence: `artifacts/pre-push-validation/<VALIDATION_RUN_ID>/`, including build,
  tests, comparisons and actual run artifacts. No live calls, provisioning or push was performed.

These follow-up counts do not change the historical counts above.
Original acceptance criteria, TRX forensics and SKU evidence remain in
[execution-observability.md](execution-observability.md); they are historical deep detail, not
conditions for showing the honest offline demo.
