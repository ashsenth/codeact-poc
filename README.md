# Sterling Vale CodeAct

> **Readiness: NOT COMPLETE.** Live Small/Medium/Large acceptance must be established on an x64
> virtualization-capable host. Passing offline tests does not establish Hyperlight execution or
> model correctness. See [the observability review](docs/review-current/execution-observability.md).

A production-quality .NET reference application that runs a **controlled A/B comparison** between two
agent orchestrations over an identical application:

- **Classic** — Microsoft Agent Framework direct tool-calling.
- **CodeAct** — Microsoft Agent Framework CodeAct via `HyperlightCodeActProvider` and `execute_code`.

Both apps share the same HTTP contract, prompts, model configuration, datasets, tools, limits, and
output schema. **The only intentional variable is orchestration.**

> **Disclaimer.** All data is **synthetic** and reproducible from a seed. The application produces
> **synthetic analysis, not investment advice**, and proposes asset-class notionals only — never
> named securities.

## Scenario

"Sterling Vale" is a synthetic wealth manager. For each household, the agent values positions in the
household base currency, computes asset-class allocations, evaluates drift and risk limits, and
proposes rebalancing notionals — emitting a schema-valid `ExposureReport`. A deterministic
**oracle** computes the same result independently and is used only to score correctness (it is never
an agent tool).

## Architecture

```
Classic.Api ─┐                             ┌─ CodeAct.Api
             │   (identical contract via   │
             ├─  Api.Hosting endpoints) ───┤
             │                             │
        AnalysisService ── AnalysisRunner ── AgentShared (prompt, schema, fake model, fingerprints)
             │                 │
        IAgentFactory ◄── the ONLY seam ──► ClassicAgentFactory | CodeActAgentFactory (Hyperlight)
             │
        Tools (7 read-only tools) ── Application (oracle, FX) ── Domain (pure)
             │
        Infrastructure (dataset load, chat client, FileRunStore)
```

See [docs/architecture.md](docs/architecture.md) for detail.

## Prerequisites

- **.NET SDK 9.0** (pinned in `global.json`).
- For **live** runs: an Azure OpenAI deployment (prefer `DefaultAzureCredential`).
- For **CodeAct runtime**: an **x64 host with hardware virtualization** — see below.

### CodeAct runtime (Hyperlight) — host requirements and setup

CodeAct's `execute_code` runs model-generated code inside a **Hyperlight micro-VM**, which requires
an **x64 host with hardware virtualization**:

| Host | Hypervisor backend | Notes |
| --- | --- | --- |
| Linux x64 | KVM (`/dev/kvm`) or MSHV | recommended; simplest guest build/run |
| Windows x64 | Windows Hypervisor Platform (WHP) | enable the "Windows Hypervisor Platform" optional feature |

> **Not supported: Windows on ARM64.** Hyperlight's backends (WHP / KVM / MSHV) are x64-oriented, so
> the CodeAct sandbox cannot run on an ARM64 Windows machine. Use an x64 Linux or Windows host, an
> x64 cloud VM, or GitHub Codespaces for CodeAct runtime and the gated Hyperlight tests. On an
> unsupported host, leave `HYPERLIGHT_PYTHON_GUEST_PATH` **unset** — the CodeAct API and benchmark
> fall back to an offline `execute_code` stand-in (deterministic, but **not** a real CodeAct
> measurement: the sandbox and `call_tool(...)` are not exercised, so CodeAct produces no real data).

**Setup on a supported x64 host:**

1. **Enable virtualization.** Linux: ensure `/dev/kvm` exists and your user can access it. Windows
   x64: enable the *Windows Hypervisor Platform* feature, then reboot.
2. **Obtain the Hyperlight Python guest module.** This is a preview artifact — build or download it by
   following the official Microsoft Agent Framework Hyperlight CodeAct sample
   (`microsoft/agent-framework`) together with the `hyperlight-dev/hyperlight-wasm` project. The
   factory calls `HyperlightCodeActProviderOptions.CreateForWasm(<guest>)`, so the guest is the
   Python-on-WASM module those samples produce. On Linux x64,
   [`scripts/vm/download-guest.sh`](scripts/vm/download-guest.sh) fetches the version-matched guest
   package and records its path for you.
3. **Point the app at the guest:**
   - Linux/macOS: `export HYPERLIGHT_PYTHON_GUEST_PATH=/abs/path/to/guest`
   - Windows x64: `$env:HYPERLIGHT_PYTHON_GUEST_PATH = "C:\abs\path\to\guest"`
4. **Verify the runtime** before benchmarking — this should now **pass**, not skip:
   ```bash
   dotnet test --filter "Category=Hyperlight"
   ```
5. Run the live benchmark as usual; CodeAct will execute for real and its `households` will be
   populated.

When `HYPERLIGHT_PYTHON_GUEST_PATH` is unset the CodeAct API and benchmark use the offline
`execute_code` stand-in and the Hyperlight runtime tests **skip** with an explicit reason. No Docker
image is provided because Hyperlight needs host virtualization that isn't portable across containers.

## Live validation on x64

Readiness remains **NOT COMPLETE** until the clean-clone, real-runtime and live oracle evidence
has been recorded in the [observability review](docs/review-current/execution-observability.md).
Phase A is laptop preparation only. Phase B starts only after connecting to the approved VM.
Never change execution policy or global git configuration. Never commit endpoints, credentials,
generated datasets or `artifacts/`. Do not push without approval.

### Provision and connect (explicit approval required)

The scripts are Bash scripts; use an existing Bash environment for planning. Approved creation
also requires Azure CLI and `jq` in that environment. No Azure resource is created by default.
Review the printed commands and cost before approving creation:

```bash
bash scripts/provision-vm.sh --subscription "$SUBSCRIPTION_ID" \
  --ssh-public-key "$HOME/.ssh/id_ed25519.pub" --resource-group codeact-validation \
  --location westus2 --name codeact-validation --size Standard_D4s_v5
```

Defaults: Ubuntu 24.04 x64, SSH public-key authentication only, 64 GiB Standard SSD, Standard public
IPv4, SSH allowed only from the detected current IPv4 `/32`, all other inbound traffic denied.
Use `--ssh-ip YOUR_CURRENT_IPV4` if outbound IP detection is unavailable. The resource group must
be new and dedicated. Dsv5 supports nested virtualization; actual KVM access must still be verified.
The SKU API can omit `NestedVirtualization`: the script checks x64/V2 metadata, honors explicit
negative capability values and permits an absent field only for the documented `Standard_D4s_v5`
default. Other sizes with an absent field require separate review, not an automatic fallback.
Standard security avoids Trusted Launch restrictions on nested virtualization.
The [retail price API](https://prices.azure.com/api/retail/prices) lists **USD 0.192/hour compute** for
Linux D4s v5 in westus2 (checked 2026-11-03), about USD 4.61/day if left running, **plus** disk,
public IP, egress and Azure OpenAI charges. Other sizes/regions require a reviewed estimate.
Quota, regional capacity, public-IP policy and current prices must be checked before approval.

Only after explicit approval, repeat the plan command with
`--estimated-hourly-usd 0.192 --approved-create`. This is not authorization for the assistant to run it.
If provisioning fails, stop and inventory the dedicated resource group; no automatic deletion occurs.

After creation, obtain the public IP with the read-only command:

```bash
az vm show --subscription "$SUBSCRIPTION_ID" -g codeact-validation -n codeact-validation \
  --show-details --query publicIps -o tsv
ssh -i "$HOME/.ssh/id_ed25519" codeact@VM_PUBLIC_IP
```

In VS Code, use **Remote-SSH: Connect to Host**, select `codeact@VM_PUBLIC_IP`, then open the
repository on the VM. Do not copy private keys or secrets into the repository. No application ports
need to be exposed. A changed client public IP requires a separately approved NSG update.

### Bootstrap and clean clone (on the VM only)

Approve the push before these steps. Ubuntu already provides git; if it is absent, installing it
with `sudo apt-get update` and `sudo apt-get install -y git` requires approval for host changes.
Bootstrap needs a checkout to read `global.json`. Its default invocation only describes changes.
The explicit install option authorizes packages, Microsoft apt configuration, system SDK installation,
KVM module loading and user group membership on this VM, not on the laptop.

```bash
git clone --branch feature/observability-validation --single-branch \
  https://github.com/ashsenth/codeact-poc.git codeact-bootstrap
cd codeact-bootstrap
bash scripts/bootstrap-vm.sh
# Only after approving the printed host changes:
bash scripts/bootstrap-vm.sh --approved-install
```

The script installs SDK **9.0.317** from `global.json`, PowerShell 7, Azure CLI, git, KVM/native
prerequisites and the provider-matched **Hyperlight Python guest 0.4.0** from NuGet. It uses the
prebuilt `runtimes/linux-x64/native/python-sandbox.aot`; no Rust/source build is needed.
Downloads remain in ignored `artifacts/guest/`. No shell profiles are modified.
Disconnect all SSH/Remote-SSH sessions and reconnect to activate `kvm` group membership.
Then perform an independent fresh clone, record its SHA and run the locked gates:

```bash
git clone --branch feature/observability-validation --single-branch \
  https://github.com/ashsenth/codeact-poc.git codeact-clean
cd codeact-clean
RESULTS_DIR="artifacts/validation-evidence/$(date -u +%Y%m%dT%H%M%SZ)-$(cat /proc/sys/kernel/random/uuid)"
mkdir -p "$RESULTS_DIR"
git rev-parse HEAD | tee "$RESULTS_DIR/commit-sha.txt"
git status --porcelain
dotnet restore --locked-mode
dotnet build -c Release --no-restore
dotnet test -c Release --no-build --filter 'Category!=LiveModel&Category!=Hyperlight' \
  --logger trx --results-directory "$RESULTS_DIR/non-live-tests"
```

Run each command separately and **stop on any failure**. Expected **197 actually executed/passed**,
labeled **OFFLINE-CONTROL**, zero skips. Record the SHA, OS, SDK, VM size and command outputs.
This evidence, not the bootstrap checkout, closes clean-clone reproduction.

### Run the validation modes

Use the guest path printed by bootstrap (an absolute path in the bootstrap checkout):

```bash
export HYPERLIGHT_PYTHON_GUEST_PATH="$HOME/codeact-bootstrap/artifacts/guest/0.4.0/runtimes/linux-x64/native/python-sandbox.aot"
pwsh -NoProfile -File scripts/verify-observability.ps1 -Mode Offline
pwsh -NoProfile -File scripts/verify-observability.ps1 -Mode Hyperlight -PreflightOnly
pwsh -NoProfile -File scripts/verify-observability.ps1 -Mode Hyperlight
az login --use-device-code
read -r -p 'Azure OpenAI endpoint: ' AZURE_OPENAI_ENDPOINT
read -r -p 'Azure OpenAI deployment: ' AZURE_OPENAI_DEPLOYMENT_NAME
export AZURE_OPENAI_ENDPOINT AZURE_OPENAI_DEPLOYMENT_NAME
pwsh -NoProfile -File scripts/verify-observability.ps1 -Mode Live -Trials 1
```

Authentication uses Azure CLI/DefaultAzureCredential with the appropriate Azure OpenAI data-plane
role; remove unrelated credential environment variables to ensure the application uses the same
identity. An optional `AZURE_OPENAI_API_KEY` must be entered privately, never as a literal in shell
history, docs or commits. The Live preflight performs **one minimal billable model call** to verify
auth and current request capacity, not sustained quota. `-Mode Live -PreflightOnly` also performs
that call; do not run it separately unless another call is intended. API/model compatibility errors
stop validation rather than trigger retries or configuration changes.

- **OFFLINE-CONTROL**: deterministic fake model, no real Python execution, unchanged workloads.
- **LIVE / Hyperlight**: real KVM guest with scripted model inputs, all **6** runtime tests must
  actually run and pass. It does not certify live model correctness.
- **LIVE / Live**: preflight, locked build, 197 non-live passes, 6 actual runtime passes, then paired
  Small, Medium and Large comparisons sequentially. Every profile must complete with exact oracle
  matches, valid reconciliation and real `CODE_EXECUTION_COMPLETED` events without stand-in events
  before the next profile starts. Trials default to 1; full payload capture is explicit and sensitive.
- **SKIPPED**: missing prerequisites or skipped tests are never acceptance passes. Stop on failure,
  report the root cause and propose a fix before changing code. Never weaken a test or validity gate.

The script uses PATH-resolved dotnet and portable paths, preserves/restores its environment, keeps
the existing Windows WHP path and legacy `-Live` alias, and never changes execution policy.
Use a unique `-OutputRoot` under ignored `artifacts/` for every run. Each test gate also creates a
fresh timestamp/GUID results directory, even if OutputRoot is reused. It requires one TRX per
explicitly invoked project (ten non-live, one Hyperlight), reads files with literal paths and rejects
any failed/notExecuted tests or unexpected file counts. Results include TRX, per-file `counts.json`,
per-run journals and incremental `validation-summary.json`. Record latency, tokens, estimated cost,
model-facing/granular/sandbox counts, comparison IDs, commit SHA and VM size beside the existing
offline controls. Reassess all 15 review criteria as **MET**, **NOT MET**, or **BLOCKED** with
evidence. Only mark **COMPLETE** when all are MET with live evidence.

### Tear down (separate explicit approval required)

This deletes **everything** in the dedicated resource group, including disks, IP and logs. Retrieve
needed evidence privately first. Never execute teardown without approval:

```bash
az group delete --subscription "$SUBSCRIPTION_ID" --name codeact-validation --yes --no-wait
```

Deleting the dedicated group removes its ongoing VM/disk/IP charges. Deallocation alone retains
billable disks/public IP. Verify deletion with `az group exists --subscription "$SUBSCRIPTION_ID"
--name codeact-validation`.

## Local setup

```bash
cp .env.example .env          # optional; fill in only for live/Hyperlight runs
dotnet restore --locked-mode
dotnet build -c Release
dotnet run --project src/SterlingVale.DataGenerator -- data   # generate small/medium/large
```

By default (no `AZURE_OPENAI_ENDPOINT`) the apps use a **deterministic fake model** and run fully
offline.

## Run the APIs

```bash
dotnet run --project src/SterlingVale.Classic.Api      # Classic
dotnet run --project src/SterlingVale.CodeAct.Api      # CodeAct
```

Endpoints (identical for both):

| Method | Route |
| --- | --- |
| GET | `/health/live`, `/health/ready` |
| POST | `/api/v1/analyses` |
| GET | `/api/v1/analyses/{runId}` |
| GET | `/api/v1/analyses/{runId}/metrics` |
| GET | `/api/v1/analyses/{runId}/artifacts` |

```bash
curl -X POST localhost:5000/api/v1/analyses -H "content-type: application/json" \
  -d '{ "datasetProfile": "small" }'
```

## Benchmark

```bash
dotnet run --project src/SterlingVale.Benchmark -- validate-data
dotnet run --project src/SterlingVale.Benchmark -- run --profile small --trials 10 [--open]
dotnet run --project src/SterlingVale.Benchmark -- compare
dotnet run --project src/SterlingVale.Benchmark -- report --comparison-id <id> [--open]
dotnet run --project src/SterlingVale.Benchmark -- visualize [--profiles small,medium,large] [--open]
```

Each `run`/`report` writes `artifacts/comparisons/{id}.{json,md,csv,html}`; per-run artifacts land in
`artifacts/runs/{runId}/`. `visualize` stitches the latest comparison per profile into
`artifacts/comparisons/scaling-{timestamp}.html`.

### View the results

Open the generated `.html` in any browser — it is fully self-contained (inline SVG, no internet, no
JavaScript). Each dashboard is written for someone seeing the benchmark for the first time and leads
with **what actually happened** rather than raw numbers:

- **Completion badges** — how many trials each mode *finished with a valid report* (e.g. `Classic
  0/3` vs `CodeAct 3/3`), colour-coded, so a total Classic failure reads as "0/3 completed", not a
  misleading "0 ms".
- **Plain-language takeaway** — one sentence summarising the outcome for this dataset.
- **"How each mode runs"** — a short panel contrasting Classic (model calls each tool itself, one
  network round-trip per call) with CodeAct (model writes one program that loops and calls tools
  *inside* the sandbox).
- **"What happened & why"** — a data-driven observation. When Classic returns an empty report it
  reports what the data actually shows (Classic gathered the data but failed to emit a schema-valid
  report; it was not stopped by a tool-call cap or the context window) and notes that, because
  Classic did not complete, the efficiency numbers are not a like-for-like comparison.
- **Delta cards + charts** for duration / tokens / tool calls / cost — shown **only when both modes
  completed**, so you never compare against a run that produced nothing. When a mode failed, the
  affected cells read `n/a`.
- **"Every trial" table** with a *"What the run produced"* column (e.g. "exact match to the
  reference report", "gathered data but returned an empty / invalid report") and colour-coded status.

Add `--open` to launch it automatically:

```bash
dotnet run --project src/SterlingVale.Benchmark -- run --profile small --trials 10 --open
```

The `.csv` beside it has one row per trial for your own plots. Run all three profiles, then
`visualize`, to see the gap widen with dataset size. The scaling dashboard leads with a **completion
matrix** — one row per dataset (smallest → largest) with a colour-coded **Completed / Partial /
Failed** badge for each mode — so you can tell at a glance which sizes each mode finished. Charts and
per-dataset deltas follow, with deltas marked `n/a` wherever a mode never produced a comparable run.

## What we observed (live, x64 VM)

> Outcomes depend on the model, dataset, and environment — nothing below is hard-coded or
> guaranteed. These are the results from our own live runs (Azure OpenAI `gpt-4o`, 3 trials per
> profile, on an x64 Linux VM with the real Hyperlight sandbox). Reproduce them with the scripts in
> [`scripts/vm/`](scripts/vm/).

| Profile | Households | Classic | CodeAct | Classic median tokens / cost | CodeAct median tokens / cost |
| --- | --- | --- | --- | --- | --- |
| small | 10 | **Failed** 0/3 | Completed 2/3 | ~40.7k / $0.11 | ~55.8k / $0.23 |
| medium | 40 | **Failed** 0/3 | Completed 3/3 | ~33.3k / $0.09 | ~62.0k / $0.24 |
| large | 120 | **Failed** 0/3 | Completed 3/3 | ~60.5k / $0.16 | ~65.8k / $0.28 |

**What the Classic failure actually is — and what it is *not*.** In these runs Classic gathered the
data successfully (54–81 tool calls over 4–5 turns) but then returned an empty report instead of the
final `ExposureReport` — at **every** size, including the smallest (10 households). The final message
was a canned 88-byte placeholder: `{ "runId": "run_20231010_001", "generatedAt":
"2023-10-10T00:00:00Z", "households": [] }`. It was **not** stopped by a per-message tool-call cap or
the context window — it used 33–60k tokens, well under gpt-4o's 128k limit. With this model it simply
did not synthesize the gathered data into the report. Because Classic fails even at trivial scale,
**this is an output-emission failure, not a scaling result.**

**It is robust to prompting.** We added explicit anti-stub instructions to the Classic prompt —
"never emit a placeholder," "synthesize household by household," and even forbidding that exact
`run_20231010_001` runId. On a fresh live run gpt-4o produced the **identical** placeholder,
reproducing verbatim the runId it was told not to use. So this is not a prompt bug: with gpt-4o,
direct tool-calling reliably gathers data but will not assemble the final structured output, and
prompt engineering does not change it. (The ineffective prompt change was reverted.)

**Honest caveat — this is not yet a fair head-to-head.** Because Classic never produced a report, the
two modes cannot be compared on tokens, cost, latency, or tool calls: Classic's *lower* token/cost
numbers reflect that it stopped early without doing the work, and its 54–81 tool calls are
model-visible calls whereas CodeAct's real tool calls happen inside the sandbox and are not counted.
The only defensible claim from this run is that **CodeAct emitted a correct report where this Classic
setup did not.** Establishing a genuine efficiency comparison requires first getting Classic to
complete (see the open work below).

**How CodeAct produced its reports.** CodeAct asks the model to write **one program**; the looping,
tool calls, and aggregation happen *inside* the Hyperlight sandbox, so the model's context stays
small and the report is assembled by code. The one wrinkle is the sandbox's fixed ~16 KB guest→host
output buffer, which CodeAct works around by returning the report in several small `execute_code`
chunks that the model reassembles (hence its higher token count).

**Open work.** The interesting architectural claim — that CodeAct's model-visible interaction is
roughly O(1) in dataset size while direct tool-calling is O(n) — can only be *measured* once Classic
also completes. Prompt engineering does **not** get Classic there (see above), so a fair head-to-head
would require an architectural change to how Classic emits its result — e.g. a `submit_report` tool
whose parameters are the report (models fill function-call arguments far more reliably than a final
free-form JSON message), Azure **Structured Outputs** (strict `json_schema`), or incremental
per-household emission. That is deliberately left as future work.

The generated HTML dashboards state this inline, per dataset, including the caveat that the
efficiency charts are not a like-for-like comparison while one mode fails.

### Reproducing the live run on an x64 VM

The real (non-offline) numbers require an x64 host with the Hyperlight sandbox. The helper scripts in
[`scripts/vm/`](scripts/vm/) automate a run on a fresh Linux x64 VM (they assume Azure OpenAI via the
VM's managed identity):

| Script | Purpose |
| --- | --- |
| `setup-vm.sh` | Install the toolchain (.NET 9, Rust, `just`, `uv`) — run once. |
| `download-guest.sh` | Download the version-matched Hyperlight Python guest and record its path in `/root/guest_path.txt`. |
| `run-benchmark.sh` | Generate data and run the benchmark for the given profiles, detached (`bash run-benchmark.sh "medium large"`). |
| `wait-for-run.sh` | Block until the detached run finishes, then print the tail and newest artifacts. |
| `summarize.sh` | Print a compact human-readable summary of the newest comparison per profile. |



### Live comparison against Azure OpenAI

The offline fake model is a control (both modes emit the oracle, so timings are tiny). A real
Classic-vs-CodeAct comparison needs a live model. Set these in the **same shell** before running
(PowerShell: use `$env:NAME = "value"`):

```bash
az login
export AZURE_OPENAI_ENDPOINT="https://<resource>.openai.azure.com/"   # base endpoint, WITH trailing slash
export AZURE_OPENAI_DEPLOYMENT_NAME="<your-chat-deployment>"          # e.g. gpt-4o (deployment name, not model id)
# export AZURE_OPENAI_API_KEY="<key>"                                 # optional; only if key auth is enabled
dotnet run --project src/SterlingVale.Benchmark -- run --profile small --trials 10 --open
```

Common setup gotchas:
- Use the **base resource endpoint** (`https://<resource>.openai.azure.com/`), not an AI Foundry
  `.../openai/v1` URL — the wrong form returns HTTP 404 "Resource not found".
- `AZURE_OPENAI_DEPLOYMENT_NAME` is your **deployment** name (case-sensitive), not the base model id.
- For identity auth (`DefaultAzureCredential`), your account needs the **Cognitive Services OpenAI
  User** role on the resource; without it you get 403 `PermissionDenied`. Key auth may be disabled by
  policy.
- Environment variables are per-shell — set them in the same terminal you run the benchmark from.

### Making Classic completable at scale (`CLASSIC_BATCH_SIZE`)

With a live model, direct tool-calling (Classic) reliably fails to emit a report once the dataset is
large — the model gathers data but returns an empty `households` array (an output-emission failure;
the turn-by-turn evidence is in each run's `transcript.jsonl` / `tool-calls.jsonl`). To get a
*both-complete* comparison, set `CLASSIC_BATCH_SIZE` to run Classic over **small batches of
households** (one model invocation per batch, results merged); each batch is small enough that the
model emits a report, and the harness sums Classic's metrics across the batches. CodeAct is
unaffected (it always runs all households in one invocation).

```bash
export CLASSIC_BATCH_SIZE=5    # 0 / unset = single run (default); >0 = batch Classic
dotnet run --project src/SterlingVale.Benchmark -- run --profile large --trials 1 --open
```

The dashboards and the comparison JSON record the batch size, so the extra model invocations Classic
needed to keep up are explicit. Vary it to find where Classic breaks (e.g. completes at small batch
sizes, fails as the batch grows).

## Testing

```bash
# Everything except opt-in live/Hyperlight tests (this is what CI runs):
dotnet test -c Release --filter "Category!=LiveModel&Category!=Hyperlight"

# Live-model tests (opt-in; requires Azure + explicit enable):
ENABLE_LIVE_TESTS=true dotnet test --filter "Category=LiveModel"

# Hyperlight runtime tests (requires HYPERLIGHT_PYTHON_GUEST_PATH + virtualization):
dotnet test --filter "Category=Hyperlight"
```

Test kinds: **fake** (default, deterministic), **LiveModel** (opt-in, capped), **Hyperlight**
(gated on a guest). Plus unit, contract, architecture, and benchmark tests.

## Observable execution

For a single-command PowerShell validation with isolated offline settings and all three datasets:
`./scripts/verify-observability.ps1 -Trials 1`. It saves a machine-readable validation summary with
dataset sizes, statuses, latency, tokens, turns, granular tool counts, estimated costs, and correctness
scores. Use `-Live -Trials 3` only after configuring the live prerequisites below. Offline measurements
are explicitly labeled fake controls, never live performance results.

PowerShell script execution must be allowed by your organization's policy. This validation session's
host disabled script files; do not weaken that policy. The individual `dotnet` commands below can
still be run from an approved terminal. A clean remote clone has not yet been certified.

Run these commands from the repository root. The SDK is pinned to 9.0.317 with feature roll-forward;
9.0.320 was used for local validation. Azure and Hyperlight settings are read from the process
environment; copying `.env.example` does not automatically load its values into your shell.

PowerShell offline control, with explicit isolation from any live settings:

```powershell
$env:AZURE_OPENAI_ENDPOINT = ""
$env:HYPERLIGHT_PYTHON_GUEST_PATH = ""
$env:BENCHMARK_REQUIRE_LIVE = "false"
dotnet restore --locked-mode
dotnet build -c Release
dotnet test -c Release --filter "Category!=LiveModel&Category!=Hyperlight"
dotnet run -c Release --project src/SterlingVale.DataGenerator -- data
dotnet run -c Release --project src/SterlingVale.Benchmark -- compare --trials 1 --log-level normal --payload-capture full
```

This is a deterministic fake-model control, NOT a performance comparison of live agents. The offline
`execute_code` function executes no Python. Live setup on an x64 host with KVM/MSHV or WHP:

```powershell
az login
$env:AZURE_OPENAI_ENDPOINT = "https://<resource>.openai.azure.com/"
$env:AZURE_OPENAI_DEPLOYMENT_NAME = "<deployment>"
$env:HYPERLIGHT_PYTHON_GUEST_PATH = "<absolute-path-to-version-matched-python-guest>"
$env:BENCHMARK_REQUIRE_LIVE = "true"
$env:BENCHMARK_MAX_TURNS = "40"
$env:BENCHMARK_TIMEOUT_SECONDS = "600"
dotnet test -c Release --filter "Category=Hyperlight"
dotnet run -c Release --project src/SterlingVale.Benchmark -- compare --trials 3 --log-level verbose --payload-capture full
```

Use the version-matched guest from `scripts/vm/download-guest.sh` on Linux. For Windows, obtain
the corresponding win-x64 guest from the same package version, and enable WHP yourself. Do not
substitute a guest built from a different API version. Identity authentication requires the Cognitive
Services OpenAI User role; API-key authentication is optional and keys must stay outside artifacts.
`BENCHMARK_REQUIRE_LIVE=true` refuses missing Azure or guest configuration instead of substituting
the offline path. It does not prove that virtualization, authentication, quota, or the guest works.

`--log-level minimal|normal|verbose|debug` controls terminal rendering of the same events written
to disk. `--payload-capture none|metadata|full` defaults to `metadata`; verbose/debug do not override
that choice. For APIs use `EXECUTION_LOG_LEVEL` and `EXECUTION_PAYLOAD_CAPTURE`. Full capture is
explicitly for synthetic demonstrations, not a production default. Payloads are limited to 65,536
characters, redacted, and carry truncation flags. A redacted/truncated program is marked as an
unavailable exact capture rather than saved as an apparently exact Python program.

Each run has unique run/correlation IDs, comparison/trial IDs, W3C trace/span IDs, and a flushed
`events.jsonl` journal. Artifacts include `timeline.json`, `timeline.md`, model request/response
JSONL, direct-tool invocation JSONL, and applicable `classic/` or `codeact/` files. Full mode preserves
each safe, bounded original program in `codeact/generated-code-NNN.py` before invoking the tool.
Classic final-response files label model content separately from application-normalized results.
Batch aggregate metadata references independently persisted child runs and labels the aggregate
as application-merged. Model projection excludes reasoning-content objects.

Comparison summaries are also written to `artifacts/comparisons/{comparisonId}/summary.{json,md}`.
All three profiles retain their original sizes. Missing token metadata remains null. Reports expose
separate model-facing tool, granular invocation, and sandbox callback counts; the legacy
`ToolCallCount` now means granular invocations. Telemetry inconsistency causes a nonzero benchmark
exit. Completion, correctness, fairness, and telemetry validity are distinct checks.

Filter the source journal with PowerShell (replace the run ID):

```powershell
$events = Get-Content "artifacts/runs/<runId>/events.jsonl" | ForEach-Object { $_ | ConvertFrom-Json }
$events | Where-Object eventType -eq TOOL_CALL_COMPLETED | Sort-Object sequenceNumber
$events | Where-Object { $_.data.toolName -eq "get_price" }
$events | Where-Object { $_.data.modelTurn -eq 1 }
```

The current path uses direct AIFunction callbacks, not MCP. Hyperlight internal initialization,
serialization, cleanup timings, and host resource counters are unavailable. stdout/stderr are
captured only when the public tool result exposes them. Real-guest tests use a deterministic model
to request actual sandbox execution; they are not fake sandbox tests. Streaming model execution is
not used by the shared runner and is not certified by this instrumentation.

## Metrics definitions

| Metric | Meaning |
| --- | --- |
| model turns / requests | assistant turns and model requests in a run |
| tool calls / by tool | granular tool invocations |
| `execute_code` calls | CodeAct sandbox invocations (0 for Classic) |
| input/output/total tokens | usage; **null when the provider does not report it** |
| estimated cost | tokens × configured pricing, or null when usage unknown |
| schema valid | final output conformed to `exposure-report/v1` |
| flagged/breach precision & recall | vs the oracle |
| allocation / trade-notional MAE | mean absolute error vs the oracle |
| match class | Exact / WithinTolerance / Partial / InvalidSchema / Failed / TimedOut / Capped |

Computation rules live in [docs/metrics.md](docs/metrics.md).

## Fairness methodology

Prompts, schemas, tool definitions, model configuration, pricing, and dataset manifests are
SHA-256 hashed. A comparison **fails** if the paired modes' fingerprints differ. Trials are paired,
mode order is randomized (seed recorded), one warm-up per mode is discarded, and no trial is ever
dropped. Full detail: [docs/benchmark-methodology.md](docs/benchmark-methodology.md).

## Expected variability

With the offline fake model, both modes emit the oracle result — identical correctness, tiny
timings (a control, not a performance claim). Real differences require a live model and vary by
model, dataset, and environment. **No performance outcome is guaranteed or hard-coded.**

## Troubleshooting

| Symptom | Fix |
| --- | --- |
| `/health/ready` returns 503 | generate data (`DataGenerator`) or set `DATASET_ROOT` |
| Benchmark: "profile not available" | run the data generator first |
| Live: HTTP 404 "Resource not found" | use the base `https://<resource>.openai.azure.com/` endpoint and the exact deployment name |
| Live: 403 `PermissionDenied` | assign the **Cognitive Services OpenAI User** role to your identity on the resource |
| Hyperlight tests skipped | set `HYPERLIGHT_PYTHON_GUEST_PATH` and ensure virtualization |
| Live tests skipped | set `ENABLE_LIVE_TESTS=true` and configure Azure OpenAI |
| `NU1403`/locked restore fails | run `dotnet restore` to refresh lock files |

## Current preview limitations

`Microsoft.Agents.AI.Hyperlight` is preview-only. Whether the SDK exposes the model-generated code
is version-dependent; when unavailable, `generated-code.txt` contains an explicit "unavailable"
marker. The number of `execute_code` calls is **measured**, never assumed. See
[docs/sdk-findings.md](docs/sdk-findings.md) and [docs/decisions.md](docs/decisions.md).

## Security

Generated code runs only inside the Hyperlight micro-VM (never the host); network denied, no host
filesystem mounted, fresh sandbox per run, disposed afterward. See
[docs/security-model.md](docs/security-model.md) and [SECURITY.md](SECURITY.md).

## License

MIT — see [LICENSE](LICENSE).