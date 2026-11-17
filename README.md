# Sterling & Vale: Classic tool-calling vs. CodeAct

This .NET proof of concept compares Classic Microsoft Agent Framework tool calling with CodeAct Python orchestration on Hyperlight, using the synthetic Sterling & Vale wealth dataset. Both modes use the same underlying read-only tools, shared task instructions, report schema and within-model settings. A deterministic oracle scores paired trials, retaining failures as evidence: correctness comes before efficiency. This is an experiment, not a production service or investment advice.

## Why

- Does code-executing orchestration improve correctness and efficiency as data scope grows?
- Can an agent value holdings, check portfolio policy and propose trades without silently omitting required work?
- Does moving repeated tool orchestration into Python reduce model tokens, estimated cost and analysis time?
- Can a correctness-first harness distinguish a complete-looking answer from a correct one?

## How it works

Classic exposes seven granular tools directly to the model. CodeAct exposes `execute_code`; Python calls those same tools inside a Hyperlight micro-VM. The model, dataset, shared prompt body, report schema and oracle scoring match within each comparison; mechanism addenda and model-facing tool surfaces intentionally differ.

```text
             Same model + shared prompt body + dataset
                          /            \
       Classic Agent Framework       CodeAct Agent Framework
          direct tool calls          Python inside Hyperlight
                          \            /
                       Same seven tools
                              |
                   Same report schema + oracle
```

See [architecture and dataset sizes](docs/architecture.md#scenario-and-implementation) and [canonical methodology](docs/benchmark-methodology.md).

## Results

**gpt-6.1-sol v3**, five measured pairs per scope. Correct = Exact/WithinTolerance. Resource arrows are **Classic -> CodeAct**; all measured outcomes remain in medians, warm-ups excluded. Tokens include reasoning once (median reasoning shown in parentheses); cost is estimated USD; time is analysis seconds. Deltas are `(CodeAct / Classic - 1)`.

Classic failures: **TL / U / W / TO** = TurnLimitReached / unearned completeness / other incorrect reports / configured-deadline timeout. Timeouts are not wrong reports or turn-limit hits.

| Scope | Classic correct | CodeAct correct | Classic failures TL/U/W/TO | Median tokens (reasoning) | Median USD | Median seconds | Delta tokens / cost / time | Correctness context |
|---|---|---|---|---|---|---|---|---|
| N=1 | 5/5 | 5/5 | 0/0/0/0 | 36,694 (1,949) -> 14,874 (29) | 0.0452393 -> 0.0273612 | 94.098 -> 33.013 | -59.5% / -39.5% / -64.9% | Correctness-equivalent by count (5/5 each) |
| N=3 | 0/5 | 5/5 | 0/0/0/5 | 121,483 (102) -> 30,504 (52) | 0.1108500 -> 0.0539617 | 120.028 -> 50.546 | -74.9% / -51.3% / -57.9% | Different correctness; descriptive deltas only |
| Full Small (10 households) | 0/5 | 5/5 | 0/0/0/5 | 81,439 (209) -> 44,606 (81) | 0.0348349 -> 0.1132577 | 120.033 -> 100.498 | -45.2% / +225.1% / -16.3% | Different correctness; descriptive deltas only |

1. **The environment can produce Exact:** a hand-authored, no-LLM program through the real Hyperlight/worker path scored Exact at all three Small scopes. This is a feasibility control, not model reasoning.
2. **gpt-4o fails in both modes:** Classic was correct 0/5 at every scope; CodeAct was correct 2/5, 0/5 and 0/5. The harness detects complete-looking reports with missing work as unearned completeness.
3. **Sol succeeds in both modes at N=1:** Classic was WithinTolerance 5/5 and CodeAct Exact 5/5. Here CodeAct used 59.5% fewer median tokens, cost 39.5% less and took 64.9% less analysis time.
4. **At larger scopes Classic times out while CodeAct stays Exact 5/5:** every Classic attempt hit the 120-second worker deadline, not the 20-request cap (8-9 requests at N=3; 5 at full Small). The evidence establishes deadline exhaustion, not a model-internal cause. CodeAct recovered from one returned overflow at each larger scope. Full Small CodeAct cost 225.1% more than the timed-out Classic attempts: this is not an equivalent-work efficiency win.

[Full results, cross-model comparison and evidence](docs/results.md); [generated detailed counters](docs/v3-responses-results.md). Equal correct counts do not guarantee identical correct pairs; cross-model comparisons are secondary.

## Limitations

- The pinned SDK has a **16,376-byte sandbox output buffer**, with no supported size override; telemetry capture has a separate bound.
- **Medium/Large real-sandbox analyses are pending a v4 output design**; current complete single-output reports do not fit. Offline fake-model success does not prove sandbox feasibility.
- **No native mid-execution cancellation:** benchmark workers use a kill/reap watchdog instead. Standalone API hosts lack that watchdog.
- **Small trial counts:** five measured pairs per scope; provider nondeterminism, cache effects, preview SDK behavior and retail cost estimates limit generalization.
- **Synthetic data:** these results do not establish production portfolio accuracy, operational safety or investment suitability.
- **MCP is not used:** tools are direct AIFunction callbacks, so this does not measure MCP transport.

## Documentation

| Guide | Contents |
|---|---|
| [Getting started](docs/getting-started.md) | Prerequisites, offline fake-model quickstart and tests |
| [Azure setup](docs/azure-setup.md) | Foundry resource, pinned deployments, pricing, identity and API routes |
| [VM setup](docs/vm-setup.md) | Corporate-network SSH, dual NSGs, deploy key, KVM, guest and lifecycle |
| [Running experiments](docs/running-experiments.md) | Runners, environment, launch, monitoring, completion, publication and evidence sharing |
| [Troubleshooting](docs/troubleshooting.md) | Azure, shells, SSH/Remote-SSH, guest and harness failures |
| [Benchmark methodology](docs/benchmark-methodology.md) | Canonical fairness, validity rules, scoring and interpretation |
| [Results and evidence](docs/results.md) | Full narrative, within-/cross-model tables, cohort status and raw evidence locations |
| [Architecture](docs/architecture.md) | Dataset/scenario, two modes, APIs and observability |
| [Metrics](docs/metrics.md) | Oracle calculations and tolerances |
| [SDK findings and limits](docs/sdk-findings.md#known-limits) | Pinned APIs, output capacity, cancellation and constraints |
| [Security model](docs/security-model.md) | Containment and reporting boundaries |
| [Cohort record](docs/v3-enforced-cohorts.md) | Model/API/pricing provenance and dated execution gates |
| [Diagnostics](docs/v3-diagnostics.md) | Feasibility control and historical failure forensics |
| [Infrastructure gate](docs/infrastructure-gate-20261117.md) | Historical request audit and capacity stop decision |
| [Parallel-call audit](docs/v3-parallel-audit.md) | Same-turn execution policy evidence |
| [Sanitized sample](docs/sample-run/README.md) | Historical v2 pair and independent household hand-check |

## Status / next steps

- The November 17, 2026 enforced Responses cohorts are complete: five pairs each at N=1, N=3 and full Small for both models; historical API/prompt cohorts remain separate.
- Design and validate v4 output handling before attempting live Medium/Large; address the standalone API watchdog gap before treating it as deadline-contained.
- Broaden independent, correctness-first trials before generalizing. Reproductions must use new evidence roots and never interfere with an active controller.

License: [MIT](LICENSE). Reporting boundaries: [SECURITY.md](SECURITY.md).
