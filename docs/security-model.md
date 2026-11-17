# Security model

Sterling Vale CodeAct executes **model-generated code** in the CodeAct mode. This document defines
the trust boundaries and the controls that keep that safe.

## Trust boundaries

| Component | Trust | Notes |
| --- | --- | --- |
| Host process (APIs, tools, oracle) | trusted | runs our code only |
| The model / its output | **untrusted** | prompts and responses are treated as untrusted input |
| Generated code (`execute_code`) | **untrusted** | never executed in the host process |
| Datasets | synthetic | reproducible from a seed; no real data |

## Untrusted generated code

Generated code runs **only inside a Hyperlight micro-VM**, never in the host process. The provider
is configured with strict defaults:

- **No outbound network** — `AllowedDomains` is empty (deny-all).
- **No host filesystem** — no `FileMounts` are configured.
- **Fresh sandbox per run** — a new provider is created and **disposed** after each analysis, so
  state cannot leak between runs.
- **Benchmark containment** — every analysis runs in its own worker process, in both modes,
  including warm-ups and Classic batches. A parent watchdog kills and reaps that worker's
  process tree at the configured deadline. The shared observed client enforces the request
  budget before dispatch. The pinned SDK has **no mid-execution native cancellation**;
  cancelling an `AnalysisRunner` token cannot interrupt running guest code.
- **API-host gap** — standalone Classic/CodeAct API hosts do not use the benchmark worker
  watchdog. Their native guest execution is not protected by an equivalent deadline.
- **Approval mode** — `NeverRequire` (unattended benchmark runs); no host is prompted.

Provider-owned tools are reachable from generated code only through `call_tool(...)`, which marshals
back to the host `AIFunction`s (the same seven read-only tools) and returns bounded JSON.

## Host tools vs sandbox code

The seven granular tools only **retrieve or classify** synthetic data. They validate arguments,
return bounded results, are deterministic, and are safe for concurrent use. They perform no
valuation, allocation, or policy evaluation — those live in the oracle, which is **never** exposed as
a tool (enforced by architecture tests).

## Telemetry redaction

Telemetry (Activities + Metrics) records only non-sensitive attributes: run/comparison/trial ids,
mode, prompt/dataset **hashes**, model id, counts, durations, token counts, and cost. It **never**
records prompts, portfolio data, tool results, secrets, or generated code. Sensitive prompt/response
logging is off by default.

## Secret handling

- No secrets are committed; `.env` is git-ignored (`.env.example` holds placeholders only).
- Azure auth prefers `DefaultAzureCredential`; an API key is used only when explicitly provided.
- CI runs a secret scan and a dependency vulnerability scan.

## Denial-of-service limits

- Request bodies are validated against a schema and oversized/invalid requests are rejected (400).
- Benchmark per-run worker deadlines and request-boundary caps provide containment as above;
  they do not fix SDK cancellation or protect standalone API hosts.
- Sandbox heap/stack sizes and output are bounded by the provider.

## Input validation

- `POST /api/v1/analyses` validates the raw body against `analysis-request/v1`
  (`additionalProperties: false`) before processing.
- Model output is validated against `exposure-report/v2`; invalid output is recorded as a failure,
  never repaired.

Comparison validity and model-outcome classification are defined only by the
[canonical methodology](benchmark-methodology.md#validity-rules).
