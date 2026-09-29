# Sterling Vale CodeAct

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
data successfully (54–81 tool calls over 4–5 turns) but then returned an empty / schema-invalid
report instead of the final `ExposureReport` — at **every** size, including the smallest (10
households). It was **not** stopped by a per-message tool-call cap or the context window: it used
33–60k tokens, well under gpt-4o's 128k limit. With this model and prompt it simply failed to emit
the final structured output. Because Classic fails even at trivial scale, **this is an
output-emission failure, not a scaling result.**

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
also completes. Making the Classic agent reliably emit a schema-valid report (a prompt/harness fix,
not a fundamental limit) is the next step toward a fair comparison.

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