# Architecture Decision Records

Chronological, append-only. Each ADR records a decision, its context, and any **deviation**
from the original specification (as required by the task).

---

## ADR-0001 — Repository root is `codeact-poc`

- **Status:** Accepted.
- **Context:** The specification names the repository `sterling-vale-codeact`. The user
  explicitly instructed that all code and changes live inside the existing `codeact-poc`
  directory.
- **Decision:** The physical repository root is `codeact-poc/`. The .NET solution and product
  name remain **Sterling Vale** (`SterlingVale.sln`, `SterlingVale.*` projects) to preserve the
  specified naming for assemblies, namespaces, and docs.
- **Deviation:** Directory name only. No functional deviation.

---

## ADR-0002 — Target framework `net9.0` (samples use `net10.0`)

- **Status:** Accepted.
- **Context:** The published Agent Framework samples target `net10.0`. This host has no admin
  rights; the newest installable SDK is `9.0.317`. A restore/compile probe confirmed all pinned
  packages work on `net9.0`.
- **Decision:** All projects target `net9.0`. TFM is centralized in `Directory.Build.props` so a
  future bump to `net10.0` is a one-line change.
- **Deviation:** TFM differs from samples. No API deviation observed.

---

## ADR-0003 — Central package management, pinned preview versions

- **Status:** Accepted.
- **Context:** Fairness and reproducibility require identical, non-floating dependencies across
  both apps. `Microsoft.Agents.AI.Hyperlight` is preview-only.
- **Decision:** All versions live in `Directory.Packages.props`; `RestorePackagesWithLockFile`
  + `--locked-mode` in CI. The Hyperlight preview version is pinned exactly
  (`1.19.0-preview.260822.1`), matching the core packages.
- **Deviation:** Using a preview package is unavoidable — CodeAct exists only in preview.

---

## ADR-0004 — Both orchestrations derive tools from one `ToolCatalog`

- **Status:** Accepted.
- **Context:** The only intentional variable is orchestration. Different tool descriptions or
  schemas would confound the benchmark.
- **Decision:** A single `ToolCatalog` in `SterlingVale.Tools` defines the seven granular tools
  (name, description, schema, implementation). Classic registers them directly as
  `AIFunction`s; CodeAct registers the **same** `AIFunction`s as provider-owned tools. A contract
  test asserts both modes expose identical tool names + schemas (hashed).
- **Deviation:** None.

---

## ADR-0005 — Correctness oracle is never an agent tool

- **Status:** Accepted.
- **Context:** The oracle must be an independent ground truth, not something the agent can call.
- **Decision:** The reference calculator lives in `SterlingVale.Application` and is used only by
  tests and scoring. An architecture test asserts it is never registered in any tool catalog or
  agent options.
- **Deviation:** None.

---

## ADR-0006 — Deterministic fake model for default build/CI

- **Status:** Accepted.
- **Context:** The repo must build and run locally without live model access, and CI must be
  deterministic. Live and Hyperlight-runtime paths depend on external prerequisites.
- **Decision:** A deterministic fake `IChatClient` drives default tests. Live-model tests carry a
  `LiveModel` trait and are opt-in (`ENABLE_LIVE_TESTS`); Hyperlight tests skip with a reason when
  `HYPERLIGHT_PYTHON_GUEST_PATH` is absent. No path ever fabricates a successful live/Hyperlight run.
- **Deviation:** None.

---

## ADR-0007 — Fairness enforced by hashing, fail-closed

- **Status:** Accepted.
- **Context:** The benchmark must be scientifically defensible; every confounder must be
  detectable.
- **Decision:** Prompts, output schemas, tool definitions, model configuration, and dataset
  manifests are SHA-256 hashed. A comparison **fails** if the paired runs' hashes differ. Hashes
  are recorded in every run artifact and comparison report.
- **Deviation:** None.

---

## ADR-0008 — Generated-code artifact is best-effort, never fabricated

- **Status:** Accepted.
- **Context:** Whether the preview SDK exposes the model-generated program is version-dependent.
- **Decision:** When the SDK exposes generated code safely, it is written to
  `generated-code.txt` as a restricted artifact. When not, the file contains an explicit
  "unavailable" marker. The number of `execute_code` calls is measured, never assumed to be one.
- **Deviation:** Acknowledges a preview limitation; no spec deviation.

---

## ADR-0009 — Justified analyzer severities (CA1822, CA1859)

- **Status:** Accepted.
- **Context:** `TreatWarningsAsErrors=true` with `AnalysisLevel=latest-recommended` promotes two
  micro-optimization analyzers to build errors: **CA1822** ("member can be static") and **CA1859**
  ("use concrete collection type for performance").
- **Decision:** Both are lowered from error to **suggestion** in `.editorconfig`, with inline
  justification. Rationale: (a) generator/validator/writer/service types are deliberately
  *instance* members so they can be registered and injected via DI and substituted in tests;
  (b) domain and service APIs intentionally expose read-only abstractions (`IReadOnly*`) for
  immutability rather than concrete `List`/`Dictionary`. These are design choices, not defects.
- **Scope:** Only these two rules are relaxed, and only to `suggestion` (still visible). All other
  warnings remain errors.
- **Deviation:** None from spec intent; documents justified suppressions as the spec requires.

---

## ADR-0010 — Shared prompt + minimal per-mode mechanism addendum

- **Status:** Accepted.
- **Context:** Fairness requires "same prompt wording", but CodeAct must be told to write one
  bounded program (`execute_code` + `call_tool`) while Classic calls tools directly. These
  mechanism instructions genuinely differ.
- **Decision:** `Prompt.Shared` holds the entire task definition, computation rules, output
  contract, and disclaimer — identical for both modes and the ONLY text covered by the fairness
  prompt hash. Each mode appends a single-sentence mechanism note (`ClassicAddendum` /
  `CodeActAddendum`). That mechanism note is part of the orchestration variable under test, not a
  hidden prompt difference.
- **Consequence:** The prompt hash asserts the shared body is identical across a comparison; the
  addendum is short, explicit, and documented.
- **Deviation:** The mode addendum is a necessary, disclosed difference tied directly to the
  orchestration being measured.

---

## ADR-0011 — Offline fake model uses the oracle to synthesize answers

- **Status:** Accepted.
- **Context:** The apps must build and run without live model access. The fake model must produce
  schema-valid output and exercise at least one tool call.
- **Decision:** `FakeAnalysisPlanner` runs the deterministic oracle to compute the report and emits
  it as the fake model's answer after a representative `list_households` call. Offline runs are thus
  a useful control (both modes emit identical, correct output) and never a substitute for measuring
  a real model. The fake is clearly separated from live paths (`ENABLE_LIVE_TESTS`, `LiveModel`
  trait) and never labelled as a live result.
- **Deviation:** None; this is the deterministic fake mandated by the spec.

---

## ADR-0012 — Microsoft.Extensions.* pinned to 10.0.11

- **Status:** Accepted.
- **Context:** The Agent Framework (`Microsoft.Agents.AI`, `Microsoft.Extensions.AI 10.9.0`) and
  `System.ClientModel` transitively require `Microsoft.Extensions.*` ≥ `10.0.x`. Pinning them at
  `9.0.0` produced `NU1605` downgrade errors (fatal under warnings-as-errors).
- **Decision:** The `Microsoft.Extensions.*` central versions are pinned to `10.0.11`. They ship
  net9.0-compatible assets (already present in the transitive graph) and build cleanly on the
  net9.0 ASP.NET Core app.
- **Deviation:** None; required for a consistent, lockable dependency graph.

---

## ADR-0013 — CodeAct offline path + gated Hyperlight tests

- **Status:** Accepted.
- **Context:** CodeAct's `execute_code` requires a Hyperlight guest + hardware virtualization, which
  are unavailable in CI. The apps must still build and run offline, and both APIs must be thin,
  non-duplicating composition roots.
- **Decisions:**
  1. A shared `SterlingVale.Api.Shared` library owns the identical health/analysis endpoint mapping,
     JSON config, and problem-details middleware, so the two APIs cannot diverge in contract. (Its
     namespace is `SterlingVale.Api.Hosting` because `Shared` is a reserved VB keyword — CA1716.)
  2. `CodeActAgentFactory` builds the real `HyperlightCodeActProvider` when a guest path is
     configured; otherwise it registers an offline `execute_code` stand-in
     (`FakeExecuteCodeFunction`) so the CodeAct API runs deterministically with the fake model.
  3. The mode-aware offline planner makes the fake call `execute_code` for CodeAct (vs a granular
     tool for Classic), so CodeAct metrics show `ExecuteCodeCallCount == 1` offline.
  4. Both factories compute their tool fingerprint over the SAME seven catalog tools, proving the
     tool set/schemas are identical across modes (the fairness guarantee).
  5. Hyperlight runtime tests use `Xunit.SkippableFact`/`Skip.If` to skip with an explicit reason
     when `HYPERLIGHT_PYTHON_GUEST_PATH` is unset; they never fake a sandbox run.
- **Deviation:** The full `execute_code` E2E is only exercised when a guest is present; documented as
  a preview/environment limitation, never simulated.

---

## ADR-0014 — Phase 0–7 audit fixes

- **Status:** Accepted.
- **Context:** A verification pass over Phases 0–7 confirmed the build (Release, warnings-as-errors),
  full test suite, `dotnet format`, and a **`--locked-mode` restore** all pass, and that Domain has
  zero references while Application references only Domain. Two gaps were found and closed:
  1. **Missing request/response API schemas.** Phase 1/4 requires "versioned JSON schemas for request
     and response contracts". Only the model-output (`ExposureReport`) schema existed. Added
     `ApiSchemas` with versioned `analysis-request/v1` and `analysis-response/v1` JSON Schemas. The
     shared POST endpoint now validates the raw request body against the request schema and returns
     RFC 7807 `400` on violation (including unknown fields via `additionalProperties: false`).
  2. **Dead code.** `InMemoryRunStore` was superseded by `FileRunStore` in Phase 7 and referenced
     nowhere; it was removed.
- **Deviation:** None; both are corrections that bring the implementation in line with the spec.

---

## ADR-0015 — CodeAct factory extracted to a library; benchmark harness

- **Status:** Accepted.
- **Context:** Phase 8's benchmark must drive BOTH orchestrations in-process. `CodeActAgentFactory`
  lived in the CodeAct **Web** app, which a console tool cannot reference without dragging in the
  ASP.NET host.
- **Decisions:**
  1. Extracted `CodeActAgentFactory` into a small `SterlingVale.CodeAct` class library (namespace
     `SterlingVale.CodeAct`), referenced by both `SterlingVale.CodeAct.Api` and
     `SterlingVale.Benchmark`. (Classic's factory already lived in `AgentShared`.)
  2. `SterlingVale.Benchmark` reuses the shared `AnalysisService`/`AnalysisRunner` with each mode's
     factory, so the only variable remains orchestration. It performs one unmeasured warm-up per
     mode, then N paired measured trials with **randomized** mode order (seed derived from the
     dataset hash and recorded), retains every raw trial, and aggregates median/min/max.
  3. Correctness is scored against the oracle (flagged/breach precision & recall, allocation and
     trade-notional error, match classification); missing token usage stays unknown, never invented.
  4. Fairness fingerprints (prompt/schema/tools/model/pricing/dataset) are compared across modes; a
     mismatch marks the comparison **failed** (non-zero exit) and is surfaced in the report.
  5. Outputs: per-run artifacts via the shared `FileRunStore`, plus
     `artifacts/comparisons/{id}.json` and `{id}.md`.
- **Deviation:** None.

---

## ADR-0016 — Phase 10: architecture/contract tests, CI, docs

- **Status:** Accepted.
- **Decisions:**
  1. `SterlingVale.Architecture.Tests` (NetArchTest) enforces: Domain has no framework/infrastructure
     dependencies; Application depends only on Domain; the oracle (`ExposureOracle`) is never
     referenced by Tools or Infrastructure (never a tool); the CodeAct and Classic factories do not
     reference each other.
  2. `SterlingVale.Contract.Tests` asserts the cross-mode contract: both factories produce an
     **identical tool fingerprint**, share the same prompt body, and yield matching fairness
     fingerprints; schema ids are stable.
  3. GitHub Actions CI runs restore `--locked-mode` → build (Release) → `dotnet format --verify` →
     tests filtered `Category!=LiveModel&Category!=Hyperlight` with coverage → dependency
     vulnerability scan → gitleaks secret scan.
  4. Documentation: `README.md`, `docs/architecture.md`, `docs/benchmark-methodology.md`,
     `docs/security-model.md`, `SECURITY.md` (plus the earlier `sdk-findings.md`, `decisions.md`,
     `metrics.md`).
- **Deviation:** No `docker-compose.yml` is provided — Hyperlight requires host hardware
  virtualization that is not portable across containers, and the spec makes Docker conditional on
  Hyperlight compatibility. Documented in README.
