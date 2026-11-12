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

The shared prompt is `exposure-analysis/v2`; its embedded `exposure-report/v2` schema requires
`status` (`complete` or `incomplete`) and `missingItems` (strings). Incomplete requires at least
one missing item, may omit unsupported households, and is retained as run status `Incomplete`.
The scorer always classifies it Partial, never Exact/WithinTolerance; execution, schema validity
and correctness remain separate. Complete requires an empty missing-items list.
Domain/DTO mappings preserve both fields, including across Classic batch aggregation.
The stricter v2 schema rejects v1 model outputs without these fields; historical comparison files
remain readable and are not rescored or silently upgraded.

Run summaries and comparison JSON/Markdown/CSV/HTML record the shared prompt hash and its previous
revision's hash. Scaling selects the latest prompt/model-configuration cohort among requested profiles, skips profiles
without that hash, and refuses mixed/unknown hashes rather than comparing pre-/post-change runs.
Price coverage counts successful recorded `get_price` symbols against scoped positions (shared
symbols count for multiple positions); null means arguments were not captured, not zero coverage.

Both modes build chat options from `ModelConfiguration.CreateChatOptions`: non-strict
`json_schema`, identical v2 response shape, parallel tool calls enabled, 16,384 output tokens.
The installed OpenAI adapter maps `AdditionalProperties["strict"]=false` to the response and tools.
`StructuredOutput` derives the Azure-supported schema from canonical v2 without changing the prompt:
it omits the root conditional `allOf` and unsupported string `minLength` constraints. V2 shape,
required fields, enums (including Buy/Sell), references and closed objects remain in the schema
as guidance; the unchanged full v2 validator decides acceptance locally, including conditional/length rules.
This is not a claim that Azure supports every JSON Schema keyword; see
[Azure schema limitations](https://learn.microsoft.com/en-us/azure/ai-foundry/openai/how-to/structured-outputs#json-schema-support-and-limitations).
The transport-schema hash, `strict=false`, `parallel_tool_calls=true` and output cap are stored in
run metadata/configuration events and included in the model fingerprint. Startup prints these choices.
The canonical schema hash
remains separate. Scaling also isolates model fingerprints, so old plain-JSON runs cannot mix
with schema-format runs even when their shared prompt hash matches. Unsupported deployments
surface provider failures; there is no silent fallback to plain JSON.
Wire serialization is verified with a local fake HTTP handler, not a live model call.

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

Observed transcripts preserve one JSON object per message and every call/result identity.
Text, arguments and results are individually captured in policy envelopes, so oversized payloads
remain explicitly partial without removing later messages or breaking JSONL. Metadata/none modes
retain identities without general payload content. Legacy unobserved records keep their prior representation.
Truncated or redacted raw-output captures also retain their envelope rather than masquerading as
complete text. The no-guest CodeAct stand-in executes no Python and
emits explicit offline events, not real sandbox-completion events.
Benchmark model selection is explicit: `--model fake|live`, default fake regardless of Azure
environment variables. APIs use `--model live` or `MODEL_MODE=live` for explicit opt-in.
Live no-guest CodeAct captures generated Python, does not invoke the stand-in, terminates after
the current generated-call batch, and returns `NotExecuted` with an explicit missing-guest reason.
It is not a completed analysis or a valid performance comparison.
Generation-only CodeAct token figures are labelled "first turn only, code not executed: not
comparable to Classic totals" in console/Markdown and HTML/scaling reports, next to the raw token
counts. They are not a completed CodeAct workload or evidence of token savings.

Failure events retain a safe category, HTTP status, allowlisted service code and synthesized
guidance; raw provider messages/URLs/credentials are never copied. Normal console output prints
full identity only at RUN STARTED, then a short run prefix; verbose/debug and journals retain
full identity. Warm-ups carry `isWarmup` on context and every event, are persisted, and are excluded
from measured trials. Completion/correctness precede telemetry validity in reports. Any failed or
not-executed measured attempt invalidates the comparison and suppresses aggregate performance
statistics in console, JSON, Markdown, HTML and scaling views while retaining raw trial evidence.
Each run also persists `summary.json` with the final model finish reason, final-response output tokens,
a redacted preview (up to 240 characters), all eight tool names including zero counts, and saved-source
UTF-8 byte totals. These compact diagnostics are deliberately retained independently of payload mode.
They are copied to the final run event and comparison JSON/console/HTML/Markdown/CSV. Missing finish
reasons remain unavailable, never inferred from text length or status.
Live generated Python is an explicit exception to metadata/none payload suppression: source is captured
from execute-code arguments under the same redaction/65,536-character limit and saved as exact `.py`
only when safe and complete. Offline fake payload behavior is unchanged. Generated-code events point
to the file or unavailable-capture marker and include the actual saved UTF-8 byte count.
The benchmark's optional `--households N` applies the same first-N dataset-order IDs to both modes,
warm-ups and oracle; full dataset size/hash remain unchanged and scope size is separately reported.
The live client normalizes pasted path-bearing resource URLs to the HTTPS authority root with one
safe notice. Invalid/insecure or credential-bearing URLs are rejected without echoing their values.
The shared run store and comparison writer refresh `<ARTIFACTS_ROOT>/index.md` with statuses
and relative links to available timelines, generated-code captures and tool-invocation records.
See the [current POC review](review-current/poc-demo-review.md) for verified scope and open issues.
