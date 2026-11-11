# Execution Observability Remediation

Historical acceptance status: NOT COMPLETE / NOT DEMO-READY under the original full-live criteria.
The project is now scoped as a demoable POC: those criteria are future work, not an offline-demo
release gate. See the [current POC review](poc-demo-review.md) for fresh evidence and open issues.
Live correctness and performance have not been
recertified by this change. Historical artifacts are not acceptance evidence for the new pipeline.

## Confirmed findings and implementation

| Priority | Finding | Implementation |
| --- | --- | --- |
| P1 | Missing correlation | Immutable Telemetry.ExecutionContext, per-run async-local session, W3C Activity, paired comparison/trial IDs generated before measured execution, model HTTP headers and API response headers |
| P1 | Reconstructed events | Synchronously flushed append-only events emitted at actual run/model/tool boundaries; aggregate reconstruction removed |
| P1 | Ambiguous metrics | Model-facing calls, granular invocations, callbacks, execution counts, and per-response usage separated; reconciliation and CLI validity gate added |
| P1 | Missing generated-code traceability | Supported FunctionInvokingChatClient override captures each actual code argument separately before invocation; result/stream/failure records retained incrementally |
| P1 | Classic batch attribution | Separate aggregate ID, application-merged label, child-run references and independently persisted evidence; aggregate has no purported raw model answer |
| P1 | Lost failure evidence | Incremental model/tool artifacts survive errors; failed runner records retained; cancellation produces a failure marker and timeline before propagating |
| P1 | Unrestricted payloads | Metadata default, explicit full capture, size bounds, truncation metadata, nested-secret and credential-pattern redaction; artifact API follows policy |
| P1 | Insufficient terminal output | Same event stream rendered at minimal/normal/verbose/debug; ordered JSON/Markdown timelines |
| P1 | Weak Hyperlight testing | Added actual-guest Python/callback/stdout, exception/stderr, and cancellation tests; must pass on supported x64 compute |

## Relevant sources

- `src/SterlingVale.Telemetry/ExecutionSession.cs`: immutable correlation, event ordering, immediate persistence, timeline finalization.
- `src/SterlingVale.Telemetry/PayloadPolicy.cs`: capture controls, redaction and bounds.
- `src/SterlingVale.Telemetry/ExecutionConsole.cs`: event-based verbosity.
- `src/SterlingVale.AgentShared/Analysis/AnalysisService.cs`: run lifetime and failure finalization.
- `src/SterlingVale.AgentShared/Analysis/ObservedChatClient.cs`: actual request/response boundary, safe externally visible message projection, per-response usage.
- `src/SterlingVale.AgentShared/Analysis/ObservedFunctionClient.cs`: public tool middleware, individual code and execution artifacts.
- `src/SterlingVale.Tools/Observability/RecordingAIFunction.cs`: direct callbacks, actual arguments/responses, parent linkage and failures.
- `src/SterlingVale.AgentShared/Analysis/TelemetryReconciler.cs`: event/count/token checks.
- `src/SterlingVale.Benchmark/BenchmarkHarness.cs`: paired identity and batch attribution.
- `src/SterlingVale.Infrastructure/Runs/FileRunStore.cs`: atomic final files, no journal replacement.

## Limits and remaining acceptance work

The transport is direct AIFunction, not MCP. Both modes use the same seven canonical tools;
adding MCP symmetrically is optional future work, not a prerequisite for observing this architecture.

The pinned SDK publicly exposes DelegatingChatClient, FunctionInvokingChatClient.InvokeFunctionAsync,
and execute_code result fields. No SDK internals are modified. Internal Hyperlight initialization,
serialization, cleanup, CPU and memory measurements remain unavailable. stdout/stderr may be absent;
absence is not an empty successful result. Tool/model request durations are host-observed, not invented
internal timing. SDK-internal HTTP retries are not separately measured. Streaming execution is not
used by the runner and remains unverified. Redaction or truncation intentionally prevents an exact
original-code artifact; inspect capture metadata rather than treating a sanitized program as exact.

Source files and payload artifacts require the same access controls as other synthetic benchmark
data. Redaction is defense in depth, not a guarantee that arbitrary model text contains no secret.
No hidden reasoning-content objects are projected into model artifacts.

The current Windows host reports ARM64 and has no HYPERLIGHT_PYTHON_GUEST_PATH. Azure endpoint
and deployment variables are configured, but their values are not copied into this document.
No Azure authentication/quota success is implied by variable presence. Live CodeAct cannot be
certified here. Corrective action: use an x64 virtualization-capable host, the version-matched guest,
and authorized Azure access; run the documented gated tests before all three live comparisons.
No Azure compute is provisioned automatically by this remediation.

## Validation requirements

Offline tests cover concurrent run isolation/order, immediate persistence, trace linkage,
payload defaults/redaction/truncation, tool failures, individual code capture across later failures,
Classic turn capture, timelines, missing usage, reconciliation mismatches, all dataset sizes, and
batch child identity. Existing timeout/cancellation and fairness tests remain enabled. Six real-guest
tests skip on this host; skipped tests are not evidence of Hyperlight behavior.

Release builds and the broader non-live suite are required at every phase boundary. A fresh export
must restore in locked mode, build, regenerate datasets and run offline controls without local paths.
A clean remote clone and real x64 sandbox/model runs remain separate acceptance checks.

## Acceptance criteria

| # | Criterion | Current assessment |
| --- | --- | --- |
| 1 | Clone repository | Remote clean clone not certified |
| 2 | Follow README | Copy-paste offline and live prerequisites documented; fresh-export check required |
| 3 | Run Classic | Offline path tested; live acceptance pending |
| 4 | Run CodeAct | Offline path tested; real guest blocked on this host |
| 5 | Observe terminal execution | Implemented and offline tested; live verification pending |
| 6 | Inspect Python/JSON | Full capture implemented/tested; real-model capture pending |
| 7 | Inspect tool requests/responses | Implemented; explicit full mode required |
| 8 | Inspect MCP | Not applicable: direct callbacks |
| 9 | Correlate events | Implemented/tested locally; distributed live validation pending |
| 10 | Hyperlight logs | Supported-result capture implemented; six runtime tests skipped |
| 11 | Small/Medium/Large benchmarks | Offline controls tested; live comparisons pending |
| 12 | Both performance results | No new live performance claim |
| 13 | Oracle correctness | Offline control exactness tested; actual model correctness pending |
| 14 | Complete timeline | Ordered actual observed events implemented; unavailable internals disclosed |
| 15 | Reproduce workflow | Documentation updated; clean remote/x64 reproduction pending |

Do not mark ready until both real agents complete the unchanged workloads, reconcile telemetry,
and satisfy oracle correctness. A fake control must never be published as a live performance result.

## Validation record (2026-11-03)

- SDK observed: 9.0.320; Release build succeeded with warnings-as-errors unchanged.
- Non-live suite: 197 passed, 0 failed. Real Hyperlight group: 6 skipped, 0 executed.
- All original datasets regenerated independently: Small 10/33/370, Medium 40/156/2201,
	Large 120/485/7198 households/accounts/positions.
- One measured paired fake-control trial per profile completed, with one warmup per mode.
	All six measured outputs matched the oracle exactly; fairness matched and telemetry was valid.
- Control artifacts: `artifacts/observability-validation/control-20261103/` contains the datasets,
	comparison JSON/Markdown/CSV/HTML and per-run journals, timelines and payload artifacts.
- The validation helper is blocked here by PowerShell's script execution policy. The equivalent
	individual dotnet generation/benchmark commands were executed instead. No policy was changed.
- The attempted temporary local clone did not produce a valid Git checkout; fresh-clone validation
	is NOT established. No existing build output was described as clean-clone evidence.
- Actual Azure authentication, quota, live Classic correctness and live Hyperlight remain unverified.

### Offline control results, NOT live performance

Latency below is the actual measured harness duration including observation/persistence work.
Tokens come from the deterministic fake client, NOT Azure billing. Cost is a formula applied to
those fake token counts, NOT money spent. CodeAct executes no Python and makes no granular
callbacks in this control. Ratios demonstrate report plumbing only and prove no agent advantage.

| Dataset | Households | Accounts | Positions | Classic status | CodeAct status | Classic ms | CodeAct ms | Latency reduction % | Classic tokens | CodeAct tokens | Token reduction % | Classic turns | CodeAct turns | Classic granular calls | CodeAct granular calls | Classic estimated USD | CodeAct estimated USD | Correctness |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Small | 10 | 33 | 370 | Completed | Completed | 20.8503 | 20.4563 | 1.8897 | 4174 | 4174 | 0 | 2 | 2 | 1 | 0 | 0.041425 | 0.041425 | Both Exact, precision/recall 1, allocation/trade MAE 0 |
| Medium | 40 | 156 | 2201 | Completed | Completed | 67.1642 | 44.8586 | 33.2105 | 16230 | 16230 | 0 | 2 | 2 | 1 | 0 | 0.161970 | 0.161970 | Both Exact, precision/recall 1, allocation/trade MAE 0 |
| Large | 120 | 485 | 7198 | Completed | Completed | 144.5755 | 111.0355 | 23.1990 | 49208 | 49208 | 0 | 2 | 2 | 1 | 0 | 0.491765 | 0.491765 | Both Exact, precision/recall 1, allocation/trade MAE 0 |

Source comparison IDs, in order: `<SMALL_COMPARISON_ID>`, `<MEDIUM_COMPARISON_ID>`,
`<LARGE_COMPARISON_ID>` (sanitized; exact values remain in local ignored evidence).

Each measured fake mode has one model-facing invocation; CodeAct's invocation is execute_code.
Model-facing invocations must not be confused with the granular-call columns above.

### Live blocker and corrective action

| Dataset | Live Classic | Live CodeAct | Exact blocker | Corrective action |
| --- | --- | --- | --- | --- |
| Small | Not executed | Not executed | ARM64 Windows; no guest configured | x64 WHP/KVM/MSHV host and matching guest; authorized Azure access; run gated tests and paired workload |
| Medium | Not executed | Not executed | Same runtime prerequisite prevents fair live pair | Same action, unchanged 40-household workload |
| Large | Not executed | Not executed | Same runtime prerequisite prevents fair live pair | Same action, unchanged 120-household workload |

CPU utilization, memory utilization, live quota/rate limits, context exhaustion and timeout behavior
cannot be diagnosed as causes for runs that were not launched. Do not invent such measurements.

## Phase A recount (2026-11-05, OFFLINE-CONTROL)

The previously reported TRX aggregate of 297 was invalid. The original directory contains ten
distinct project results from one run, totaling 197; no extra or leftover TRX accounts for 100.
PowerShell wildcard interpretation of the five bracket-suffixed filenames caused read failures.
Because the original interactive aggregation continued after those errors and did not clear
`$result`, it counted the previous successfully parsed document again. This is stale-variable
reuse, not an increase in actual test executions. The helper now uses `-LiteralPath`, explicit
terminating reads and a cleared document variable.

### Original per-file evidence

Directory: `artifacts/phase-a-validation/non-live-tests/`. All rows are OFFLINE-CONTROL.
Names below share the sanitized prefix `<USER>_<HOST>_2026-11-03_`; append the shown suffix.
Only the user/host prefix is redacted; filename suffixes, timestamps and counters are unchanged.
The timestamp is each file's UTC last-write time. Total/executed/passed are equal in every row;
failed and notExecuted are individually zero in every row.

| Filename suffix | Modified UTC (2026-11-03) | Total | Executed | Passed | Failed | notExecuted | Erroneous count | Difference |
| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 15_08_39.trx | 20:08:39.7114016Z | 20 | 20 | 20 | 0 | 0 | 20 | 0 |
| 15_08_39[1].trx | 20:08:40.0718079Z | 27 | 27 | 27 | 0 | 0 | 20 | -7 |
| 15_08_40.trx | 20:08:40.1343697Z | 39 | 39 | 39 | 0 | 0 | 39 | 0 |
| 15_08_40[1].trx | 20:08:40.1428585Z | 11 | 11 | 11 | 0 | 0 | 39 | +28 |
| 15_08_40[2].trx | 20:08:40.3509705Z | 5 | 5 | 5 | 0 | 0 | 39 | +34 |
| 15_08_41.trx | 20:08:41.1632600Z | 39 | 39 | 39 | 0 | 0 | 39 | 0 |
| 15_08_41[1].trx | 20:08:41.2368429Z | 6 | 6 | 6 | 0 | 0 | 39 | +33 |
| 15_08_41[2].trx | 20:08:43.0356072Z | 27 | 27 | 27 | 0 | 0 | 39 | +12 |
| 15_08_42.trx | 20:08:42.8750661Z | 7 | 7 | 7 | 0 | 0 | 7 | 0 |
| 15_08_44.trx | 20:08:44.3920906Z | 16 | 16 | 16 | 0 | 0 | 16 | 0 |
| Total | | 197 | 197 | 197 | 0 | 0 | 297 | +100 |

### Fresh deterministic rerun

Run directory: `artifacts/phase-a-validation/<VALIDATION_RUN_ID>/`.
Release build succeeded. The unchanged `Category!=LiveModel&Category!=Hyperlight` filter ran
explicitly against all ten test projects. Only these ten new TRX files were aggregated using
`Get-Content -LiteralPath ... -Raw -ErrorAction Stop`. No old results were included.
Each filename is `SterlingVale.` plus the project suffix below plus `.trx`.

| Project suffix | Modified UTC (2026-11-05) | Total | Executed | Passed | Failed | notExecuted |
| --- | --- | ---: | ---: | ---: | ---: | ---: |
| AgentShared.Tests | 21:04:03.1741402Z | 39 | 39 | 39 | 0 | 0 |
| Application.Tests | 21:04:04.3985261Z | 27 | 27 | 27 | 0 | 0 |
| Architecture.Tests | 21:04:05.6702367Z | 6 | 6 | 6 | 0 | 0 |
| Classic.IntegrationTests | 21:04:07.4371343Z | 16 | 16 | 16 | 0 | 0 |
| CodeAct.IntegrationTests | 21:04:09.2462274Z | 7 | 7 | 7 | 0 | 0 |
| Comparison.Tests | 21:04:12.2337720Z | 27 | 27 | 27 | 0 | 0 |
| Contract.Tests | 21:04:13.4243440Z | 5 | 5 | 5 | 0 | 0 |
| Data.Tests | 21:04:14.6316730Z | 11 | 11 | 11 | 0 | 0 |
| Domain.Tests | 21:04:15.6640145Z | 20 | 20 | 20 | 0 | 0 |
| Tools.Tests | 21:04:16.8154573Z | 39 | 39 | 39 | 0 | 0 |
| Total | | 197 | 197 | 197 | 0 | 0 |

Every helper test gate now creates its own UTC-timestamp/GUID directory, even when OutputRoot
is reused. It invokes explicit projects with a named TRX per project, checks file count equals
project count, validates every required counter, prints/persists per-file counts, and rejects
any failed or notExecuted tests. Non-live still requires 197 actual passes; Hyperlight still
requires all six actual passes in its runtime project. No existing test/filter/reconciliation
gate was weakened. Full script execution on pwsh/Linux remains unverified on this laptop;
syntax and equivalent individual commands were checked without changing execution policy.

SKIPPED / not executed in this recount: real Hyperlight, live model calls/comparisons,
provisioning, bootstrap and clean-clone reproduction. Readiness remains NOT COMPLETE.

### Provisioning checks (read-only, not LIVE execution)

Checked 2026-11-05. Neither provisioning nor bootstrap was executed, including their plan modes.

- The [current Microsoft Learn CLI quickstart](https://learn.microsoft.com/en-us/azure/virtual-machines/linux/quick-create-cli)
	still demonstrates Ubuntu 22.04; it is not evidence for a 24.04 image spelling. A direct, read-only
	catalog lookup of the exact URN resolved `Canonical:ubuntu-24_04-lts:server:latest` to
	`name=24.04.202610210`, `architecture=x64`, `hyperVGeneration=V2`. The underscores are correct;
	no image spelling change was made. Prefix-matched `image list` results included daily/Gen1 images,
	so the verification used exact `image show` instead of accepting the last prefix match.
- The westus2 SKU catalog returns `name=Standard_D4s_v5`, architecture `["x64"]`, generations
	`["V1,V2"]`, and no `NestedVirtualization` field (filtered values `[]`). The original expression
	`[?name=='Standard_D4s_v5'].capabilities[?name=='NestedVirtualization'].value | [] | [0]`
	also returns null on a local positive fixture with the capability present. The flattened expression
	`[?name=='Standard_D4s_v5'].capabilities[] | [?name=='NestedVirtualization'].value | [0]`
	returns the entire `True` string, not its first character. The earlier first-character claim
	was incorrect. Local JMESPath assertions verified present and absent cases.
- The script now selects the exact SKU object and uses structured `jq` capability extraction,
	requires x64/V2, and rejects explicit negative values. For an absent nested field, only the
	documented `Standard_D4s_v5` default is accepted. The
	[Microsoft Learn Dsv5 feature table](https://learn.microsoft.com/en-us/azure/virtual-machines/sizes/general-purpose/dsv5-series)
	explicitly lists nested virtualization as Supported. Other sizes without an explicit positive
	capability remain blocked pending review. KVM permissions and six actual runtime passes are
	still mandatory; catalog metadata is not proof of actual sandbox execution.

Read-only verification commands (no create/update/delete commands were executed):

```bash
az vm image show --location westus2 \
	--urn Canonical:ubuntu-24_04-lts:server:latest \
	--query '{architecture:architecture,hyperVGeneration:hyperVGeneration,name:name}' -o json
az vm list-skus --location westus2 --size Standard_D4s_v5 --all \
	--query "[?name=='Standard_D4s_v5'].{name:name,nestedQuery:capabilities[?name=='NestedVirtualization'].value,architecture:capabilities[?name=='CpuArchitectureType'].value,hyperV:capabilities[?name=='HyperVGenerations'].value}" -o json
```

Bash syntax checking passed without executing the provisioning script. A temporary VS Code task
request was refused because the tool cannot target the nested repository workspace; no task file
was created in the parent folder. A PowerShell quoting error in the first local query probe was
corrected in the command, not in application code. Slow duplicate catalog reads were canceled;
only the completed exact-image and SKU results above are used as evidence. Execution policy,
global git configuration and Azure resources were not changed.