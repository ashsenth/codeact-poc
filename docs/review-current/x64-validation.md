# Optional x64 validation workflow

**Status: partial VM validation; first live pair invalid.** Bootstrap and five guest tests now
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
- Five actual supported Hyperlight test passes, not skips; explicitly exclude the retained
  `HyperlightKnownIssue` cancellation test until the SDK supports interrupting native execution.

An Azure VM must support nested virtualization. A catalog capability or SKU family document
is a prerequisite, not proof that the guest can execute. A normal hosted container or Codespace
must not be assumed to provide the required hypervisor device.

## Scripts and approval boundaries

| Script | Purpose and limits |
| --- | --- |
| [provision-vm.sh](../../scripts/provision-vm.sh) | Prints a plan by default; `--approved-create` can create resources only after explicit approval and metadata checks. Requires a new dedicated resource group. |
| [bootstrap-vm.sh](../../scripts/bootstrap-vm.sh) | Prints a plan by default; `--approved-install` installs host packages, SDK and matching guest and changes KVM group membership. Reconnect before relying on new permissions. |
| [verify-observability.ps1](../../scripts/verify-observability.ps1) | Offline, Hyperlight-only or Live gates. Preserves its selected environment values, uses isolated TRX directories and rejects skipped runtime tests. |
| [older VM helpers](../../scripts/vm/) | Historical setup/download/run/summarize workflow; not reverified here. |

The provisioning defaults reviewed previously were Ubuntu 24.04 x64, D4s v5, SSH public-key
authentication, an SSH source IPv4 /32, otherwise denied inbound traffic, a 64 GiB Standard SSD
and Standard public IPv4. Review the script's actual printed plan before approval.
**Known open bug:** ECDSA public-key validation rejects valid ECDSA algorithm tokens; do not
interpret that rejection as a private-key detection result. See finding D3 in the
[POC review](poc-demo-review.md).

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

The Live preflight makes a small Azure call. Full Live mode restores/builds, requires all 236
non-live passes and five actual guest passes, explicitly excluding `HyperlightKnownIssue`,
then runs profiles sequentially. It requires exact
oracle results, valid telemetry and actual code-execution completion without stand-in events.
The benchmark now rejects incomplete/incorrect comparisons too; this helper additionally requires
exact oracle matches and actual guest execution. Live benchmark commands explicitly select `--model live`.

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
the paired comparison valid or establish a performance advantage.

**STOP:** comparison exited 1, Classic failed. N=3 and full Small/Medium/Large were not run.
The initial comparison JSON lost Classic's failure-category fields during worker JSON
round-trip; the authoritative journal retains `InvalidModelOutput`. Case-insensitive failure
deserialization and a transport regression fix this in code; original live artifacts are
retained unchanged. No model call was repeated to repair presentation.

Ignored evidence root: `artifacts/phase-b-20261116-1721/`, containing clean clone/cache,
fresh test logs/TRX, SDK snapshots/hashes, native probes, managed-identity preflight and
`live-n1/` comparison/run evidence. Warm-ups are retained but excluded from the tables.
