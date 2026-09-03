# Requirements traceability

Legend — Status: **Met**, **Partial**, **Not Met**, **Not Verifiable** (in this environment).
Verification: **E** executed command/test, **S** static inspection, **T** specific test cited.

| # | Requirement | Evidence (files/symbols) | Verify | Status | Gap / risk | Severity |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | Two independently runnable apps | `src/SterlingVale.Classic.Api/Program.cs`, `src/SterlingVale.CodeAct.Api/Program.cs` | E (build), S | Met | Not started via `dotnet run` in audit; exercised via `WebApplicationFactory` | Low |
| 2 | Identical external HTTP contract | `src/SterlingVale.Api.Shared/AnalysisEndpoints.cs` (shared by both) | S, T (Contract.Tests) | Met | — | — |
| 3 | Same dataset + hash | `Infrastructure/Data/DatasetLoader.ComputeDatasetHash`, `FairnessFingerprints` | S, T (Data.Tests determinism) | Met | — | — |
| 4 | Same prompt + hash | `AgentShared/Prompting/Prompt.cs`; hash = `Sha256Hex(Prompt.Shared)` | S | **Partial** | Instructions are NOT byte-identical: shared task body + a 1-sentence mode "mechanism addendum" (`ClassicAddendum`/`CodeActAddendum`); hash covers only `Shared` (ADR-0010) | Low (disclosed) |
| 5 | Same request / output schema | `Domain.Analysis.AnalysisRequest`, `Prompting/OutputSchema` (`exposure-report/v1`) | S, T | Met | — | — |
| 6 | Same model + parameters | `ModelConfiguration` shared via `AnalysisService`; both factories read same config | S, T (Contract fingerprints) | Met | — | — |
| 7 | Same tools/names/descr/schemas | `Tools/ToolCatalog`; both factories `ComputeToolFingerprint` equal | T (`CrossModeContractTests`) | Met | — | — |
| 8 | Same timeout / retry / pricing / warm-up | `AnalysisRunner` (timeout), `PricingConfiguration`, `BenchmarkHarness` (1 warm-up) | S, T | Met | Retry policy = none for both (`RetryCount=0` always); trivially identical | Low |
| 9 | Orchestration is the only intended difference | `IAgentFactory` seam; `ClassicAgentFactory` vs `CodeActAgentFactory` | S, T (Architecture/Contract) | Met | Plus the prompt addendum (#4) | Low |
| 10 | Seven granular tools | `Tools/ToolNames.All`, `PortfolioToolService` | T (`ToolCatalogTests`) | Met | — | — |
| 11 | No coarse/compute-everything tool | `Tools` has no valuation/allocation tool; oracle absent | T (`ArchitectureTests.Tools_never_depend_on_the_oracle`) | Met | — | — |
| 12 | Separate deterministic oracle, never a tool | `Application/Oracle/ExposureOracle`; not in any catalog | T (Architecture) | Met | — | — |
| 13 | Deterministic synthetic data + manifest hashes | `DataGenerator/*`, `DatasetWriter.Sha256Hex`, `manifest.json` | T (Data.Tests), E (validate-data) | Met | — | — |
| 14 | Metrics + artifact persistence | `AgentShared/Artifacts/RunArtifacts`, `Infrastructure/Runs/FileRunStore` | T (`RunArtifactsTests`, integration) | Met | — | — |
| 15 | Structured correctness comparison | `Benchmark/Scoring/CorrectnessScorer` | T (`CorrectnessScorerTests`) | Met | — | — |
| 16 | Paired benchmark trials | `Benchmark/BenchmarkHarness` (`pairId`) | T (`HarnessEndToEndTests`), E (smoke) | Met | — | — |
| 17 | Run-order randomization | `BenchmarkHarness` seeded from dataset hash (recorded) | S | Met | — | — |
| 18 | Warm-up behavior (1 unmeasured/mode) | `BenchmarkHarness.RunAsync` | S, T | Met | — | — |
| 19 | Retry consistency / record attempts | no retry implemented; `RetryCount=0` | S | Met | Field vestigial | Low |
| 20 | Cost configuration | `PricingConfiguration.EstimateCost` (null-safe) | T | Met | — | — |
| 21 | Fake-client tests (default, offline) | `AgentShared/Fakes/DeterministicFakeChatClient`; all default tests | E (146 passed) | Met | — | — |
| 22 | Live-model test isolation | `[Trait Category=LiveModel]`, `ENABLE_LIVE_TESTS` gate, caps | E (skipped), S | Met | Not executed (no creds) | Low |
| 23 | Hyperlight integration tests | `HyperlightRuntimeTests`, `HyperlightEndToEndTests` (gated) | E (skipped), S | **Not Verifiable** | Runtime not exercised here (no guest/virtualization); deep direct `execute_code`/`call_tool` assertions are via API E2E, not low-level | Low |
| 24 | Security restrictions (sandbox) | `CodeActAgentFactory.CreateHyperlight` (no domains, no mounts, dispose) | S | **Partial/Not Verifiable** | Correct by inspection; not runtime-verified without a guest | Medium |
| 25 | Metrics/token usage never invented | `AnalysisRunner` (`InputTokens` etc. nullable) | S, T | Met | — | — |
| 26 | No hard-coded performance outcome | no savings %/winner anywhere; report shows raw medians only | S | Met | — | — |
| 27 | Generated-code captured safely / marker when absent | `AnalysisRunner.ExtractGeneratedCode`, `RunArtifacts.GeneratedCodeUnavailable` | T | Met | — | — |
| 28 | Required documentation | `README.md`, `docs/{architecture,benchmark-methodology,security-model,metrics,sdk-findings,decisions}.md`, `SECURITY.md` | S | Met | Port/line-ending nuances (see findings) | Low |
| 29 | CI pipeline (restore-locked, build, format, tests, coverage, vuln, secret) | `.github/workflows/ci.yml` | S | **Partial** | Not executed (CI-only); CRLF/`.gitattributes` risk on Linux | Medium |
| 30 | Central package mgmt, pinned, locked | `Directory.Packages.props`, `packages.lock.json`, `RestorePackagesWithLockFile` | E (locked restore) | Met | Hyperlight preview pin (unavoidable) | Low |
| 31 | Quality gates (nullable, warnings-as-errors, analyzers, XML docs) | `Directory.Build.props` | E (0 warnings) | Met | `CS1591` suppressed (documented), `CA1822/CA1859` → suggestion (ADR-0009) | Low |
| 32 | Startup configuration validation | `DatasetSnapshotProvider.Validate` (datasets only) | S | **Partial** | Invalid `BENCHMARK_*`/`PRICING_*` env silently fall back to defaults; not validated/failed at startup | Medium |
