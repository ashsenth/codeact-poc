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
