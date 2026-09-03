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
- For **CodeAct runtime**: Hyperlight requirements below.

### Hyperlight virtualization requirements

CodeAct's `execute_code` runs generated code inside a **Hyperlight micro-VM**, which needs
**hardware virtualization** on the host (e.g. KVM on Linux / WHP on Windows) and a guest module path
in `HYPERLIGHT_PYTHON_GUEST_PATH`. When that variable is **unset**, the CodeAct API and benchmark run
with an offline `execute_code` stand-in so everything still works without virtualization — and the
Hyperlight runtime tests **skip** with an explicit reason. No Docker image is provided because
Hyperlight requires host virtualization that is not portable across containers.

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
dotnet run --project src/SterlingVale.Benchmark -- run --profile small --trials 3
dotnet run --project src/SterlingVale.Benchmark -- compare
dotnet run --project src/SterlingVale.Benchmark -- report --comparison-id <id>
```

Outputs land in `artifacts/runs/{runId}/` and `artifacts/comparisons/{id}.{json,md}`.

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