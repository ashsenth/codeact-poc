# Infrastructure gate: request limits and sandbox output capacity

November 17, 2026. **No live model runs. No prompt, schema, oracle, scorer, tool,
dataset or dependency changes.** Historical trial data and generated summaries remain
unchanged. Medium/Large sandbox controls were **not run**, because the pinned SDK
does not expose the requested output-buffer configuration.

## 1. In-flight model-request limit

`AnalysisService` now passes the configured `ModelConfiguration.MaxTurns` (including
`BENCHMARK_MAX_TURNS` in benchmark workers) into the shared `ObservedChatClient`.
An atomic counter reserves each request **before** calling the underlying client.
Both modes, warm-ups and Classic batch workers use this same service path.

For budget N, requests 1 through N are permitted. A final answer on request N is
accepted. If the function-invocation loop asks for N+1, the client emits
`TURN_LIMIT_REACHED` and throws `TurnLimitReachedException` **without forwarding the
request**. The runner ends immediately as existing status **Capped**, with failure
category **TurnLimitReached**. This is a model outcome, not infrastructure. Keeping
the existing Capped wire status avoids a schema or scorer change.

The blocked attempt is not a `MODEL_REQUEST_STARTED`/`MODEL_REQUEST_FAILED` pair:
it made no model call. Recorded request/response/usage totals therefore remain the
actual dispatched requests, and telemetry reconciliation still passes. Ordinary
provider/transport failures retain infrastructure precedence. A request can contain
multiple tool calls: this is a request budget, not a granular callback budget.
Provider-internal HTTP retries remain inside one observed request. The 120-second
worker-tree deadline is unchanged and remains necessary for uninterruptible native code.

The limit also applies without an ambient telemetry session and to the streaming
entry point without buffering or replacing the underlying stream. Each analysis
creates a fresh client/counter. The previous post-return cap remains a defensive
fallback for independently constructed agents, not benchmark enforcement.

The execution's serialized model configuration and model fingerprint now include
`turnLimitEnforcement = "model-request-boundary/v1"`. This intentionally distinguishes
future enforced runs from historical post-return-only runs with the same numeric
budget. Old metadata/fingerprints are not rewritten. Future results must not be mixed
into the old resource-efficiency cohort.

Regression tests execute the real shared service/factories offline with budget **20**:
Classic and CodeAct, measured and warm-up, each stop before request 21 and retain
valid telemetry/model-outcome classification. Separate both-mode tests accept the
final answer on request 20. Two additional cases test non-streaming/streaming
enforcement without telemetry.

## 2. Read-only historical audit

Source: raw `MODEL_REQUEST_STARTED` events, **not** reported assistant-turn counts
or inferred tool executions. Threshold is strictly **greater than 20**, not >=20.
Only recorded live-model/Hyperlight comparisons are included; reclassified duplicates
are excluded and run IDs are deduplicated. Offline controls/sanitized samples are not
live trials. Warm-ups, including the two archived v3 zero-trial attempts, are separate.

| Revision | Mode | Measured trials exceeding 20 / available | Maximum measured requests | Warm-ups exceeding 20 / available |
|---|---|---:|---:|---:|
| v1 | Classic | Unavailable: no v1 raw live trials present | Unavailable | Unavailable |
| v1 | CodeAct | Unavailable: no v1 raw live trials present | Unavailable | Unavailable |
| v2 | Classic | 0/10 | 8 | 0/4 |
| v2 | CodeAct | 0/10 | 5 | 0/4 |
| v3 | Classic | 4/15 | 41 | 1/5 |
| v3 | CodeAct | 1/15 | 38 | 0/5 |

v2 consists of the nine-pair published N=1/N=3/full Small sweep plus the earlier
single N=1 pair in `artifacts/phase-b-20261116-1721/live-n1/`. Both subsets have zero
over-budget trials. v3 consists of the 15 measured pairs in the three completed
profiles. Its five warm-ups per mode include three completed-profile warm-ups and
two aborted-attempt warm-ups. No measured trial was discarded for this audit.
**Unavailable v1 is not a finding of zero violations**: no v1 raw live artifacts are
available in this workspace, so an all-time v1 count cannot be reconstructed here.

### Affected measured v3 trials (annotations only)

| Scope / pair | Mode | Requests | Historical outcome | Raw evidence |
|---|---|---:|---|---|
| N=1 / 5 | Classic | 41 | Capped | [14f4a06e timeline](../artifacts/v3-sweep-20261116/n1/runs/14f4a06e5006471bb7b0a625c40638e0/timeline.md) |
| N=3 / 2 | Classic | 41 | Capped | [643b4e45 timeline](../artifacts/v3-sweep-20261116/n3/runs/643b4e4548ca4814b3e5d68bc705b546/timeline.md) |
| N=3 / 3 | Classic | 41 | Capped | [821bb243 timeline](../artifacts/v3-sweep-20261116/n3/runs/821bb243f086471b99137ca33f53d970/timeline.md) |
| N=3 / 5 | Classic | 21 | Capped | [6683b8b1 timeline](../artifacts/v3-sweep-20261116/n3/runs/6683b8b1130a44e0974824755c41152d/timeline.md) |
| Full Small / 4 | CodeAct | 38 | TimedOut | [9688b2aa timeline](../artifacts/v3-sweep-20261116/full-small/runs/9688b2aad48145a2909f1b4f6c154c31/timeline.md) |

The additional over-budget Classic N=1 warm-up is
[`a606d87b9b5a4ba89ff931d02bdcec0a`](../artifacts/v3-sweep-20261116/n1/runs/a606d87b9b5a4ba89ff931d02bdcec0a/timeline.md):
41 requests. It is not included in the measured denominator or medians.

These are historical **post-return-limit** results, not evidence that a 20-request
budget was enforced. Their recorded statuses, scores, tokens, cost and timing remain
unchanged, as do the existing summary files. In particular, do not interpret their
resource deltas as a fair enforced-20-request comparison, or invent counterfactual
scores/resources from a hypothetical stop at request 20. No rerun or rescore occurred.

[Audit JSON with every run/count/source](../artifacts/turn-limit-audit-20261117.json).
Reproduction:

```bash
python3 scripts/vm/audit-model-turns.py --artifacts-root <existing-artifacts-root> \
  --threshold 20 > <new-audit.json>
```

## 3. Exact offline overflow behavior

The added supported-Hyperlight regression runs `print('x' * 32768)` through the
normal CodeAct factory, provider and observed function client, driven only by a
deterministic scripted client. The intended stdout is **32,769 bytes**, including
the newline. The script's next scheduled response inspects the actual tool result.

Observed result delivered to that client:

```json
{
  "stdout": "",
  "stderr": "... Guest aborted: 8 ... Failed to serialize function call result ... Not enough space in shared output buffer. Required: 32856, Available: 16376 ...",
  "exit_code": -1,
  "success": false
}
```

- The **guest call aborts/panics** while serializing its result. The host process
  does **not** crash. The pinned provider catches the `Sandbox.Run` exception and
  returns the failure envelope to the model-facing conversation.
- This is **not silent or partial-output truncation**: stdout is empty and an
  explicit error is returned, including required/available buffer bytes.
- `CODE_EXECUTION_FAILED` is recorded; `MODEL_TOOL_CALL_COMPLETED` records the
  returned failure object. There is no thrown `MODEL_TOOL_CALL_FAILED` host exception.
  The scripted client receives the error and successfully returns its next scheduled
  final text (`overflow-observed`). This does not claim that a real model would recover
  or that a subsequent program in the same provider would succeed.
- Full capture records the small error envelope without truncation. Telemetry's
  separate 65,536-character payload limit does not cause this guest-buffer failure.

Detailed captured result: the TRX under
`artifacts/infrastructure-gate-20261117/overflow/` (test standard output).
The regression is included in the final supported runtime gate.

## 4. Medium/Large oracle report sizes and SDK decision

Sizes are actual UTF-8 bytes from the **unchanged** oracle and canonical
`ReportJson.Serialize`, all households, no BOM/newline, 32-character run ID and fixed
generatedAt `2026-01-01T00:00:00+00:00`. These include the full envelope and every
oracle field/detail. Indented sizes are also disclosed; they are not the sizing basis.

| Dataset | Households | Canonical compact report bytes | Indented bytes |
|---|---:|---:|---:|
| Small | 10 | 16,547 | 27,413 |
| Medium | 40 | **64,764** | 107,628 |
| Large | 120 | **196,685** | 326,747 |

The requested **2x Large** minimum would be **393,370 bytes**, before considering
tool-result transport overhead. The smaller prior successful Small control has
concise breach descriptions; it is not the canonical oracle serialization used here.
JSON-escaped host tool results are larger again. Large also exceeds the current
65,536-character telemetry capture cap; a future supported large-buffer design must
separately address exact source/result capture and serialization overhead.

### Pinned SDK inspection: no configurable shared output buffer

Verified by reflection of the actual installed assemblies, their XML and the
previously captured matching decompilation/source (no version upgrade):

- `Microsoft.Agents.AI.Hyperlight` **1.19.0-preview.260822.1** public
  `HyperlightCodeActProviderOptions` exposes `HeapSize`, `StackSize`, `Backend`,
  `ModulePath`, tools, mounts, allowed domains, approval mode and host input directory.
  There is **no** stdout/shared-output-buffer-size property.
- `Hyperlight.HyperlightSandbox.Api` **0.4.0** `SandboxBuilder` exposes heap/stack,
  module/backend and input/output-directory settings, not shared-output-buffer sizing.
  `WithOutputDir`/`WithTempOutput` configure filesystem mounts, **not** this buffer.
- Its PInvoke 0.4.0 `FFISandboxOptions` contains only `module_path`, `heap_size`,
  `stack_size`, `backend`. Increasing heap/stack is not a supported way to configure
  the shared output channel. The provider passes those same memory settings through
  its builder; it offers no lower-level output-buffer override.
- The underlying .NET package identifies source commit
  `3d8171e7a9e706da0f2a5837c0bcf2ed942643af`.
  Previously captured [SandboxBuilder source](../artifacts/phase-b-20261116-1721/sdk-inspection/codeact-pinned-sandbox-builder.cs)
  and [FFI options](../artifacts/phase-b-20261116-1721/sdk-inspection/codeact-pinned-sandbox-options.cs)
  corroborate the installed surface.

[Capacity JSON: exact sizes, public members, FFI fields and assembly SHA-256s](../artifacts/sandbox-capacity-20261117/capacity.json).
The inspected provider and API hashes match the earlier cancellation investigation:
`7f69060fb7678e57a2a25cbe55ad14f81c86066570500482215cb8366eaed363` and
`e173db75007b53718ced87fe0be94e1f8e56b1ef1a9e830426efce2c8e9938dd`.

Reproduce without a guest or model:

```bash
dotnet build scripts/vm/Diagnosis -c Release
dotnet scripts/vm/Diagnosis/bin/Release/net9.0/Diagnosis.dll sandbox-capacity \
  <existing-generated-data-root> <new-capacity-output-directory>
```

### Stop decision and options requiring approval

**No buffer setting changed, no buffer size was added to a fingerprint as though
configurable, and no Medium/Large control execution was attempted.** No new Exact
claim is made for those scopes. Their measured oracle sizes are not feasibility results.

1. **Preferred: supported SDK/native update or upstream change** exposing the shared
   output buffer end-to-end. Verify it against the actual pinned replacement, choose
   at least 393,370 bytes plus envelope headroom, record/version/fingerprint the setting,
   cover overflow and exact capture, then run Medium/Large controls within 120 seconds.
   This requires explicit dependency/guest-change approval; no compatible release is
   assumed or selected here.
2. **Approved protocol redesign for chunking/host-side assembly** with completeness
   and size validation. This is not a drop-in infrastructure setting: it changes output
   delivery/orchestration and requires explicit design/fairness approval. No prompt,
   schema, tool or analysis semantics have been changed to implement it.
3. **Keep the pinned stack and constrain scope/output size**, treating overflow as an
   explicit failure rather than accepting empty/truncated reports. This does not meet
   the full Medium/Large single-output requirement; don't start further live scaling.

## 5. Validation and preservation

- Release solution build: zero warnings/errors.
- **291 offline non-live tests passed**, zero failed/skipped: previous 283 plus six
  both-mode service budget/boundary cases and two no-session/streaming cases.
- **Six supported real-Hyperlight tests passed**, zero failed/skipped: previous five
  plus the overflow-return regression. Existing native-cancellation known issue is
  still excluded and is not counted as passed.
- TRX: `artifacts/infrastructure-gate-20261117/offline/` and
  `artifacts/infrastructure-gate-20261117/hyperlight/`.
- All **1,858** historical v2/v3 profile/summary/evidence files remain byte-identical
  against `artifacts/infrastructure-gate-20261117-inputs.sha256`.
- No new live runs, data edits, cohort regeneration, control/cohort mixing, or protected
  component changes. No Medium/Large sandbox control results are fabricated.
