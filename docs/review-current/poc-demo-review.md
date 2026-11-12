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

## November 11 live diagnostics and laptop generation

The original failed `events.jsonl` contained only `ClientResultException` and duration. It did
not retain the HTTP status, service code or message; the original cause could not be reconstructed.
A bounded repeat with the new diagnostic client established **HTTP 404** for both modes.
Both measured attempts failed. Console/JSON/HTML marked the comparison invalid and suppressed
aggregate statistics despite correctly reconciled telemetry.

The configured HTTPS endpoint had a non-root path. A second bounded repeat changed only the
child process's endpoint to the same authority with `/`; it did not alter saved configuration,
authentication, resource access, deployment or any Azure resource.

| Observed after using the base endpoint | Evidence |
| --- | --- |
| Azure connectivity/authentication | Model responses and real tool calls succeeded; no model-request failure was observed. This does not certify every deployment or future access. |
| Measured live Classic | Seven model responses and 51 model-facing calls; final JSON had an empty households array, so status Failed/InvalidModelOutput. No correctness or performance success claimed. |
| Separate scoped live Classic API check | A one-household request returned Completed, one household, six model responses, 22 model-facing/granular calls, valid schema and valid telemetry. It was not independently oracle-scored; this is not a full Small comparison. |
| Measured live CodeAct, no guest | One model response and one execute-code request. Exact generated Python saved as `codeact/generated-code-001.py` (5,508 bytes). No Python or granular callback executed. |
| CodeAct outcome | `NotExecuted`, category `NoHyperlightGuest`, reason `not executed: no Hyperlight guest`; no stand-in success was substituted. |
| Comparison | Completion 0/2, correctness 0/2; invalid with InvalidModelOutput/NoHyperlightGuest. Exit 1; aggregate statistics suppressed; raw attempts retained. |

Artifacts are in ignored `artifacts/live-diagnostic-current/` and `artifacts/live-base-endpoint/`.
Their root indexes link comparisons, measured/warm-up run statuses, timelines and available source
and invocation records. Run IDs and environment-specific values are deliberately omitted here.
The scoped API evidence is in ignored `artifacts/live-classic-scoped/`. Readiness first returned
503 because relative dataset paths were resolved from the API project content root. Restarting with
absolute data/artifact roots produced healthy readiness and the completed scoped request. Both
temporary loopback API processes were stopped; no server or VM was left running.
Live results are current narrow connectivity/generation evidence, not the historical performance
claims retained in [historical-live-notes.md](historical-live-notes.md).

### Changes and regressions

- Fake/live is explicitly selected; default fake ignores Azure endpoint variables. Invalid modes
  and missing live configuration fail rather than substituting a fake result. APIs require
  explicit `--model live` or `MODEL_MODE=live` too.
- Typed SDK failures are mapped to safe category/status/allowlisted-code diagnostics. Raw error
  bodies, unknown service codes, credentials, endpoint URLs and local paths are not copied.
- All report surfaces put completion/oracle scores before telemetry. Any incomplete measured
  attempt invalidates the whole comparison, suppresses medians/deltas/charts and returns nonzero.
- Normal logs print full IDs at run start only, then an eight-character prefix plus event/tool/time.
  Verbose/debug and on-disk events retain full identity.
- Warm-ups are labelled on every event, persisted and indexed, but excluded from measurements.
  Shared API/benchmark run persistence refreshes the artifact-root index.
- Generation-only mode captures every program requested in the current response, invokes no
  execution function and stops the model loop. Capture bounds/redaction remain enforced.
- Six new regression tests cover diagnostics, invalid renderers/JSON, explicit selection,
  compact logging and generation-only source/status/termination. Existing end-to-end controls
  additionally check warm-up exclusion and artifact-index links. The non-live suite has
  **205 tests**, up from 199; the optional strict helper's expected count is updated.

Final validation: Release build had zero warnings/errors. Ten fresh TRX files were read with
literal paths: **205 passed, 0 failed, 0 skipped**. Freshly generated Small/Medium/Large controls
all completed Exact with matching fairness and valid telemetry, followed by successful scaling
visualization, while Azure remained configured. An additional CLI run with no model option
selected fake; invalid/missing model option values returned exit 2. Full run IDs occurred exactly
once per run in the normal-level demo log (12 measured/warm-up runs), and all 27 index links resolved.
These controls are fake-model harness evidence, not live performance. No push or VM action occurred.

### Azure setup templates

These are **not yet verified/executed administrative setup templates**. Azure CLI was not invoked:
it has hung on this host previously. Substitute placeholders locally; never commit their values.
No new role is needed merely to fix the endpoint in the successful connectivity retry.

```powershell
az login --tenant <tenant-id>
az account set --subscription <subscription-id>
az cognitiveservices account deployment list --resource-group <resource-group> --name <resource-name> --query "[].name" --output tsv
az role assignment list --assignee <user-object-id> --scope <azure-openai-resource-id> --include-inherited --query "[].roleDefinitionName" --output tsv
```

An authorized administrator can explicitly approve and apply this resource-scoped role assignment
if it is actually missing. **This changes access; it was not run:**

```powershell
az role assignment create --assignee-object-id <user-object-id> --assignee-principal-type User --role "Cognitive Services OpenAI User" --scope <azure-openai-resource-id>
```

Use the deployment's **name**, not just the model name. Set the resource's supported Azure OpenAI
base HTTPS endpoint, with no `/openai/v1`, project/deployment path or query. Example placeholder
format: `https://<resource-name>.openai.azure.com/`. Account networking must also allow this client.
API-key authentication is optional; without a key the client uses `DefaultAzureCredential`.
See Microsoft's [Azure OpenAI RBAC reference](https://learn.microsoft.com/en-us/azure/ai-foundry/openai/how-to/role-based-access-control?view=foundry-classic).

For the already configured, path-bearing endpoint, this exact normalization was executed
successfully in a child process (it does not itself make a model request):

```powershell
$configured = [uri]$env:AZURE_OPENAI_ENDPOINT
$env:AZURE_OPENAI_ENDPOINT = $configured.GetLeftPart([System.UriPartial]::Authority) + '/'
```

Run the README's explicit-live laptop command next. It is expected to return nonzero without a
guest; inspect each mode independently. HTTP 401 suggests identity/key authentication; 403 suggests
resource-scoped RBAC or network policy; 404 suggests endpoint/deployment routing. Do not infer a
missing role from the observed 404. Analytical Classic completion remains a separate next step,
not a reason to provision a VM or change the experiment's prompt without review.

### Scoped Classic API reproduction

The concrete loopback run succeeded; the placeholder-address templates below are **not yet verified
literally**. Replace `<loopback-http-url>` with your chosen loopback HTTP address and use the same
address in both terminals. After the Azure setup above, from the repository root in terminal one:

```powershell
$env:DATASET_ROOT = Join-Path (Get-Location) 'data'
$env:ARTIFACTS_ROOT = Join-Path (Get-Location) 'artifacts\live-classic-scoped'
$env:BENCHMARK_MAX_TURNS = '20'
$env:BENCHMARK_TIMEOUT_SECONDS = '60'
$env:EXECUTION_PAYLOAD_CAPTURE = 'full'
dotnet run -c Release --no-build --no-launch-profile --project src\SterlingVale.Classic.Api -- --model live --urls <loopback-http-url>
```

In terminal two, from the repository root:

```powershell
$base = '<loopback-http-url>'
Invoke-RestMethod -Uri ($base + '/health/ready') -TimeoutSec 10
$households = Get-Content -LiteralPath 'data\small\households.json' -Raw | ConvertFrom-Json
$body = @{ datasetProfile = 'small'; householdIds = @(@($households)[0].id) } | ConvertTo-Json
Invoke-RestMethod -Method Post -Uri ($base + '/api/v1/analyses') -ContentType 'application/json' -Body $body -TimeoutSec 90
```

Stop the server after the check. A Completed response establishes non-empty schema-valid analysis,
not independent oracle accuracy; inspect persisted metrics and source evidence separately.

## Scoped live ARM64 follow-up

November 11, 2026, 23:51-23:53 UTC. Live Azure model; Windows ARM64; no Hyperlight guest;
CodeAct **not executed**. No prompt or model-request tuning was changed.

### Original smoke artifacts: evidence cannot be recovered retroactively

The user-reported Classic run's `classic/final-response.json` contains only an envelope:
content null, original length 81 characters, capture mode metadata. Its last model-turn file records
**34 output tokens** but neither finish reason nor text. It is not possible to determine whether
that original message was prose, partial/empty JSON, length-truncated output or refusal. Neither
the length nor low token count proves a stop reason. We did not reconstruct or invent the message.

The user-reported CodeAct run has `generated-code-001.capture.json`, no `.py` file. Its event says
metadata capture with original source length **5,521 characters** and null content, not a redaction
or size-limit failure. The source cannot be restored from that marker. Old runs are not rewritten.
New live runs save safe bounded source independently of the general payload-capture mode; exact
source remains unavailable when redacted or oversized. The offline fake control preserves its
previous metadata/none suppression behavior.

### Commands and measured results

The README's two explicit-live commands were executed with `--households 1` and `--households 3`,
one measured pair after one warm-up per mode, batching disabled, max turns 20 and per-run timeout
120 seconds. Both comparisons returned exit 1 because CodeAct was intentionally not executed;
neither is a valid performance comparison. Warm-ups remain indexed but are not included below.
All token values are real per-response provider usage; durations are individual run wall times,
not aggregate medians or sandbox timing.

| Scope N | Mode | Status | Finish reason | Model turns | Input tokens | Output tokens | Duration ms | Oracle | Saved Python bytes |
| ---: | --- | --- | --- | ---: | ---: | ---: | ---: | --- | ---: |
| 1 | Classic | Completed | stop | 6 | 20,766 | 920 | 13,933.371 | Partial | 0 |
| 1 | CodeAct | NotExecuted | tool_calls | 1 | 1,911 | 78 | 2,262.295 | Not scoreable: no execution | 139 |
| 3 | Classic | Completed | stop | 5 | 35,776 | 961 | 12,052.803 | Partial | 0 |
| 3 | CodeAct | NotExecuted | tool_calls | 1 | 1,919 | 1,208 | 7,320.979 | Not scoreable: no execution | 4,614 |

Final-response output tokens (distinct from total output across all turns): Classic N=1 **471**,
Classic N=3 **232**. Both reports parsed and contained the requested household count, but neither
was correct. N=1 breach precision/recall were zero and allocation MAE was 0.0860313060.
N=3 flagged/breach recall were zero; numeric MAE zero does not establish correctness when risk
sets are wrong. `Completed` establishes schema/non-empty output, not oracle success.

| Scope N | Mode | list_households | get_policy | list_accounts | list_positions | get_price | get_fx | asset_class | execute_code |
| ---: | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 1 | Classic | 1 | 1 | 1 | 3 | 13 | 3 | 0 | 0 |
| 3 | Classic | 0 | 3 | 3 | 11 | 17 | 3 | 0 | 0 |
| 1 | CodeAct | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 1 |
| 3 | CodeAct | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 1 |

Classic counts are model-facing requests by name, not unique securities/accounts; CodeAct
granular callbacks are all zero because Python did not execute. `asset_class=0` is not a
classification shortcut: positions already carry asset class. The earlier full-Small run made no
price calls; scoped price calls can cover multiple positions sharing a symbol.

New actual Classic preview excerpts:

```json
{"runId":"exposure-rebalance-2026-01-01","generatedAt":"2026-01-01T00:00:00.000Z","households":[{"householdId":"H0001","baseCurrency":"USD","totalValue":206,"flagged":true}]}
```

```json
{"runId":"scenario-ExposureAndRebalance-small-H0001-H0002-H0003","households":[{"householdId":"H0001","baseCurrency":"USD","totalValue":0}]}
```

These are shortened excerpts with omitted fields/households, **not the original smoke run's message**,
not complete report reproductions and not correct calculations. Full captures, exact source,
last-response usage, safe previews and tool counts remain in the ignored run artifacts.

Evidence roots: `artifacts/live-scoped-n1/` and `artifacts/live-scoped-n3/`, each with `index.md`
and linked comparisons/run summaries/timelines/source. Both measured `.py` files were checked
on disk; emitted byte counts matched file length. Source presence proves capture, not runnable
or correct Python. In particular, the N=1 generated program is only 139 bytes.

### Prompt decision: unchanged

No prompt changes were made to make Classic finish. A possible future **shared** clarification,
requiring approval, is: "Apply the task only to the explicitly requested household IDs. Before
returning a report, verify that every position in that scope has price, required FX and classification
evidence; do not emit placeholder or zero-valued reports when evidence is incomplete."
It would apply to both modes' shared instructions, not just Classic. The shared body refers to
households "in scope", while the Classic addendum says "EVERY household" and CodeAct's protocol
slices the whole household list. A shared scope/evidence reminder could remove that tension, but
would change the experimental prompt fingerprint and is **proposed only, not implemented**.
The new N=1/N=3 outputs also show that merely finishing normally is not the core correctness fix.

### Regression scope

Tests cover stop/length/content-filter finish-reason preservation and redacted previews in failed-run
events, zero-inclusive tool counts, metadata/none/full live source capture with actual byte lengths,
redaction/oversize safety, endpoint normalization and its exact one-line notice, and scoped
first-N oracle/report agreement for both modes. Invalid/oversized household counts are rejected.
The expected non-live count is now **220**, with historical counts above retained as dated evidence.
Final fresh TRX recount: **220 passed, 0 failed, 0 skipped across ten projects**. One earlier full
attempt exposed an existing parallel-listener race in `TelemetryRedactionTests` (a shared List
changed during enumeration); the isolated test and unchanged full-suite rerun passed. That
unrelated test was not edited. Scoped fake CLI also returned Exact for both modes and rejected
zero, negative, non-numeric and beyond-dataset household counts. Prompts are unchanged.

## Token comparability and telemetry test follow-up

The branch through `dbb2c36` was pushed with approval; `git ls-remote` matched the local full SHA.
Subsequent token-label/test changes are a separate local commit, not part of that push.

Generation-only CodeAct counts now carry the explicit label **"first turn only, code not executed:
not comparable to Classic totals"** in the README and HTML report (also console/Markdown/scaling).
The N=1 and N=3 HTML reports were regenerated from existing captures, without new live model calls.
The numerical evidence above is unchanged; neither token count represents completed CodeAct analysis.

The 139-byte N=1 program is exactly:

```python
import json
hh = call_tool("list_households")
batch = [h for h in hh if h["id"] == "H0001"]
print(json.dumps(batch, separators=(",", ":")))
```

It retrieves/filter-prints household metadata, not portfolio analysis. This is consistent with an
exploratory first step that would need execution output and additional model turns to continue;
the model's next action is unverified. Generation-only termination intentionally supplies neither.
The 4,614-byte N=3 program begins full analysis over the requested households. Neither was executed.
The shared-prompt proposal remains verbatim above and **unapplied**.

The contained race fix is in
`TelemetryRedactionTests.Tool_metrics_and_activity_expose_only_whitelisted_tags`: callback sinks
now use concurrent queues and stable snapshots, and the activity listener only collects this
test's trace/direct child activities. The metric listener was already restricted to its own
meter. The test deliberately runs 32 additional concurrent tool calls to exercise collector
safety while preserving redaction/allowed-tag assertions. No production telemetry behavior changed.
README Next steps names the fixed test and retains monitoring/parallel stress as follow-up.
The expected non-live suite count is **221**, including one new token-labelling regression.
Validation: Release build zero warnings/errors; ten fresh TRX files confirm **221 passed,
0 failed, 0 skipped**. Three additional complete tools-suite runs passed (40 tests each),
including the concurrent collector stress. Both existing live HTML reports were regenerated
and checked for the exact comparability label; no new model calls were made. Prompts remain unchanged.

## Approved shared prompt v2 live follow-up

November 12, 2026. This section supersedes the earlier **unapplied** proposal's status, not its
historical results. Commit `b4a0762` was pushed first and verified against origin.
Only `Prompt.Shared` changed task instructions; both mode addenda remain unchanged.
The approved addition is:

> Apply the task only to the explicitly requested household IDs. Before returning a report,
> verify that every position in that scope has price, required FX and asset class (from position data or asset_class).
> If evidence is incomplete, do not invent values. Return the report with status 'incomplete' and list the missing items (e.g. symbols without a price).

The embedded schema is now `exposure-report/v2`. It requires `status` and `missingItems`;
incomplete needs a non-empty list, complete an empty list. An incomplete report may have no
households rather than fabricate numeric placeholders. The runner retains it with execution
status `Incomplete`, mappings retain missing items, and scoring always yields Partial even if
all supplied values otherwise exactly match the oracle. Batch aggregation preserves this state.
The schema is not relaxed to accept invented trade directions or repair model answers.

| Shared prompt boundary | SHA-256 |
| --- | --- |
| Before (v1, previous scoped experiments) | `d6dba82ae649a3ca253a9f3e61bf70392fbae04d54d3b81daa4f6a7a3ad8b596` |
| After (v2, these experiments) | `630a3a841752d3b4475fadde5050b2884c11b59b28f87e4c43a694fc57ebbc86` |

The hash covers Shared **including the schema**. Comparisons/run summaries record the boundary.
Scaling selects only the latest prompt cohort and refuses mixed or unknown hashes; historical
reports stay readable but cannot be aggregated with this revision. No before/after speedup is claimed.

### Measured live ARM64 results

Same limits: Small, one measured trial per mode after one clearly labelled warm-up per mode,
20 turns, 120 seconds, temperature zero, Classic batching zero, full payload capture, no guest.
Fresh roots: `artifacts/live-prompt-v2-n1/` and `artifacts/live-prompt-v2-n3/`.
Both commands exited 1 because both comparisons are invalid; telemetry/fairness matched.
CodeAct tokens are **first turn only, code not executed: not comparable to Classic totals**.
No model/deployment, pricing or mode-specific prompt settings were tuned.

| N | Mode / run prefix | Status | Finish | Turns | Input / output tokens | Duration ms | Oracle | Python bytes |
| ---: | --- | --- | --- | ---: | ---: | ---: | --- | ---: |
| 1 | Classic `4cae5754` | Failed | stop | 6 | 21,079 / 1,082 | 16,003.170 | InvalidSchema | 0 |
| 1 | CodeAct `509ab37b` | NotExecuted | tool_calls | 1 | 2,176 / 1,258 | 6,619.094 | Not scoreable | 5,017 |
| 3 | Classic `bc40c18c` | Completed | stop | 5 | 37,101 / 974 | 14,763.335 | Partial | 0 |
| 3 | CodeAct `577ed589` | NotExecuted | tool_calls | 1 | 2,184 / 536 | 4,330.247 | Not scoreable | 1,616 |

| N | Mode | list_households | get_policy | list_accounts | list_positions | get_price | get_fx | asset_class | execute_code |
| ---: | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 1 | Classic | 0 | 1 | 1 | 3 | 13 | 3 | 0 | 0 |
| 1 | CodeAct | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 1 |
| 3 | Classic | 0 | 3 | 3 | 11 | 17 | 3 | 0 | 0 |
| 3 | CodeAct | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 2 |

| N | Positions in scope | Distinct symbols | Classic get_price calls | Positions with recorded successful prices | Classic declared incomplete? |
| ---: | ---: | ---: | ---: | ---: | --- |
| 1 | 26 | 13 | 13 | 26 | No: raw answer declared complete, missingItems=[] |
| 3 | 117 | 17 | 17 | 117 | No: raw answer declared complete, missingItems=[] |

N=1's final answer contains placeholder-looking values (`totalValue=1234567.89`) and a Crypto
trade with `direction="Hold"`, which the shared Buy/Sell schema rejects. It was not truncated:
finish stop, final response 660 output tokens. No repair or retry hid the failure.
N=3's final answer has three households with totalValue zero and empty drift/breach/trade arrays;
finish stop, final response 245 output tokens. Schema completion is not correctness.
The accepted-report status for N=1 is unavailable because validation rejected it; its raw
`classic/final-response.json` establishes the model's explicit complete claim.

Both scoped Classic runs fetched every distinct price needed by their positions. Price-call count
is not position coverage: repeated symbols reuse one quote. Asset class is already present in
position data, so not calling `asset_class` is not missing evidence. The earlier full-Small
smoke's gap was missing prices (zero calls); these new scoped runs demonstrate an additional
arithmetic/report-integrity gap despite full price coverage. The approved prompt did not fix it.

CodeAct source files were checked on disk: N=1 has `generated-code-001.py` (5,017 bytes);
N=3 has `generated-code-001.py` (159 bytes) and `generated-code-002.py` (1,457 bytes).
The two N=3 calls belong to the same first model response, not two model turns.
No code ran, no portfolio callbacks occurred, and these are not CodeAct correctness/performance results.
Both roots contain index, comparison JSON/Markdown/CSV/HTML, timelines and model/tool artifacts.

Validation: ten fresh TRX files confirm **228 passed, 0 failed, 0 skipped** (seven new cases:
incomplete mapping/empty output, three invalid completion contracts, incomplete-never-Exact,
mixed-prompt rejection, and report-boundary/missing-items rendering). Provisioning remains paused.
Final Release build: zero warnings/errors. An actual visualize check with old-prompt Medium
and new-prompt Small selected Small only, explicitly skipped Medium, and wrote the new hash to
scaling HTML. Both live HTML reports were checked for the hash, missing-items/price-scope evidence
and generation-only token warning. README/docs scan found no machine paths, resource IDs,
literal resource endpoints/IPs or internal-feed URLs. README is 400 lines.

## Structured output and generation-only count follow-up

**Historical staged state, superseded before commit:** the strict/serial settings described in
this section were replaced by parallel/non-strict settings in the correction below. No live
experiment used the strict/serial configuration.

November 12, 2026. No further live calls were made; laptop iteration stops after this change.
The prompt text and embedded canonical v2 schema are unchanged, retaining shared hash
`630a3a841752d3b4475fadde5050b2884c11b59b28f87e4c43a694fc57ebbc86`.
The live tables above used the older plain-JSON request settings, not the new strict settings.

The installed Microsoft.Extensions.AI OpenAI adapter supports `ChatResponseFormat.ForJsonSchema`
and maps `AdditionalProperties["strict"]=true` into the native request. Both factories now use
the same option builder, including CodeAct with/without a guest. Requests specify strict schema,
disable parallel tool calls, and use the same 16,384-token output cap. The generation-only path
previously omitted that explicit output cap; it now matches the other paths. These settings and
the projected schema hash are recorded in metadata/configuration events and the model fingerprint.
Scaling selects and validates both prompt and model-configuration cohorts, preventing comparison
of these new settings with historical plain-JSON runs.

Azure structured outputs support only a JSON Schema subset: the canonical v2 conditional
`allOf`/`if`/`then`/`else` and string `minLength` cannot be enforced server-side. The derived
transport schema retains v2's shape, required fields, closed objects, references and enums,
including Buy/Sell rather than Hold. Full unchanged v2 validation still runs locally and rejects
inconsistent missing-items lists or empty required strings. Both schema hashes are retained;
there is no silent fallback if a deployment rejects structured output. This change is
**not live-verified**. See [Azure limitations](https://learn.microsoft.com/en-us/azure/ai-foundry/openai/how-to/structured-outputs#json-schema-support-and-limitations).

The N=3 CodeAct journal for run `577ed589` contains one MODEL_RESPONSE_RECEIVED with **two distinct
function-call IDs**, both named execute_code and both at modelTurn=1. They map to the 159-byte
and 1,457-byte saved source files. Each gets one MODEL_TOOL_CALL_STARTED, one CODE_GENERATED
and one CODE_EXECUTION_NOT_EXECUTED; there are zero CODE_EXECUTION_STARTED events.
This is two requested calls, not double-counting, two executed programs, or two model turns.
Generation-only capture deliberately retains the whole returned batch and stops after its last
function; it neither runs Python nor supplies results for another model turn.
The existing three capture-mode regression cases now assert one model request/turn, two distinct
call IDs, two execute-code requests, zero actual tool invocations and two explicit NotExecuted markers.

README now records: Classic retrieved complete price evidence for all scoped positions but
produced zero/placeholder values while declaring complete.
**Hypothesis (unverified until CodeAct executes):** in-context arithmetic across many positions
is unreliable. This is not a demonstrated advantage for CodeAct; its arithmetic remains untested.

New tests capture actual options from both factories and verify persisted settings. A local
fake HTTP handler checks the real OpenAI adapter's strict JSON-schema request, parallel-call flag,
v2 schema equivalence and rejection of Hold; full local validation still rejects conditional
violations that the server subset cannot express. A scaling regression also rejects mixed
model-configuration fingerprints. No guest or VM provisioning was attempted.
Final Release build: zero warnings/errors; ten fresh literal-path TRX reads confirm
**231 passed, 0 failed, 0 skipped**. No live calls, commits or pushes were made in this follow-up.

## Parallel tool-call correction

November 12, 2026. The prior parallel-off choice was based on the
[Azure structured outputs, Function calling section](https://learn.microsoft.com/en-us/azure/ai-foundry/openai/how-to/structured-outputs#function-calling-with-structured-outputs):

> Structured outputs are not supported with parallel function calls. When using structured outputs set `parallel_tool_calls` to `false`.

That note addresses strict structured function calling, not a general requirement of tool-calling
agents. The SDK's global `strict` option also applies to function schemas; enabling it for the
response had enabled strict tools too. Preserving realistic parallel batching is more important
for this comparison than strict server-side output constraints. The SDK does not itself require
serial calls: a fake-HTTP test verifies its outgoing request with parallel on and strict off.

Both modes now explicitly send `parallel_tool_calls=true`, `response_format=json_schema` and
`strict=false` for the response and tools. The v2 schema is non-strict guidance; the unchanged
full local validator, not a server guarantee, decides acceptance. The prompt and canonical schema
texts are unchanged. Metadata/configuration events record the native-named parallel/strict fields,
the transport schema hash and output cap; fingerprints include these settings. Scaling rejects
mixed model configurations. No silent downgrade or retry is used if a deployment rejects the format.

Benchmark and both API startup paths print:

```text
Model output: response_format=json_schema; parallel_tool_calls=true; strict=false; validation=local exposure-report/v2
```

README includes the requested note: Classic runs with parallel tool calls enabled (model default);
disabling it would multiply Classic's turns and isn't how the pattern is used in practice.
Earlier live evidence and the unverified arithmetic hypothesis remain unchanged.

Validation: clean Release build; **231 passed, 0 failed, 0 skipped**, counted from ten fresh TRX
files. A fresh fake-only N=1 control completed with both modes Exact and confirmed the startup
line, persisted `parallel_tool_calls=true` / `strict=false`, and matching model fingerprints.
This only verifies wiring and accounting, not live model behavior or performance.
No new live calls or VM/provisioning actions were performed.
