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

Every run is identified by a `runId` (a GUID) that is the correlation ID across all output. It names
the artifact folder `artifacts/runs/{runId}/`, appears on every structured log line (`AnalysisRunner`
logs run start/completion at Information and each tool call at Debug — the console app wires a console
logger; the APIs use the host logger), and is stamped on every record in the JSONL artifacts below.

`FileRunStore` writes these per-run artifacts (built by the pure `RunArtifacts`):

| File | Contents |
| --- | --- |
| `result.json` / `metrics.json` / `metadata.json` | response DTO, metrics, and run/config metadata |
| `events.jsonl` | run timeline (start, per-tool counts, turns, completion) |
| `raw-output.txt` | the final model text, verbatim (never repaired) |
| `generated-code.txt` | **every** `execute_code` program the model emitted (CodeAct; delimited when chunked) |
| `transcript.jsonl` | the full model conversation — one line per message (role, text, tool calls, tool results) |
| `tool-calls.jsonl` | one line per host-side tool invocation (name, argument JSON, result JSON, duration, status) |

`tool-calls.jsonl` is the only place CodeAct's **in-sandbox** `call_tool` invocations are visible —
they never appear in the model messages. Capture happens in `RecordingAIFunction`, a transparent
decorator applied by `ToolCatalog` that records to an ambient (AsyncLocal) `ToolCallRecorder` the
runner activates per run. It delegates name/description/schema to the inner tool, so the fairness
fingerprint is unchanged. Detailed arguments/results appear only in these local, synthetic-data
artifacts and logs; the OpenTelemetry metric/trace tags remain redacted to `tool.name`/`tool.status`.

