# Architecture

Sterling Vale CodeAct is a controlled A/B comparison of two agent orchestrations over an
**identical** application: **Classic** direct tool-calling vs **CodeAct** via
`HyperlightCodeActProvider`. Everything except the orchestration wiring is shared.

## Dependency direction

```
        Domain  ◄─ Application ◄─ Tools ─────────────┐
          ▲            ▲            ▲                 │
          │            │            │                 │
          └── (pure) ──┘        AgentShared ◄─────────┤
                                    ▲    ▲            │
                Telemetry ──────────┘    │            │
                                         │            │
                    Infrastructure ◄─────┤     SterlingVale.CodeAct
                         ▲               │            ▲
                         │               │            │
        Api.Shared ◄─────┼───────────────┘            │
             ▲           │                            │
             │           │                            │
   Classic.Api ──────────┘             CodeAct.Api ───┘
                                       Benchmark ──────┘
```

- **Domain** — pure value objects, immutable model, `FinancialMath`. Zero references (enforced by
  `ArchitectureTests`).
- **Application** — the deterministic **oracle**, portfolio snapshot, and FX converter. References
  only Domain.
- **Tools** — the canonical seven granular tools + `ToolCatalog`. References Application + Telemetry.
- **AgentShared** — shared prompt, output schema, contracts, `AnalysisRunner`, `AnalysisService`,
  `ClassicAgentFactory`, deterministic fake model, fairness fingerprints.
- **SterlingVale.CodeAct** — `CodeActAgentFactory` (Hyperlight). Isolated so the console benchmark
  can use it without the ASP.NET host.
- **Infrastructure** — dataset loading, chat-client providers (Azure / fake), `FileRunStore`, DI.
- **Api.Shared** (namespace `SterlingVale.Api.Hosting`) — the single endpoint mapping used by both
  APIs, so the HTTP contract cannot diverge.
- **Classic.Api / CodeAct.Api** — thin composition roots (~15-line `Program.cs`). Each registers only
  its mode-specific `IAgentFactory`.
- **Benchmark** — paired-trial harness, correctness scoring, comparison reporting.

## The single orchestration seam

`IAgentFactory` is the only place the two modes differ:

| | Classic | CodeAct |
| --- | --- | --- |
| Tools | registered directly on the agent | provider-owned via `HyperlightCodeActProvider` |
| Model surface | the 7 tools | `execute_code` (tools reached via `call_tool`) |
| Tool fingerprint | hash of the 7 tools | **same** hash of the 7 tools |

Everything downstream — prompt, schema, dataset, model config, pricing, `AnalysisRunner`, metrics,
artifacts, HTTP contract — is shared.

## Request flow

`POST /api/v1/analyses` → validate request against `analysis-request/v1` schema → `AnalysisService`
resolves the snapshot, builds the shared tool service, computes fairness fingerprints, creates the
mode's agent, and runs it through `AnalysisRunner` (usage aggregation, turn/tool counting, timeout,
turn cap, schema validation, raw-output capture) → `FileRunStore` persists artifacts → response DTO.

## Observability

Each run has distinct immutable run/correlation IDs, paired comparison/trial IDs and W3C trace/span
IDs. The run ID names `artifacts/runs/{runId}/`. `ExecutionSession` synchronously flushes boundary
events to an append-only journal and renders that same event stream at the requested verbosity.
The API response and outgoing model HTTP requests also carry correlation headers.

`FileRunStore` writes these per-run artifacts (built by the pure `RunArtifacts`):

| File | Contents |
| --- | --- |
| `result.json` / `metrics.json` / `metadata.json` | response DTO, metrics, and run/config metadata |
| `events.jsonl` | actual ordered run/model/function/tool boundary events, retained on failure |
| `timeline.json` / `timeline.md` | ordered views with journal references |
| `model/requests.jsonl`, `model/responses.jsonl` | per-request/per-response capture envelopes and usage |
| `tools/invocations.jsonl` | granular host invocations, including real CodeAct callbacks |
| `classic/model-turn-NNN.json`, `classic/final-response.json` | model captures, distinct from normalized application output |
| `codeact/generated-code-NNN.py` | individual exact programs when full capture is safe and complete; otherwise a `.capture.json` envelope |
| `codeact/execution-NNN.json`, available stream logs | supported public execution-result fields; unavailable streams are not invented |
| `raw-output.txt`, `transcript.jsonl`, `tool-calls.jsonl` | legacy convenience captures, subject to payload policy and bounds |

CodeAct's **in-sandbox** `call_tool` invocations do not appear in model messages. They are captured
in the tool journal and events by `RecordingAIFunction`, a transparent
decorator applied by `ToolCatalog` that records to an ambient (AsyncLocal) `ToolCallRecorder` the
runner activates per run. It delegates name/description/schema to the inner tool, so the fairness
fingerprint is unchanged. Metadata capture is the default; explicit full capture redacts and bounds
payloads to 65,536 characters. Model-facing calls, granular calls and sandbox callbacks are separate
metrics, reconciled against actual events and per-response usage. A batch aggregate references
independently persisted children and labels its output as application-merged, not model-generated.

The legacy combined transcript can truncate mid-record on Medium/Large; use structured model
capture envelopes and result JSON instead. The no-guest CodeAct stand-in executes no Python and
emits explicit offline events, not real sandbox-completion events.
See the [current POC review](review-current/poc-demo-review.md) for verified scope and open issues.
