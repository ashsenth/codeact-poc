# Optional x64 validation workflow

**Historical validation ledger.** For current commands, provisioning, corporate VPN
and dual CorpNetPublic NSGs, feature registration, deploy keys and VM lifecycle use
the [README runbook](../../README.md#ubuntu-x64-vm-and-real-hyperlight).
The current supported runtime selection has six tests; the native-cancellation
known issue remains excluded, not passed. Earlier five-pass counts below are dated.
Validity is defined by the [canonical methodology](../benchmark-methodology.md#validity-rules),
not by historical classifications in this ledger.

## Request-budget and sandbox-capacity gate (November 17, 2026)

The [infrastructure report](../infrastructure-gate-20261117.md) records the shared
in-flight request limit, read-only historical audit, real-guest overflow regression,
Medium/Large oracle byte sizes, and the pinned SDK's absent buffer-setting API.
Future runs stop before request 21 with budget 20 in both modes, measured/warm-ups;
status Capped / category TurnLimitReached is a model outcome, not infrastructure.
The enforcement version is in execution configuration and model fingerprint.

Historical measured over-20 counts: v2 0/10 Classic and 0/10 CodeAct; v3 4/15 Classic
and 1/15 CodeAct. v1 raw live artifacts are unavailable, not assumed to have zero
violations. No raw trial, score, summary or prompt was edited or rerun.

Release build: zero warnings/errors. **291 offline tests and six supported real
Hyperlight tests passed**, zero failed/skipped; native-cancellation known issue remains
excluded. Medium/Large canonical report sizes: 64,764 / 196,685 UTF-8 bytes.
The provider returns a guest buffer-abort error to the client without killing the host.
No supported shared-output-buffer setting exists in the pinned stack, so **no
Medium/Large controls or live model runs were started**. See the report for the
explicit stop decision and approval-dependent options.

## No-LLM v3 diagnostic gate (November 16, 2026)

The [isolated feasibility control and complete failure-forensics report](../v3-diagnostics.md)
records Exact results for N=1, N=3 and full Small through the shared real Hyperlight
worker/recording path and 120-second limit, without any LLM calls.
Control artifacts and both failed prototypes are outside model cohorts/summaries.
All 13 non-correct measured CodeAct trials have per-trial execution/error/truncation,
final-value provenance and first-missing-component findings.

Release build: zero warnings/errors. **283 offline non-live tests plus five supported
real-Hyperlight tests passed**, zero failed/skipped in the final selections.
The existing native-cancellation known issue remains explicitly excluded.
The real-guest tests need an absolute guest path because testhost changes its cwd.
See the report for the preserved initial path-check failures, module/output-buffer
constraints, source/input manifests, exact output sizes and reproduction commands.
No prompt, schema, scorer/oracle, tools, datasets, model runs or model results changed.

## v3 full Small five-pair result (November 16, 2026)

After N=3 was checked, committed and pushed at
`2de44a231febc712004166f4159f959292ed5312`, the committed runner launched full
Small (10 households) under nohup from that source. Comparison
`small-20261116214010-6960fd4f41e645af8be1874d26eb63be` is VALID:
five measured pairs, one warm-up per mode, all telemetry valid, no infrastructure
failures, approved configuration/pricing and fairness fingerprints unchanged.
The committed checker verified every raw tool-error count and regenerated the
final three-profile v3 summary, excluding both aborted directories and all v2 data.

| Mode | Correct | Unearned (assessed) | Tool argument errors | Median tokens | Median est. USD | Median analysis s |
|---|---|---|---:|---:|---:|---:|
| Classic | 0/5 | 5/5 (5/5) | 0 | 71,677 | 0.1943500 | 21.851 |
| CodeAct | 0/5 | 4/5 (4/5) | 0 | 51,643 | 0.1860475 | 32.905 |

Classic: UnearnedCompleteness 5. CodeAct: UnearnedCompleteness 4, TimedOut 1.
The deadline timeout is a recorded model task outcome, not infrastructure failure.
Warm-ups: Classic Incomplete/Partial, CodeAct Completed/UnearnedCompleteness;
zero tool argument errors each, neither stopped measured execution.
CodeAct descriptive deltas: -28.0% tokens, -4.3% estimated cost, +50.6% analysis
time. Both modes are 0/5 correct, so no correctness-equivalent efficiency win.
All measured outcomes remain in the medians; warm-ups are excluded.

N=1 reclassification is unchanged and its raw artifacts remain untouched.
The solution and portable Diagnosis Release builds passed with zero warnings/errors;
the final code gate is **279 non-live tests passed, zero failed/skipped**.
No further code changes followed that gate. No prompt changes were made during
continuation. No Medium/Large runs were started.

## v3 N=3 fresh five-pair result (November 16, 2026)

The committed runner ran under nohup from pushed source
`1cfa1c91b1cb18976bc23c850bb633d3ced1c727`.
Comparison `small-20261116213201-b41838e36f6d4c1cb9bbf1eded261e48` is VALID:
five measured pairs, one warm-up per mode, unchanged limits/model/pricing/scope
fingerprints, valid telemetry for every run, no infrastructure failures.
The committed checker verified raw tool-error counts and metadata and regenerated
the explicitly scoped v3 Markdown/HTML summary. Historical v2 is unchanged.

| Mode | Correct | Unearned (assessed) | Tool argument errors | Median tokens | Median est. USD | Median analysis s |
|---|---|---|---:|---:|---:|---:|
| Classic | 0/5 | 2/5 (2/5) | 0 | 225,429 | 0.5759400 | 49.642 |
| CodeAct | 0/5 | 3/5 (4/5) | 8 | 24,730 | 0.0802000 | 15.344 |

Classic: UnearnedCompleteness 2, Capped 3. CodeAct: UnearnedCompleteness 3,
InvalidSchema 1, Partial 1. CodeAct descriptive deltas are -89.0% tokens,
-86.1% estimated cost, -69.1% analysis time; neither mode was correct, so this
is not a correctness-equivalent efficiency win. Warm-ups (excluded from aggregates):
Classic Completed/UnearnedCompleteness, CodeAct Failed/InvalidSchema, zero tool
argument errors each. The model failure did not stop measured execution.

Both earlier zero-measured-trial attempts/logs remain outside summaries:
`artifacts/v3-sweep-20261116/aborted-20261116T212359Z/` and
`artifacts/v3-sweep-20261116/aborted-20261116T213138Z/`.
The second archive's 58 files plus log were verified byte-for-byte before/after
the move; its separate reclassification preserves the original raw events.
N=1 source SHA-256s still match their manifest; its committed checker passes
against the current binaries. The separate Diagnosis Release build also passes,
zero warnings/errors. The code gate remains 279 non-live passes.

Commit/push this N=3 record before starting full Small, five pairs under nohup.
No Medium/Large.

## Shared terminal-failure propagation correction (November 16, 2026)

The N=3 continuation from `b532c72` stopped during CodeAct warm-up, before any
measured trials. A recovered `get_fx` argument error was copied by AnalysisService
into the final `RUN_FAILED` event, although the actual final outcome was schema-invalid
model output. This was the remaining layer of the approved classification fix,
not observed Azure/auth/runtime failure.

Failure selection now lives in AgentShared and is used by AnalysisService as well
as the benchmark/reclassifier. Recovered input errors, including their historical
terminal echoes, cannot override later model outcomes; genuine infrastructure
events retain precedence. Both modes have regression cases for the exact sequence.
Offline reclassification of the stopped attempt's raw events confirms no warm-up
infrastructure failures and one CodeAct tool argument error. Its comparison stays
invalid solely because no measured pairs exist; no raw artifact was rewritten.

Release solution build: zero warnings/errors. **279 non-live tests passed, zero
failed/skipped**, ten projects; TRX:
`artifacts/v3-sweep-20261116/tests-shared-failure-fix/`.
No prompt, schema, tool, pricing, model configuration or feedback/retry behavior changed.
Commit/push this correction before archiving the second zero-trial attempt and
starting a fresh N=3 via the committed runner. Full Small follows only after
checking and committing/pushing N=3; Medium/Large remain unauthorized.

## Approved v3 continuation gate (November 16, 2026)

The recovered tool-input-error validity fix is shared by Classic, CodeAct, warm-ups and
offline reclassification. Explicit input validation errors followed by model continuation
are recorded model behavior. Per-trial `toolArgumentErrors` deduplicates host/model wrapper
events; warm-up records remain separate and unmeasured. Infrastructure/runtime/implementation
faults retain precedence over model task failures.

Pricing fingerprints now use normalized numeric decimal values. Actual v2/v3 metadata has
identical rates and complete pricing provenance, verified against the new .NET fingerprint
`1d3ecc56b26a79ade912464e4e17a7d00548faeb991f10846bb3a8e06a2ad41c`.
No raw pricing fields or fingerprints were edited.

N=1 reclassification wrote a separate
`small-20261116204456-a702d6024c2a4eb1afcc742408d492f0-reclassified` comparison.
Every original raw file and the original comparison passed SHA-256 verification against
`artifacts/v3-sweep-20261116/n1-raw-before.sha256`. All ten match labels, comparison validity,
correctness/unearned counts, task outcomes, usage, cost and timing measurements are unchanged.
All ten measured tool-argument counts are zero. Old warm-up oracle scores are unassessed;
their validity/counts are derived from raw events without a replay.

The three non-correct N=1 CodeAct trials are documented in README Results: pair 2 is a
zero/empty placeholder; pair 4 omits all four required breaches and has an inconsistent flag;
pair 5's null baseCurrency fails the schema before semantic completeness can be assessed.
No prompt text changed.

Reproducibility helpers now live in committed `scripts/vm/`: run/check profiles, sample export
and the Diagnosis oracle/sample project. No hard-coded VM paths/endpoints/credentials remain
in these helpers. The new checker was run against N=1 and independently verified raw counts,
model configuration, pricing/provenance, fingerprints, scope and timelines. Oracle/sample
helpers were built and exercised against historical synthetic evidence without live calls.

Pre-live validation: Release solution and Diagnosis builds, zero warnings/errors;
**275 non-live tests passed, zero failed/skipped**, ten projects. TRX:
`artifacts/v3-sweep-20261116/tests-validity-fix/`.
After commit/push, preserve the stopped N=3 directory/log under `aborted-<UTC timestamp>`,
then run new N=3 and full Small with the committed scripts, five pairs each under nohup.
Archive directories are excluded from summaries. Commit/push after each profile;
stop on infrastructure failures only. Medium/Large remain unauthorized.

## v3 implementation gate (November 16, 2026)

The approved shared-prompt v3 additions are scope-aware and identical across modes.
Shared prompt SHA-256 (canonical LF-normalized text): `f0cf5bce1c07cceb82ef7e9b2552788dc6154f7eb5e7b4a61a0e8d4d426b667c`;
previous v2 hash: `630a3a841752d3b4475fadde5050b2884c11b59b28f87e4c43a694fc57ebbc86`.
The output schema, tools, oracle, model, limits and pricing are unchanged.
Completeness detection/reporting is post-run only, with no feedback/retry loop.
Missing required components are recorded per in-scope household; complete claims with
omissions receive `UnearnedCompleteness`, a valid model task outcome, not infrastructure failure.

Release build (`dotnet build -c Release --no-restore`): zero warnings/errors.
All ten non-live test projects: **262 passed, zero failed/skipped**, using
`Category!=LiveModel&Category!=Hyperlight&Category!=HyperlightKnownIssue`.
TRX evidence is under `artifacts/v3-sweep-20261116/tests-non-live/`.
This is not a new Hyperlight test gate; the five earlier supported runtime passes and the
excluded native cancellation known issue remain recorded below.

Authorized live sequence after the implementation commit/push: five measured pairs each
for Small N=1, N=3, then full Small, under nohup with logs and unchanged 20-turn/120-second
limits. Commit/push after each profile; stop on infrastructure failure, retain task outcomes.
v3 evidence/summary lives under `artifacts/v3-sweep-20261116/`, separate from historical v2.
Medium/Large are not authorized.

The first pre-run hash calculation omitted the repository's CRLF-to-LF normalization;
the canonical hash above is confirmed by both modes' runtime fingerprints. No prompt text
changed after the implementation commit. The live launcher serialized the unchanged
USD 2.50/10.00 rates as 2.5/10.0; pricing fingerprints preserve decimal scale. v3's pricing
fingerprint is therefore `92d04a975e504c55a723faf6ec9887813f4d6d489c3fb72b7caac7b8f19f7843`,
versus historical v2's `de12efe9ee6ec3cf44038e3e73e8fb23e5569fcf87f6bd624117238fe7142ae2`.
Numeric rates and full pricing provenance are identical; v3 keeps one representation and
does not mix either prompt cohort. Schema/tools/model/dataset fingerprints match v2 exactly.

### v3 Small N=1 completed

Source commit: `4f3aaac90fe65c59110ffc53b3676bc780a212a1`.
`nohup` run, five measured pairs plus one warm-up per mode; process exited 0.
Comparison `small-20261116204456-a702d6024c2a4eb1afcc742408d492f0` is VALID:
matched cross-mode fingerprints, reconciled telemetry, ten measured trials, no infrastructure
failures. Every run's model and numeric pricing/provenance match the historical configuration.
CodeAct measured trials made eight execute_code calls and 323 host callbacks.

Classic: zero correct, four UnearnedCompleteness, one Capped; CodeAct: two Exact,
two UnearnedCompleteness, one InvalidSchema. Neither the cap nor schema failure was discarded.
Per-household omissions appear in the comparison HTML and JSON; unassessed failures remain
unassessed rather than being classified complete. The separate v3 summary and README Results
record medians and correctness-contextual deltas.
Log: `artifacts/v3-sweep-20261116/n1.log`; comparison and trial evidence:
`artifacts/v3-sweep-20261116/n1/`. Historical v2 artifacts were not rewritten.

### v3 Small N=3 stopped during warm-up

Source commit: `d8c76c87fdc4543200e3733e94b2d83e4d28c5aa`.
`nohup` process exited 1. Comparison
`small-20261116205402-85294096917f4740ad11516dcccee948` is INVALID with
`ToolArgumentException` and `MissingOrUnpairedTrials`; **zero measured trials**.
Both warm-up records and all generated code/events remain saved.

CodeAct warm-up `317344e395ff4c2f9c6df668eb8c7fa5` first failed with Python
`KeyError: 'baseCurrency'`, then called `get_fx(fromCurrency="JPY", toCurrency="Unknown")`
and subsequently with null `toCurrency`. The host correctly rejected those invalid arguments.
The agent later returned a schema-valid placeholder report (zero total, empty components),
and the run ended Completed with valid telemetry. This is not a correctness pass.

The existing `BenchmarkHarness.FindFailure` selects the first event with an error category
when no worker/model-call failure exists; `TrialResult.InfrastructureFailed` treats
ToolArgumentException as infrastructure. `WarmUpAsync` therefore promoted a recoverable
model tool-argument error into a harness-invalid comparison. No Azure/auth/worker outage
was observed. Classification was **not changed** and no attempt was rerun or discarded.
Full Small was not started; Medium/Large remain unauthorized. No benchmark workers remain.

Log: `artifacts/v3-sweep-20261116/n3.log`; comparison, warm-up timelines and raw evidence:
`artifacts/v3-sweep-20261116/n3/`. The regenerated v3 summary includes the blocked scope with
no invented measured rates/medians. README Results records the stop and N=1's full medians.

After the stop, the summary renderer was corrected to label scopes with no measured trials
as "not measured" / "unassessed", displaying INVALID and its failure category directly
in the outcomes column rather than an empty label or `0/0 (unavailable%)`.
This is reporting-only; no classification, prompt, scoring, model configuration or live
evidence was changed. A focused regression covers this pre-measurement failure case.
Final Release build: zero warnings/errors; final full non-live suite:
**263 passed, zero failed/skipped**, ten projects. TRX:
`artifacts/v3-sweep-20261116/tests-final/`.

**Status: partial VM validation; initial live pair had a failed Classic task outcome.** Bootstrap and five guest tests now
passed on Ubuntu x64. Native cancellation remains a labelled SDK limitation; see the dated
[Phase B record](#phase-b-vm-validation-november-16-2026). Full-profile validation is not complete.
The offline POC does not require this workflow. No command in this document authorizes resource
creation, installation or deletion. Verification is limited to the commands/results explicitly
recorded in the dated Phase B section; the complete helper workflow is not yet verified.

## What a real run needs

- Linux x64 with accessible KVM/MSHV, or Windows x64 with Windows Hypervisor Platform.
- A Python guest matching the pinned Hyperlight package version, not an arbitrary guest build.
- Authorized Azure OpenAI access, deployment configuration and sufficient request capacity.
- A Release build and generated Small/Medium/Large data, as in the README.
- Six actual supported Hyperlight test passes, not skips; explicitly exclude the retained
  `HyperlightKnownIssue` cancellation test until the SDK supports interrupting native execution.

An Azure VM must support nested virtualization. A catalog capability or SKU family document
is a prerequisite, not proof that the guest can execute. A normal hosted container or Codespace
must not be assumed to provide the required hypervisor device.

## Scripts and approval boundaries

| Script | Purpose and limits |
| --- | --- |
| [provision-vm.sh](../../scripts/provision-vm.sh) | Prints a plan by default; `--approved-create` can create resources only after explicit approval and metadata checks. Requires a new dedicated resource group. |
| [bootstrap-vm.sh](../../scripts/bootstrap-vm.sh) | Prints a plan by default; `--approved-install` installs host packages, SDK and matching guest and changes KVM group membership. Reconnect before relying on new permissions. |
| [verify-observability.ps1](../../scripts/verify-observability.ps1) | Offline or Hyperlight-only acceptance gates; legacy Live mode is retired before inference. Preserves selected environment, isolates TRX, and rejects skips. Use README commands for current cohorts. |
| [VM helpers](../../scripts/vm/) | Setup/run compatibility entry points now delegate to approved bootstrap/cohort runners; download and summary helpers use caller/repository paths, not a root-user checkout. |

The current provisioning plan uses Ubuntu 24.04 x64, D4s v5, SSH public-key
authentication, CorpNetPublic TCP 22 rules on both NIC and subnet NSGs, a 64 GiB
Standard SSD and Standard public IPv4. It requires the corporate VPN and
Microsoft.Network/AllowBringYourOwnPublicIpAddress feature registration. Policy can
delete SSH rules; follow the README inspection/recreation instructions.
The historical ECDSA token validation defect is fixed; plan tests cover accepted
public-key formats. Review the actual printed plan before approval.

The old compute-only estimate was USD 0.192/hour in westus2, checked November 3, 2026.
It is **not a current price or budget approval**. Disk, public IP, egress and Azure OpenAI are
additional. Reconfirm current price, quota, regional capacity and policy before any approval.
Dedicated-resource-group deletion destroys all resources in it and needs separate approval.
Stopping/deallocating compute alone does not remove all ongoing charges.

The exact image/SKU lookup findings and why an absent `NestedVirtualization` field is not a
generic positive result are retained in
[execution-observability.md](execution-observability.md#provisioning-checks-read-only-not-live-execution).

## Validation after approved setup

Configure Azure variables named in the README and set the matching guest path in the same shell.
Do not record environment-specific values, credentials or identity tokens in logs or source.
First perform the runtime gate, then a short live pair. The helper is an optional stricter gate:

```powershell
# NOT YET VERIFIED. Run only on an approved, already configured x64 host.
$validation = Join-Path scripts verify-observability.ps1
& $validation -Mode Hyperlight
& $validation -Mode Live -PreflightOnly
& $validation -Mode Live -Trials 1
```

Legacy Live mode is now retired before any inference; use the README's committed
cohort runner instead. The old acceptance helper required exact
oracle results, valid telemetry and actual code-execution completion without stand-in events.
The benchmark now separates task outcomes from infrastructure validity; this optional stricter
acceptance helper still requires exact oracle matches and actual guest execution. Live benchmark
commands explicitly select `--model live`.

If script execution is forbidden, do not bypass policy. Run the individual permitted dotnet
commands in the README and inspect the same conditions explicitly. Full Linux PowerShell helper
execution and provisioning remain unverified; bootstrap installation has run with approval.

## Record, do not infer

Retain commit, dataset hashes, model/deployment configuration without secrets, guest version,
host architecture, execution kind, every trial's completion/oracle scores, usage, estimated cost
and model-facing/granular/sandbox counts. Keep failed attempts. Do not mix batched Classic,
offline controls and unbatched live trials into one efficiency claim.

Hyperlight internal initialization/cleanup/CPU/memory, provider internal retries and absent streams
are unavailable rather than zero. Compare only after both agents do useful, correctly scored work.
The old full acceptance matrix remains reference material, not a requirement for the offline POC.

## Phase B VM validation (November 16, 2026)

Host: Ubuntu 24.04 x64 Azure Standard_D4s_v5; KVM readable/writable after reconnect.
Bootstrap installed .NET SDK 9.0.317, pwsh, Azure CLI and Hyperlight Python guest 0.4.0.
Source: `3088451dbbdc9e833e1cc990cf90b3b7666a89d0`; worker-safety changes were in the
working tree during validation, before review/commit. Shared prompt hash is unchanged:
`630a3a841752d3b4475fadde5050b2884c11b59b28f87e4c43a694fc57ebbc86`.

### Clean clone and original runtime gate

A separate **local-source** clone (`git clone --no-local`) at that SHA, with an empty NuGet
package cache, passed locked restore and Release build (zero warnings/errors).
Ten fresh TRX files contain **231 passed, zero failed, zero skipped**.
This is not a remote-origin clone.

The original unfiltered six-test gate hung in
`Real_running_program_observes_cancellation_and_retains_source`: the five-second token did
not interrupt `while True: pass`. Its process consumed a CPU core for over six minutes;
the journal stopped at `CODE_EXECUTION_STARTED`. The test process tree was stopped and
its incremental evidence copied before cleanup. No six-test pass is claimed.

Each other test was then run individually with `--blame-hang --blame-hang-timeout 3m`:

| Original test | Result |
| --- | --- |
| Provider_constructs_with_provider_owned_tools_and_disposes | 1 passed |
| Real_python_stdout_and_host_callbacks_are_captured | 1 passed |
| Real_python_exception_preserves_stderr_and_failed_execution | 1 passed |
| Analysis_runs_through_real_sandbox | 1 passed |
| Sandbox_state_is_isolated_between_runs | 1 passed |

Every invocation discovered/executed exactly one test; **zero failures, zero skips**.
These use a scripted model to drive a **real guest**; they are not live-model passes.

### Supported gate and process containment

[Pinned SDK investigation](../sdk-findings.md#native-execution-cancellation-november-16-2026)
confirmed no supported mid-execution timeout/interrupt API. With explicit design approval,
every benchmark analysis now runs in a supervised child process, equally for Classic and
CodeAct, measured runs, warm-ups and Classic batches. At the existing configured wall-time
deadline the parent terminates/reaps only that worker process tree and saves `TimedOut`
with INCOMPLETE diagnostics, interrupted invocation markers, usage, generated source and
the continued journal/timeline. No detached native execution is treated as cancellation.
Worker startup is within the deadline; trial wall time includes process overhead, while the
normal run metrics retain the analysis duration. Standalone APIs do not have this watchdog.

The cancellation test keeps its original body/assertions, additionally labelled
`HyperlightKnownIssue`. The explicit gate is:

```bash
dotnet test tests/SterlingVale.CodeAct.IntegrationTests -c Release --no-build \
  --filter 'Category=Hyperlight&Category!=HyperlightKnownIssue' \
  --blame-hang --blame-hang-timeout 3m
```

Post-change Release build: zero warnings/errors. Fresh TRX counts: **236 non-live passes**
across ten projects (five added containment/recovery cases); supported gate **5 passed,
0 failed, 0 skipped**. The excluded known issue is **not passed**.
A five-second native infinite-loop watchdog probe terminated/reaped the guest in **5.095 s**
and retained exact Python and its journal. This is runtime containment evidence, not a live
model benchmark. An earlier probe also terminated the process but failed its source-file
assertion because capture was metadata-only; it is retained as a failed probe, not a pass.

### Managed identity and first live pair

Device-code login was blocked by Conditional Access. The app already uses
`DefaultAzureCredential`; inspection of Azure.Identity 1.13.1 confirmed managed identity
precedes CLI credentials and defaults to system-assigned identity without a client ID.
No credential-chain code or laptop behavior was changed. In process-local VM settings,
API-key and service-principal/workload variables were unset. An Azure-Identity
`DefaultAzureCredentialCredentialSelected` event confirmed **ManagedIdentityCredential**.
The one-token call through the actual app provider succeeded on attempt one (10 input,
1 output token, finish `length`, expected for this tiny cap). No retry was needed.
Access tokens, identity IDs and resource endpoints were not logged or committed.

One measured Small pair, `--households 1`, after a labelled warm-up per mode.
Settings: 20 turns, 120 s, temperature zero, batching zero,
`parallel_tool_calls=true`, response `json_schema`, `strict=false`, 16,384 output tokens.
Prompts, limits, deployment and v2 output schema were not tuned.

| Mode | Status | Oracle | Finish | Turns | Input | Output | Trial ms | Analysis ms | execute_code | Callbacks |
| --- | --- | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Classic | Failed | InvalidSchema | stop | 6 | 23,739 | 518 | 15,475.8603 | 14,815.3756 | 0 | 0 |
| CodeAct | Completed | Exact | stop | 2 | 8,235 | 1,923 | 13,267.1049 | 12,555.7937 | 1 | 49 |

| Mode | list_households | get_policy | list_accounts | list_positions | get_price | get_fx | asset_class | execute_code |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Classic | 0 | 1 | 1 | 3 | 13 | 3 | 0 | 0 |
| CodeAct | 1 | 1 | 1 | 3 | 26 | 17 | 0 | 1 |

Classic counts are model-facing data calls; CodeAct's data counts are actual sandbox callbacks.
Both modes had matching fingerprints and valid telemetry. Classic covered all 13 symbols /
26 scoped positions with successful prices, but declared complete with `totalValue: null`
and empty calculation arrays. The final message was captured fully (not truncated/redacted),
finish `stop`, 96 final-response output tokens. Local v2 validation correctly rejected it.
This is a schema/report-integrity failure, not missing guest execution, timeout or auth failure.

CodeAct executed one saved 5,640-byte Python program: `CODE_EXECUTION_COMPLETED`, success true,
exit code zero, stdout available, 49 real callbacks. Its report scored Exact: flagged/breach
precision/recall 1 and allocation/trade errors zero. CodeAct usage now covers completed analysis,
not the laptop's generation-only first turn. This single correct CodeAct run does **not** make
the paired comparison valid under the policy in force at execution, or establish a performance
advantage. The revised validity policy below supersedes that original classification.

**STOP (original policy):** comparison exited 1, Classic failed. N=3 and full Small/Medium/Large were not run in that initial attempt.
The initial comparison JSON lost Classic's failure-category fields during worker JSON
round-trip; the authoritative journal retains `InvalidModelOutput`. Case-insensitive failure
deserialization and a transport regression fix this in code; original live artifacts are
retained unchanged. No model call was repeated to repair presentation.

Ignored evidence root: `artifacts/phase-b-20261116-1721/`, containing clean clone/cache,
fresh test logs/TRX, SDK snapshots/hashes, native probes, managed-identity preflight and
`live-n1/` comparison/run evidence. Warm-ups are retained but excluded from the tables.

## Three-trial sweep and revised validity (November 16, 2026)

The approved baseline `ffffc5a3dd4737913cbf266601276878687fb90a` was pushed and the
remote branch SHA verified before these changes. The user then approved N=1, N=3 and full Small,
each with `--trials 3`, committing/pushing results after each profile. Medium/Large are not approved.

The benchmark now treats **INVALID / stop sweep** as infrastructure/harness failure:
auth/model-call errors, worker crash, missing guest, fairness/config/prompt-hash mismatch,
missing/unpaired trials or telemetry reconciliation failure, including infrastructure errors
in warm-ups. **VALID / recorded** task outcomes include InvalidSchema, Partial, declared
incomplete, turn cap and TimedOut at the configured deadline. VALID is not model success.
Every task outcome remains in medians. Efficiency deltas are withheld unless all measured
trials are oracle-correct; correctness appears alongside any permitted deltas.
The initial pair's artifacts are preserved with their original exit/classification; under the
revised policy its Classic InvalidSchema is a recorded task outcome, not infrastructure invalidity.

Before the sweep: Release build zero warnings/errors; focused comparison tests **63 passed**;
full non-live suite **249 passed across ten fresh TRX files**, zero failed/skipped; supported
real-Hyperlight gate **5 passed**, zero failed/skipped. After adding pricing configuration to
the metadata envelope, the nine existing artifact tests passed. The retained sixth guest
test remains explicitly excluded as `HyperlightKnownIssue`, not a pass.

### Official pricing verification

Read-only Azure deployment metadata identifies model GPT-4o **2024-11-20**, SKU
**GlobalStandard**, resource region **eastus**. CLI metadata access initially reported a stale
subscription context; refreshing the already-approved managed-identity login succeeded.
No Azure resource settings changed. Credentials, resource identifiers and endpoints are omitted.

On **2026-11-16**, the official [Azure Retail Prices API](https://prices.azure.com/api/retail/prices?%24filter=productName%20eq%20%27Azure%20OpenAI%27%20and%20armRegionName%20eq%20%27eastus%27%20and%20contains(meterName,%27gpt%204o%201120%27))
returned these consumption meters, USD, units **1K**, effective **2024-12-01**:

| Meter | Meter ID | USD / 1K | Configured USD / 1M |
| --- | --- | ---: | ---: |
| gpt 4o 1120 Inp glbl Tokens | 3ba7b4f7-d853-55b2-ab71-dcbee21a6277 | 0.0025 | 2.50 |
| gpt 4o 1120 Outp glbl Tokens | 3336fed2-e64d-5639-951a-806ead2ede4b | 0.01 | 10.00 |

Thus `PRICING_INPUT_PER_MILLION` / `PRICING_OUTPUT_PER_MILLION` remain unchanged.
Source URL, verification date and basis are configured through the variables documented in
[.env.example](../../.env.example), read identically by benchmark/API composition, and retained
in each run's `pricingConfiguration`. The public response is retained in
`artifacts/phase-b-sweep-20261116/pricing/azure-retail-20261116.json`.
This is an **uncached retail token cost estimate**, not an invoice: cached-input discounts,
negotiated rates, unreported usage and VM/infrastructure charges are excluded.
The [official pricing overview](https://azure.microsoft.com/en-us/pricing/details/azure-openai/)
also labels retail pricing as estimates.

The sweep retains the same prompt hash
`630a3a841752d3b4475fadde5050b2884c11b59b28f87e4c43a694fc57ebbc86`,
20 turns, 120 seconds, temperature zero, 16,384 output tokens, parallel tool calls true,
strict false, v2 output schema, full capture and batching zero. The VM/guest remain Ubuntu
24.04 x64 Standard_D4s_v5 / Hyperlight Python 0.4.0, using managed identity. Each profile has
one labelled unmeasured warm-up per mode, then three randomized measured pairs.
Analysis duration excludes worker startup/transport; process-inclusive duration is retained
separately. Medians are componentwise over every measured task outcome, not just successes.

**API safety follow-up:** both standalone API hosts lack the benchmark worker watchdog.
The pinned native cancellation limitation still affects direct API hosting; it is not protected
by the benchmark's supervised process containment.

### Small N=1, three measured pairs

Source: `7fea4ec53faa5b83973c04400edd3d23db3c1dc6`.
Comparison: `small-20261116183315-756172429e844d8a89569f332174cce4`.
Dataset hash: `f686013740692996ccae5dcc82d626b76ffba902d27a84e07842666100c7f684`.
Model fingerprint: `4f121c2cc54e6eac998ba721ae76462b5e9bfb04381f029bb417e6583dac468a`.

**VALID**, exit zero; fairness matched and telemetry reconciled in all six trials.
Classic **0/3 oracle-correct**, all Completed/Partial; CodeAct **3/3 Exact**, with actual
Python execution/callbacks. CodeAct precision/recall are all 1 and allocation/trade errors zero.
Classic's mean flagged recall is 0.333333, breach recall 0.111111, allocation MAE
0.0754610705; schema-valid completion is not correctness. No efficiency winner is inferred.

Tool counts below are in this fixed order:
`list_households / get_policy / list_accounts / list_positions / get_price / get_fx / asset_class`.
Classic counts are model-facing data calls; CodeAct counts are real sandbox callbacks.
execute_code is separate. All monetary values are estimated USD under the verified rates.

| Trial | Mode | Status / oracle | Turns | Named tools, order above | Input / output tokens | Analysis s | Est. USD | execute_code | Callbacks |
| --- | --- | --- | ---: | --- | ---: | ---: | ---: | ---: | ---: |
| 1 | Classic | Completed / Partial | 6 | 0/1/1/3/13/3/0 | 23,739 / 724 | 16.695 | 0.0665875 | 0 | 0 |
| 1 | CodeAct | Completed / Exact | 2 | 1/1/1/3/26/26/0 | 8,227 / 1,907 | 12.370 | 0.0396375 | 1 | 58 |
| 2 | Classic | Completed / Partial | 6 | 0/1/1/3/13/3/0 | 23,739 / 771 | 16.429 | 0.0670575 | 0 | 0 |
| 2 | CodeAct | Completed / Exact | 3 | 1/1/1/3/26/26/26 | 15,875 / 2,951 | 16.945 | 0.0691975 | 2 | 84 |
| 3 | Classic | Completed / Partial | 6 | 0/1/1/3/13/3/0 | 23,739 / 719 | 15.396 | 0.0665375 | 0 | 0 |
| 3 | CodeAct | Completed / Exact | 3 | 1/1/1/3/26/17/0 | 16,031 / 3,030 | 18.634 | 0.0703775 | 2 | 49 |
| Median | Classic | Partial 3/3; correct 0/3 | 6 | 0/1/1/3/13/3/0 | 23,739 / 724 | 16.429 | 0.0665875 | 0 | 0 |
| Median | CodeAct | Exact 3/3 | 3 | 1/1/1/3/26/26/0 | 15,875 / 2,951 | 16.945 | 0.0691975 | 2 | 58 |

Medians are componentwise, so a median row need not correspond to one actual trial.
Every measured run's metadata was independently checked for 20 turns/120 s, parallel true,
strict false, 16,384 output tokens and pricing/source/date; the prompt hash matches the
unchanged shared prompt. Evidence: `artifacts/phase-b-sweep-20261116/n1/` and `n1.log`.
The initial launch used an absent default data directory and exited before warm-ups/model
calls or comparison creation. Its `n1-missing-data-startup.log` is retained; the existing
Phase B data directory was then selected and all manifests validated before this live run.

### Small N=3, three measured pairs

Source checkout: `5ea78816437f2a3bd9e246f530cf61975fddbff4` (same benchmark code as
`7fea4ec`, subsequent changes only recorded N=1). Comparison:
`small-20261116183702-cd37a0744d8245e1ae955d9cf00cd06c`.
Dataset/model/prompt hashes, guest, pricing and all limits are identical to N=1; scope is three
households for both modes and the oracle. All six measured configurations/provenance were
independently checked again.

**VALID**, exit zero; fairness matched and telemetry reconciled in every measured trial.
Classic **0/3 oracle-correct**, all Completed/Partial; CodeAct **2/3 Exact**, one
Failed/InvalidSchema. The latter is a model task outcome, not infrastructure invalidity,
and is retained in every median. No efficiency winner is inferred.

Named-tool order is the same seven-tool order defined above.

| Trial | Mode | Status / oracle | Turns | Named tools, order above | Input / output tokens | Analysis s | Est. USD | execute_code | Callbacks |
| --- | --- | --- | ---: | --- | ---: | ---: | ---: | ---: | ---: |
| 1 | Classic | Completed / Partial | 5 | 0/3/3/11/17/3/0 | 39,321 / 1,646 | 16.793 | 0.1147625 | 0 | 0 |
| 1 | CodeAct | Completed / Exact | 4 | 1/3/3/11/117/117/0 | 34,324 / 6,365 | 30.678 | 0.1494600 | 3 | 252 |
| 2 | Classic | Completed / Partial | 5 | 0/3/3/11/17/3/0 | 39,321 / 1,641 | 19.733 | 0.1147125 | 0 | 0 |
| 2 | CodeAct | Completed / Exact | 2 | 1/3/3/11/117/117/0 | 10,656 / 3,017 | 13.943 | 0.0568100 | 1 | 252 |
| 3 | Classic | Completed / Partial | 5 | 0/3/3/11/17/3/0 | 39,321 / 1,646 | 17.510 | 0.1147625 | 0 | 0 |
| 3 | CodeAct | Failed / InvalidSchema | 3 | 1/3/3/11/117/77/0 | 22,610 / 6,516 | 27.201 | 0.1216850 | 2 | 212 |
| Median | Classic | Partial 3/3; correct 0/3 | 5 | 0/3/3/11/17/3/0 | 39,321 / 1,646 | 17.510 | 0.1147625 | 0 | 0 |
| Median | CodeAct | Exact 2/3; InvalidSchema 1/3 | 3 | 1/3/3/11/117/117/0 | 22,610 / 6,365 | 27.201 | 0.1216850 | 2 | 252 |

Evidence: `artifacts/phase-b-sweep-20261116/n3/` and `n3.log`, including warm-ups
(not measured), source SHA, original model responses/Python and full journals.
The benchmark continues to the approved full Small profile; Medium/Large remain unapproved.

### Full Small (10 households), three measured pairs

Source checkout: `55dcaf075369d415abc38dfc59cdcedd42234712` (same benchmark code as
`7fea4ec`; the intervening commits only recorded scoped results). Comparison:
`small-20261116184051-6a9a3fe1d0c148d8bff90034aee2e297`.
All ten households, 33 accounts and 370 positions; no `--households` override.
Dataset/model/prompt hashes, guest, pricing and all limits are identical to the scoped runs.
All six measured configurations/provenance were independently checked again.

**VALID**, exit zero; fairness matched and telemetry reconciled in every measured trial.
Both modes **0/3 oracle-correct**, all Completed/Partial. Classic emitted only one household
per report; CodeAct emitted ten, but neither mode found the oracle's flagged households or
breaches (flagged/breach recall zero). All reports declared `complete` with no missingItems.
These are failed model task outcomes despite schema-valid completion, not useful equivalent
work. CodeAct's zero allocation/trade error fields do not override the Partial classification
or missing breach/flag recall. No efficiency winner is inferred from lower usage/duration.

Named-tool order is the same seven-tool order defined above.

| Trial | Mode | Status / oracle | Turns | Named tools, order above | Input / output tokens | Analysis s | Est. USD | execute_code | Callbacks |
| --- | --- | --- | ---: | --- | ---: | ---: | ---: | ---: | ---: |
| 1 | Classic | Completed / Partial | 8 | 1/10/10/10/17/3/17 | 68,608 / 1,575 | 23.525 | 0.1872700 | 0 | 0 |
| 1 | CodeAct | Completed / Partial | 5 | 4/15/15/50/1096/1096/548 | 21,857 / 2,361 | 20.329 | 0.0782525 | 4 | 2824 |
| 2 | Classic | Completed / Partial | 8 | 1/10/10/10/17/3/17 | 68,608 / 1,600 | 22.201 | 0.1875200 | 0 | 0 |
| 2 | CodeAct | Completed / Partial | 5 | 4/15/15/50/548/548/0 | 19,775 / 1,978 | 16.350 | 0.0692175 | 4 | 1180 |
| 3 | Classic | Completed / Partial | 8 | 1/10/10/10/17/3/17 | 68,608 / 1,580 | 23.418 | 0.1873200 | 0 | 0 |
| 3 | CodeAct | Completed / Partial | 5 | 4/15/15/50/548/548/548 | 21,175 / 2,088 | 16.341 | 0.0738175 | 4 | 1728 |
| Median | Classic | Partial 3/3; correct 0/3 | 8 | 1/10/10/10/17/3/17 | 68,608 / 1,580 | 23.418 | 0.1873200 | 0 | 0 |
| Median | CodeAct | Partial 3/3; correct 0/3 | 5 | 4/15/15/50/548/548/548 | 21,175 / 2,088 | 16.350 | 0.0738175 | 4 | 1728 |

Every measured CodeAct trial was independently checked against its actual guest events,
not inferred from configuration: N=1 had 5 guest returns / 5 successful executions; N=3 had
6 returns / 5 successful executions; full Small had 12 returns / 9 successful executions.
Python execution failures remain model task evidence, not silently counted as successful
programs. All returns identify runtime `hyperlight`; there are no not-executed/stand-in events.
Actual callback-start event counts equal every trial's reported callbacks, including repeated
calls across code executions. All three comparisons' efficiency deltas are null.

Evidence: `artifacts/phase-b-sweep-20261116/full-small/` and `full-small.log`.
Each scope has two persisted labelled warm-ups, excluded from its six measured rows.
Results were committed/pushed after N=1 and N=3 before launching the next profile.
**STOP:** the approved N=1, N=3, full Small sweep is complete. Medium/Large were not run
and require separate approval. No prompts, limits, schema, package pins or Azure resource
settings changed during the sweep.

## Full-Small diagnosis and cross-profile reporting (November 16, 2026)

Reporting-only follow-up from `25ad7b70df6094b746e7757af8038129bf289179` on the same
Standard_D4s_v5 VM. No new model calls, guest executions or Medium/Large runs.
The original prompt-hash cohort and all its trial evidence are unchanged.

For full-Small pair 1, CodeAct H0001's USD **826,579.16** matches the unchanged oracle
and an independent Decimal calculation of all 26 positions. The first computational
divergence is omitted allocations/drifts: the Python hard-codes empty analytical arrays
and `flagged: false` despite four oracle breaches. This is semantic incompleteness,
not an observed valuation/units error or oracle arithmetic bug. Classic retrieved only
10 of 33 accounts' positions, then stopped at turn 8 with one fabricated household;
the captured final response itself is incomplete, not truncated by the harness.
See the [field-by-field diagnosis and independent ledger](../sample-run/diagnosis.md).
Shared-prompt, semantic-validation and scoring-coverage fixes are proposed there but
**not applied**; prompt changes require approval and a new shared-prompt hash cohort.

The explicit-comparison `summarize` command writes local
`artifacts/summary.md` and `artifacts/summary.html`: all three scopes, both modes,
correctness first, all-outcome medians and correctness-labelled descriptive deltas.
All 18 individual trial timelines are directly linked. Full-Small's lower resource
usage remains labelled **both Partial, not a correctness-equivalent win**.
The [committed sample](../sample-run/README.md) contains one real full-Small pair,
actual Python, invocation excerpts and a comparison HTML; aliases replace run-directory
identifiers. The 14-file sample passed endpoint/secret/absolute-path scans,
relative-link checks and byte-for-byte comparison of its four Python programs.

Validation: Release build **0 warnings/errors**; full non-live suite **255 passed,
0 failed, 0 skipped across ten projects**. Six new reporting cases cover scope
preservation, analysis-time/all-outcome medians, contextual deltas, unknown measurements,
and rejection of mixed prompt/model/pricing/execution cohorts. The observability helper
now expects 255; historical 249-test sweep evidence above is not relabelled.
