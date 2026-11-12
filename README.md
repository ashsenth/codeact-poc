# Sterling Vale: Classic tool-calling vs. CodeAct

A .NET proof of concept comparing two ways an AI agent can analyze the same synthetic
wealth-management portfolio: call data tools directly, or write Python that calls those tools
inside a Hyperlight sandbox. The project includes reproducible datasets, a reference calculation,
paired comparisons, and inspectable execution artifacts. **The offline demo works on ARM64
without Azure or a sandbox; real model and Hyperlight results remain unverified on this branch.**
This is a POC, not a production service or investment advice.

## 1. Goal

Does CodeAct-style code execution improve **accuracy, tokens, latency, estimated cost, or
number of calls** compared with classic tool-calling as the dataset grows?

The intended comparison holds data, seven underlying tools, task instructions, output schema,
model configuration, and pricing constant. The orchestration changes.
The shared prompt body is fingerprinted; each mode also has a different mechanism addendum.
Matching fingerprints checks configuration consistency, not scientific equivalence by itself.

**No winner is assumed.** Offline controls answer a narrower question: does the comparison
harness produce correct reports and internally consistent telemetry?

## 2. Premise & background

| Term | Plain-English meaning |
| --- | --- |
| Classic tool-calling | The model requests named functions with structured arguments, receives results, and decides what to do next. A response can contain multiple tool calls; one call does not necessarily mean one network round-trip. |
| CodeAct | The model writes and runs code that calls tools, loops, and computes intermediate results. This can move work out of repeated model interactions. |
| Hyperlight | An embedded, hypervisor-isolated micro-VM for untrusted code. Here it hosts the Python guest; it needs supported x64 hardware and virtualization. |
| Microsoft Agent Framework | The .NET agent layer managing model interactions and function invocation. Both modes use it; CodeAct additionally uses its Hyperlight provider. |

Why compare them? Larger portfolios require repeated retrieval, arithmetic, and structured
output. Code may make that work easier to express, but generated code, sandbox overhead,
output limits, and model errors can offset the benefit. Measure correctness before efficiency.
The current CodeAct prompt requests multiple small code executions to accommodate guest output
limits; this implementation does **not** promise one program or constant model calls.

Background sources:
[function calling](https://platform.openai.com/docs/guides/function-calling),
[CodeAct paper](https://arxiv.org/abs/2402.01030),
[Hyperlight](https://github.com/hyperlight-dev/hyperlight), and
[Microsoft Agent Framework](https://learn.microsoft.com/en-us/agent-framework/overview/).
The pinned preview APIs are recorded in [SDK findings](docs/sdk-findings.md).

## 3. The scenario

**Sterling Vale** is a fictional wealth manager. Its synthetic households own accounts and
positions across asset classes and currencies. Seeded generation produces these sizes:

| Profile | Households | Accounts | Positions |
| --- | ---: | ---: | ---: |
| Small | 10 | 33 | 370 |
| Medium | 40 | 156 | 2,201 |
| Large | 120 | 485 | 7,198 |

For each household, the agent should:

1. Retrieve its policy, accounts, positions, prices, FX rates, and classifications.
2. Value positions in the household's base currency and compute allocations.
3. Flag drift, concentration, foreign-currency, and crypto policy breaches.
4. Propose asset-class buy/sell notionals, not named securities.
5. Return a schema-valid `ExposureReport`.

The deterministic **oracle** independently calculates the reference report for live scoring.
It checks flagged households, breaches, allocations, and trade notionals using documented
rounding and tolerances. It is not exposed as an agent tool.
**Offline only:** the fake model's answer is deliberately generated from this same oracle,
so an exact match validates plumbing, not independent model reasoning.
See [calculation rules](docs/metrics.md) and [comparison methodology](docs/benchmark-methodology.md).

## 4. How it works

```mermaid
flowchart TD
    Data["Seeded datasets"] --> Snapshot["Shared portfolio snapshot"]
    CLI["Benchmark CLI: paired trials"] --> Shared["AnalysisService / AnalysisRunner"]
    APIs["Classic.Api / CodeAct.Api"] --> Shared
    Shared --> Classic["Classic: model calls functions"]
    Shared --> Code["CodeAct: model requests execute_code"]
    Code --> Sandbox["Live x64: Hyperlight Python guest"]
    Sandbox --> Tools["Seven read-only host tools"]
    Classic --> Tools
    Snapshot --> Tools
    Code -. "No guest: no-code stand-in" .-> Fake["Offline fake-model control"]
    Shared --> Evidence["Events, timelines, results, metrics"]
    Snapshot --> Oracle["Deterministic oracle"]
    CLI --> Score["Correctness + fairness + telemetry"]
    Oracle --> Score
    Evidence --> Score
    Score --> Reports["JSON / Markdown / CSV / HTML"]
```

The CLI generates one oracle reference per profile, warms up each mode once, randomizes paired
execution order, runs both modes, scores their reports, and writes artifacts. Warm-ups are
excluded from measured comparisons. Both APIs expose the same analysis and run-retrieval contract.
Tools use direct `AIFunction` callbacks, **not MCP**.

### Source projects

All names below live under [src/](src/), with the `SterlingVale.` prefix.

| Project suffix | Responsibility |
| --- | --- |
| Domain | Portfolio model, money, financial math, reports, and metrics. |
| Application | Portfolio snapshots, FX conversion, and the reference oracle. |
| Tools | Seven read-only retrieval/classification functions and invocation recording. |
| AgentShared | Prompts, schemas, shared runner/service, Classic factory, and fake model. |
| CodeAct | Hyperlight agent factory and explicit no-guest stand-in. |
| Infrastructure | Dataset loading, Azure/fake client selection, DI, and file run storage. |
| Telemetry | Correlation, ordered event journals, payload policy, timelines, and console output. |
| Api.Shared | Shared HTTP endpoints, validation, and response mapping. |
| Classic.Api | Thin ASP.NET host for the Classic factory. |
| CodeAct.Api | Thin ASP.NET host for the CodeAct factory. |
| DataGenerator | Seeded Small/Medium/Large data and hash manifests. |
| Benchmark | Paired execution, scoring, aggregation, CLI, and report rendering. |

### Test projects

All names below live under [tests/](tests/), also prefixed `SterlingVale.`.

| Project suffix | Coverage |
| --- | --- |
| Domain.Tests | Value objects, math, and invariants. |
| Application.Tests | Oracle, FX, and portfolio calculations. |
| Data.Tests | Deterministic generation, loading, and manifests. |
| Tools.Tests | Tool contracts, recording, event isolation, payloads, and timelines. |
| AgentShared.Tests | Runner limits, fake clients, schemas, artifacts, and reconciliation. |
| Architecture.Tests | Dependency boundaries. |
| Contract.Tests | Cross-mode HTTP/schema/tool consistency. |
| Classic.IntegrationTests | Classic API using offline clients, plus opt-in live tests. |
| CodeAct.IntegrationTests | Offline API and separately gated real-Hyperlight tests. |
| Comparison.Tests | Scoring, aggregation, reports, and all-profile offline comparisons. |

More detail: [architecture](docs/architecture.md) and [security model](docs/security-model.md).

## 5. Quickstart: offline demo

### Prerequisites

- Git and .NET 9 SDK per [global.json](global.json); validated with SDK **9.0.320** and configured roll-forward.
- A terminal running PowerShell: Windows PowerShell works here; PowerShell 7 is available
  for Windows, Linux, and macOS. No Python, Azure account, guest image, or virtualization required.
- Package-feed access for the first restore. Allow a few minutes for initial downloads.

**Verification scope:** the commands below succeeded in a clean local-source clone on Windows
ARM64. Linux/macOS execution and a remote-origin clone are **not yet verified**.

Clone setup template (**remote clone not yet verified**; replace the placeholder):

```powershell
git clone --branch feature/observability-validation <repository-url> codeact-poc
Set-Location codeact-poc
```

From the repository root, paste these into one terminal. The explicit environment settings
prevent inherited Azure, guest, batching, pricing, or output-directory settings changing the demo:

```powershell
$env:AZURE_OPENAI_ENDPOINT = ""
$env:HYPERLIGHT_PYTHON_GUEST_PATH = ""
$env:BENCHMARK_REQUIRE_LIVE = "false"
$env:CLASSIC_BATCH_SIZE = "0"
$env:DATASET_ROOT = "data"
$env:ARTIFACTS_ROOT = "artifacts"
$env:BENCHMARK_MAX_TURNS = "12"
$env:BENCHMARK_TIMEOUT_SECONDS = "120"
$env:PRICING_INPUT_PER_MILLION = "2.50"
$env:PRICING_OUTPUT_PER_MILLION = "10.00"
$env:ENABLE_LIVE_TESTS = "false"
$env:OTEL_EXPORTER_OTLP_ENDPOINT = ""
$generator = Join-Path src SterlingVale.DataGenerator
$benchmark = Join-Path src SterlingVale.Benchmark
dotnet restore --locked-mode
dotnet build -c Release --no-restore
dotnet run -c Release --no-build --project $generator -- data
dotnet run -c Release --no-build --project $benchmark -- validate-data
dotnet run -c Release --no-build --project $benchmark -- compare --trials 1 --model fake --log-level normal --payload-capture full
dotnet run -c Release --no-build --project $benchmark -- visualize
```

Stop on failure; an old dashboard is not evidence of success. `compare` runs all available profiles;
`visualize` combines the latest per profile. Its printed HTML path opens without external assets.

Actual output excerpt, with changing comparison ID omitted:

```text
[small] VALID — 10 households, 33 accounts, 370 positions; on-disk manifest present
[medium] VALID — 40 households, 156 accounts, 2201 positions; on-disk manifest present
[large] VALID — 120 households, 485 accounts, 7198 positions; on-disk manifest present
Model selected: fake. Reason: explicit fake control; Azure environment variables are ignored.
Completion: 2/2; correctness (Exact/WithinTolerance): 2/2
COMPARISON VALID
Execution kind: offline-fake-control
Fairness: MATCHED; telemetry: VALID (accounting only, not success)
```

Optional verification, also run successfully:

```powershell
dotnet test -c Release --no-build --filter "Category!=LiveModel&Category!=Hyperlight"
```

Result: **221 passed, 0 failed, 0 skipped across ten projects**, including diagnostic, mode,
scope, capture and normalization regressions. The separate Hyperlight group discovered six tests;
all six skipped here, with zero executed. A skip is not sandbox evidence.

## 6. What the results show

### Fake-model controls, NOT real performance

This laptop's fake client makes one representative tool request, then emits a precomputed oracle
report. Classic calls `list_households`; CodeAct calls a stand-in `execute_code` that runs
**no Python** and invokes **no data callbacks**. Neither mode performs live model analysis.

Fresh local-clone run at `2026-11-06T00:30Z` (November 5 local time), one measured pair per
profile after warm-up; values below were read from the generated comparison JSON:

| Profile | Oracle match, both | Fairness / telemetry | Fake tokens, each mode | Formula USD, each mode | Harness ms, Classic / CodeAct |
| --- | --- | --- | ---: | ---: | ---: |
| Small | Exact | MATCHED / VALID | 4,174 | 0.041425 | 20.1399 / 17.7399 |
| Medium | Exact | MATCHED / VALID | 16,230 | 0.161970 | 40.9000 / 40.1731 |
| Large | Exact | MATCHED / VALID | 49,208 | 0.491765 | 113.1979 / 106.6385 |

For every profile: two model turns per mode, one model-facing invocation per mode,
Classic/CodeAct granular calls **1/0**, `execute_code` invocations **0/1**, sandbox callbacks **0/0**.
Both modes had flagged/breach precision and recall 1, allocation/trade error 0.
Fake tokens and formula costs are identical; wall-clock timings vary.
**These numbers validate the harness and telemetry, not a speedup, model accuracy, or Azure bill.**

| Metric | Interpretation |
| --- | --- |
| Correctness | Schema validity plus oracle match class, flagged/breach precision and recall, allocation MAE, and trade-notional error as a household-value fraction. |
| Duration | Reported trial wall time around analysis; includes observation/persistence inside the service, excludes warm-up and later report rendering. Not model-only or micro-VM CPU time. |
| Tokens | Per-response input/output/total usage aggregated across the run; unknown remains null. Offline usage is synthetic. |
| Estimated USD | Input/output tokens times configured per-million prices; not an invoice. |
| Model turns | Assistant responses observed at the model boundary; distinguish them from function invocations. |
| Model-facing calls | Functions requested by the model: granular tools for Classic, `execute_code` for CodeAct. |
| Granular calls | Actual host data-tool invocations, including real CodeAct callbacks; legacy `toolCallCount` now means this. |
| Sandbox callbacks | Granular calls originating from CodeAct; zero in this no-guest control. |
| Telemetry valid | Events, invocation counts, and token totals reconcile. It does not imply correctness or real sandbox execution. |

### Reports and evidence

| Relative location | Contents |
| --- | --- |
| `<ARTIFACTS_ROOT>/index.md` | Comparisons and runs, status, warm-up labels, and available timeline/code/tool-invocation links. |
| `artifacts/comparisons/{id}.{json,md,csv,html}` | Raw trials, summaries, scores, fingerprints, execution kind, and dashboards. |
| `artifacts/comparisons/{id}/summary.{json,md}` | Additional comparison summary. |
| `artifacts/comparisons/scaling-{timestamp}.html` | Latest-per-profile overview; check execution kinds before mixing runs. |
| `artifacts/runs/{runId}/result.json`, `metrics.json`, `metadata.json` | Structured result, metrics, identity, and configuration. |
| `events.jsonl`, `timeline.json`, `timeline.md` within each run | Ordered boundary events and their references. |
| `model/`, `tools/`, `classic/`, `codeact/` within each run | Requests/responses, invocations, final-response captures, generated programs, and available streams. |

Default payload capture is metadata only; live generated Python is saved independently of this mode.
The demo uses `full`; payloads/source are redacted and capped at 65,536 characters. Exact source must be safe and complete.
Use capture flags, not assumptions about a missing stream or source file.
`transcript.jsonl` retains every message and call/result identity. For observed runs, text, arguments
and results are separate capture envelopes with content/length/truncation/redaction fields; large
payloads can be partial, but no whole-file cutoff removes later calls or breaks JSONL framing.
Separate generated Python captures still require an unredacted, untruncated program for a `.py` file;
otherwise a `.capture.json` explicitly marks the unavailable exact source.
Current evidence and IDs: [POC demo review](docs/review-current/poc-demo-review.md).

Completion and oracle correctness come first in console/JSON/HTML summaries. If any measured run
fails or is not executed, the comparison says `COMPARISON INVALID: N of M runs failed (<category>)`,
exits nonzero, and suppresses aggregate medians/deltas/charts; raw attempts remain available.
Telemetry can reconcile a failed request: **VALID accounting is not successful analysis**.
Warm-ups are persisted and labelled **WARM-UP, not measured**. Normal logs show full IDs at
RUN STARTED, then an eight-character run prefix; verbose/debug and disk journals retain full IDs.

## 7. Running live

**Live ARM64, November 11:** connectivity works. New scoped trials below are oracle-scored,
not performance claims. Classic's schema-valid reports were **Partial**, not correct; CodeAct
generated Python but was **NotExecuted: no Hyperlight guest**. Both comparisons returned exit 1.
Configure these variables in the same terminal; do not put real values in source or artifacts:

| Variable | Purpose |
| --- | --- |
| `AZURE_OPENAI_ENDPOINT` | HTTPS resource address; pasted `/openai/v1` paths are normalized to the base with a one-line notice. |
| `AZURE_OPENAI_DEPLOYMENT_NAME` | Exact chat deployment name, not merely a model identifier. |
| `AZURE_OPENAI_API_KEY` | Optional alternative to `DefaultAzureCredential`; keep outside artifacts. |

The benchmark defaults to `--model fake`, **even with Azure variables set**.
Only `--model live` opts in; missing live configuration never falls back to fake.
APIs also default to fake; opt in with `--model live` or `MODEL_MODE=live`.
Copying [.env.example](.env.example) does not load shell variables.

Identity/deployment setup templates below are **not yet verified/executed**; replace placeholders:

```powershell
az login --tenant <tenant-id>
az account set --subscription <subscription-id>
az cognitiveservices account deployment list --resource-group <resource-group> --name <resource-name> --query "[].name" --output tsv
```

Use the **deployment name**, not the model name. Example base: `https://<resource-name>.openai.azure.com/`.
The SDK uses this authority with `/`; path/query/fragment are removed, not silently sent as a second route.
An authorized administrator can grant **Cognitive Services OpenAI User** at the resource scope;
see [exact RBAC steps and error diagnosis](docs/review-current/poc-demo-review.md#azure-setup-templates).
No login, role assignment or resource change was performed by this review.

Laptop connectivity/generation check, **executed but not yet verified as a successful comparison**:

```powershell
$env:BENCHMARK_REQUIRE_LIVE = "false"
$env:HYPERLIGHT_PYTHON_GUEST_PATH = ""
$env:BENCHMARK_MAX_TURNS = "20"
$env:BENCHMARK_TIMEOUT_SECONDS = "120"
$benchmark = Join-Path src SterlingVale.Benchmark
dotnet run -c Release --no-build --project $benchmark -- run --profile small --trials 1 --households 1 --model live --log-level normal --payload-capture full
dotnet run -c Release --no-build --project $benchmark -- run --profile small --trials 1 --households 3 --model live --log-level normal --payload-capture full
```

`--households N` selects the first N dataset households for both agents, warm-ups and oracle; default is all.
Measured runs after warm-up, **live ARM64**, no prompt changes. CodeAct tokens: **first turn only, code not executed: not comparable to Classic totals**.

| N | Classic status / finish / turns | Input / output tokens | Duration ms | Oracle | CodeAct turns / input / output / Python bytes |
| ---: | --- | ---: | ---: | --- | --- |
| 1 | Completed / stop / 6 | 20,766 / 920 | 13,933.371 | Partial | 1 / 1,911 / 78 / 139 |
| 3 | Completed / stop / 5 | 35,776 / 961 | 12,052.803 | Partial | 1 / 1,919 / 1,208 / 4,614 |

Both Classic runs skipped `asset_class`. Full per-tool counts, final previews and historical capture
limits are in [scoped live findings](docs/review-current/poc-demo-review.md#scoped-live-arm64-follow-up).
Finish reason, last-response output tokens, a redacted 240-character preview and zero-inclusive counts
are persisted in each run's `summary.json`, final event and comparison; generated-source events include byte counts.
Safe live Python lands in `codeact/generated-code-*.py` even with metadata/none payload capture;
redaction/size bounds still produce explicit markers. Python is never executed by the laptop fallback.
Failure events contain a safe category, HTTP status/allowlisted service code and sanitized guidance,
not raw provider error text. Live calls incur charges; old journals cannot recover omitted messages.

An x64 guest adds **actual Python execution, host callbacks and a scoreable CodeAct report**.
Pass all six runtime tests first, then use explicit live mode for Small/Medium/Large pairs.
The optional `BENCHMARK_REQUIRE_LIVE=true` strict gate refuses a missing guest; leave it false
for laptop code generation. It does not prove auth, capacity or correctness.

`CLASSIC_BATCH_SIZE` optionally decomposes Classic into household subsets and application-merges
their outputs. Default zero leaves it disabled. Batched trials are a separate experimental variant,
not an unchanged head-to-head; the aggregate references child runs and is not one model answer.

## 8. Running real Hyperlight on x64 (optional)

**Not yet verified; VM work is paused.** Use an x64 Linux host with accessible KVM/MSHV or
Windows x64 with WHP, plus the version-matched Python guest. ARM64 is not a supported substitute.
There is no claim that an ordinary container or hosted development environment supplies virtualization.

See the [x64 workflow](docs/review-current/x64-validation.md), approval-gated
[provisioning](scripts/provision-vm.sh), [bootstrap](scripts/bootstrap-vm.sh),
[validation helper](scripts/verify-observability.ps1), and older [VM helpers](scripts/vm/).
Nothing in the offline quickstart provisions resources, installs a guest, or changes execution policy.

## 9. Project status

| Area | Status and evidence |
| --- | --- |
| Offline POC demo | Working: clean local clone, locked restore, Release build, all profiles and reports; current suite has 221 passing tests. |
| HTTP contract | Offline integration tests pass; a standalone live Classic request completed for one household, with valid schema/telemetry but no oracle score. |
| Observability | Working offline: correlation, journals, capture policy, timelines, counts, reconciliation. |
| Large payload capture | Bounded payloads with explicit flags; transcript records remain valid JSONL and retain later calls. Exact oversized payloads remain unavailable. |
| Batched Classic | Offline tested; live quality and the changed comparison methodology remain unverified. |
| Live Azure OpenAI | Scoped N=1/N=3 Classic Completed but oracle Partial; CodeAct Python saved, not executed. No valid live comparison. |
| Real Hyperlight | Six tests skipped here; x64 execution, callbacks, cancellation, and isolation unverified. |
| Remote clone / Linux / macOS | Clean local-source clone verified; remote clone and other operating systems pending. |
| Provision/bootstrap scripts | Reviewed, not executed; provisioning remains paused. |

The original [observability acceptance audit](docs/review-current/execution-observability.md)
retains the TRX recount, acceptance matrix, and SKU findings as historical deep detail.
[Historical live notes](docs/review-current/historical-live-notes.md) retain the old README claims
with source commits/dates and **NOT RE-VERIFIED** labels, not as current verified results.

## 10. Next steps

1. **Real Hyperlight on supported x64:** run all six guest tests; establish actual execution rather than a stand-in.
2. **Live Small, Medium, Large pairs:** retain every trial and inspect oracle/telemetry scores; answer the POC's real question.
3. **Remote clean-clone check:** repeat the quickstart with an empty checkout/package environment; local cached restore is narrower evidence.
4. **Linux script verification:** fix the SSH-key regex rejecting valid ECDSA keys such as `ecdsa-sha2-nistp256`, then test approved setup/validation on x64; static review is not a KVM test.
5. **Exercise new transcript captures live:** JSONL framing is fixed and regression-tested offline; confirm the per-field capture envelopes with real generated Python and callbacks on x64.
6. **Revisit Classic batching only if needed:** measure the unbatched baseline first; any decomposition changes the experiment and needs an explicit label.
7. **Verify dashboard appearance and CodeAct API hosting:** reports and scoped Classic serving passed; browser appearance and standalone CodeAct serving remain unchecked.
8. **Investigate live Classic correctness:** scoped reports stop normally but score Partial and skip classification; prompts stay unchanged pending approval.
9. **Monitor the fixed telemetry-listener race:** `TelemetryRedactionTests.Tool_metrics_and_activity_expose_only_whitelisted_tags` now isolates its trace and uses concurrent collectors; keep parallel stress coverage to prevent recurrence.

## 11. Troubleshooting

| Symptom | What to do |
| --- | --- |
| ARM64 / no guest; six Hyperlight tests skip | Expected on this laptop. Leave the guest unset for offline use; move real execution to supported x64. |
| PowerShell script blocked by policy | Paste the individual quickstart commands into an approved terminal. Do not change execution policy or bypass it. |
| OneDrive paths contain spaces or files are locked | Run from the repo root, quote paths, and avoid concurrent writers. The local clone check used an unsynchronized workspace. |
| TRX total looks too high | Use a fresh results directory and literal-path reads for filenames containing brackets; clear the XML variable before each read. Historical 297 was an invalid recount of 197. |
| Profile unavailable / readiness 503 | Generate data first. Standalone APIs resolve relative roots under their project content root; supply absolute data/artifact roots. |
| Old transcript cannot be parsed | Regenerate artifacts after the framing fix. Old files are not rewritten; new per-field capture envelopes explicitly flag partial payloads. |
| Azure 404 / 403 | 404 was reproduced with a path-bearing endpoint; base address fixed connectivity. For 403 check the resource-scoped OpenAI User role and networking. |
| Azure configured but fake selected | Intentional safe default. Pass `--model live`; environment variables alone never opt in. |
| INVALID comparison with VALID telemetry | The request accounting reconciled, but analysis failed/not executed. Read category/status and timeline; no medians are published. |
| Dashboard disagrees with a recent run | Check comparison ID, execution kind, and timestamp. Old reports are not rewritten automatically. |
| Locked restore fails | Check SDK selection and authorized feed access; do not silently regenerate lock files to conceal a failure. |

License: [MIT](LICENSE). Security boundaries and reporting: [SECURITY.md](SECURITY.md).
