# Optional x64 validation workflow

**Status: not yet verified end to end. Provisioning and bootstrap are paused.**
The offline POC does not require this workflow. No command in this document authorizes resource
creation, installation or deletion; all command templates below are **not yet verified**.

## What a real run needs

- Linux x64 with accessible KVM/MSHV, or Windows x64 with Windows Hypervisor Platform.
- A Python guest matching the pinned Hyperlight package version, not an arbitrary guest build.
- Authorized Azure OpenAI access, deployment configuration and sufficient request capacity.
- A Release build and generated Small/Medium/Large data, as in the README.
- Six actual real-Hyperlight test passes, not skips.

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

The Live preflight makes a small Azure call. Full Live mode restores/builds, requires all 220
non-live passes and six actual guest passes, then runs profiles sequentially. It requires exact
oracle results, valid telemetry and actual code-execution completion without stand-in events.
The benchmark now rejects incomplete/incorrect comparisons too; this helper additionally requires
exact oracle matches and actual guest execution. Live benchmark commands explicitly select `--model live`.

If script execution is forbidden, do not bypass policy. Run the individual permitted dotnet
commands in the README and inspect the same conditions explicitly. Linux PowerShell script
execution and all installation/provisioning remain future verification work.

## Record, do not infer

Retain commit, dataset hashes, model/deployment configuration without secrets, guest version,
host architecture, execution kind, every trial's completion/oracle scores, usage, estimated cost
and model-facing/granular/sandbox counts. Keep failed attempts. Do not mix batched Classic,
offline controls and unbatched live trials into one efficiency claim.

Hyperlight internal initialization/cleanup/CPU/memory, provider internal retries and absent streams
are unavailable rather than zero. Compare only after both agents do useful, correctly scored work.
The old full acceptance matrix remains reference material, not a requirement for the offline POC.
