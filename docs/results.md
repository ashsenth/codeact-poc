# Results and evidence

This narrative preserves the final November 17, 2026 experiment record. The [landing page](../README.md#results) shows the Sol headline; [generated full results](v3-responses-results.md) retain all current counters. No trials, scores or measurements are changed by this documentation split.

## Current state

**November 17, 2026:** the enforced Responses experiment is complete and pushed through `6a50d817dc485fb49344133d3f081363bf45de09`. Both models completed five measured pairs at Small N=1, N=3 and full Small. Sol Classic was correct only at N=1; Sol CodeAct was correct in every measured trial. Never launch another controller against an active checkout or output root.

| Area | State |
|---|---|
| Offline | Oracle-backed fake model; exercises datasets, harness, scoring and reports, not model reasoning. No Azure or virtualization needed. |
| Real-guest feasibility | A hand-authored, no-LLM program scored Exact at Small N=1, N=3 and all 10 households. |
| Live models | Historical 4o Chat Completions and newer enforced 4o/Sol Responses are separate cohorts. |
| Validation | Audit remediation gate: 314 non-live .NET tests, 13 Python checks and six supported Hyperlight tests. The native-cancellation known issue is excluded, not passed. |
| Scaling | Live scope is Small only. Medium/Large cannot fit complete single-output reports under the pinned output design; v4 redesign is pending. |
| Containment | Benchmark workers have external watchdogs. Native guest cancellation is unsupported; standalone API hosts lack that watchdog. |

See [cohort status and gates](v3-enforced-cohorts.md), [generated within-model results](v3-responses-results.md), and [known limits](sdk-findings.md#known-limits).

## Time-budget sensitivity (120 s vs 600 s)

The separately approved `gpt-6.1-sol-v3-t600` cohort changes only the timeout to
600 seconds in both modes, including the worker watchdog; the 20-request cap and
all model/prompt/schema/tool/pricing settings stay fixed. It is not pooled with
the 120-second experiment above. See the [separate sensitivity results](v3-time-budget-sensitivity.md)
for per-scope/mode correctness, timeouts, turn limits and medians, plus
correctness-labelled within-model deltas and [manual launch instructions](running-experiments.md#time-budget-sensitivity-600-seconds).
The wrapper is prepared for the operator; no sensitivity inference is launched
by the preparation task.

## What the evidence says

Read the evidence in this order; successful execution or a VALID comparison is not necessarily a correct answer.

### 1. Feasibility control: the Small task can be done

A hand-authored callback-only Python program ran through the real Hyperlight/service/worker path, without an LLM or oracle substitution. It scored **Exact** for N=1, N=3 and full Small, emitting 1,638 / 4,698 / 15,274 UTF-8 bytes with 23 / 39 / 84 host callbacks.

This demonstrates feasibility for those inputs and concise output, not model ability or a speed comparison. It is separate from the no-guest fake-model demo.
Evidence: [program, control method and failed prototypes](v3-diagnostics.md#1-hand-authored-feasibility-control).

### 2. gpt-4o: both orchestrations have correctness failures

Historical prompt-v3 Chat Completions produced unearned completeness in both modes. Across five pairs per scope, Classic was correct 0/5 at N=1, N=3 and full Small; CodeAct was correct 2/5, 0/5 and 0/5. Schema-valid complete claims often omitted allocations, required breaches or trades.

Those historical runs did **not** enforce the configured 20-request cap in flight: four Classic and one CodeAct measured trials exceeded it. Their resources are not a fair enforced-budget baseline.

The newer enforced Responses cohort is separate. Its 4o results again show Classic 0/5 in every scope and CodeAct 2/5, 0/5, 0/5. Most Classic outcomes hit TurnLimitReached, while CodeAct still emitted unearned complete reports. This does not mean 4o never succeeds or establish a model-internal cause.

Evidence: [failure forensics](v3-diagnostics.md#2-all-non-correct-measured-v3-codeact-trials), [historical request audit](infrastructure-gate-20261117.md#2-read-only-historical-audit).

### 3. Sol: correct in both modes at N=1, CodeAct correct at larger scopes

At N=1 Sol scored **5/5 in both modes**: Classic WithinTolerance, CodeAct Exact. At N=3 and full Small, Classic timed out in **all five trials per scope**, while CodeAct scored **Exact 5/5**. No Sol measured trial claimed unearned completeness. One returned sandbox overflow was counted in CodeAct at each larger scope.

Do not generalize N=1's equivalence to the larger scopes, or count timeouts as cheap successful analyses. Evidence: [final cohort record](v3-enforced-cohorts.md#final-status-november-17-2026).

### 4. CodeAct efficiency: compare within the same model first

The primary view is **CodeAct versus Classic within one model and scope**, after correctness. At Sol N=1, both modes were 5/5 correct and CodeAct used median -59.5% total tokens, -39.5% estimated cost and -64.9% analysis time. This is a small, scope-specific observation, not a guaranteed speedup.

At Sol N=3/full Small, correctness differs: resource deltas are descriptive, not equivalent-work wins. Full Small CodeAct costs **225.1% more** than Classic's timed-out attempts while producing correct reports; fewer tokens do not necessarily mean lower cache-adjusted cost. Cross-model differences are secondary and confound model capability, pricing and API behavior.

## Results (gpt-6.1-sol v3)

Five measured pairs per scope, one unmeasured warm-up per mode. Historical cohorts, smoke pairs, controls, aborted attempts and derived reclassifications are excluded. All measured outcomes remain in medians. Correct = Exact/WithinTolerance; TL = TurnLimitReached; A/O/429 = tool-argument / sandbox-overflow / HTTP-429 counts. Counts are totals, not medians.

### Primary: within-model CodeAct versus Classic

Every arrow is **Classic -> CodeAct**. Tokens show median total **(reasoning)**; reasoning is already included, never added twice. Time is median analysis seconds, not process-inclusive wall time. Delta = `(CodeAct / Classic - 1)`. Every measured retry wait was zero.

| Model / scope | Correct | Unearned | TL | A/O/429 | Median tokens (reasoning) | Median USD | Analysis s | Delta tokens / cost / time | Correctness context |
|---|---|---|---|---|---|---|---|---|---|
| 4o / N=1 | 0/5 -> 2/5 | 0 -> 3 | 5 -> 0 | 0/0/0 -> 0/0/0 | 141,077 (0) -> 15,794 (0) | 0.1892150 -> 0.0461825 | 67.891 -> 22.908 | -88.8% / -75.6% / -66.3% | Correct counts differ; descriptive only |
| Sol / N=1 | 5/5 -> 5/5 | 0 -> 0 | 0 -> 0 | 0/0/0 -> 0/0/0 | 36,694 (1,949) -> 14,874 (29) | 0.0452393 -> 0.0273612 | 94.098 -> 33.013 | -59.5% / -39.5% / -64.9% | Correctness-equivalent by count (5/5 each) |
| 4o / N=3 | 0/5 -> 0/5 | 1 -> 5 | 4 -> 0 | 0/0/0 -> 0/0/0 | 253,431 (0) -> 7,594 (0) | 0.3346050 -> 0.0170475 | 65.573 -> 14.949 | -97.0% / -94.9% / -77.2% | Correctness-equivalent by count (0/5 each); neither correct, no efficiency win |
| Sol / N=3 | 0/5 -> 5/5 | 0 -> 0 | 0 -> 0 | 0/0/0 -> 0/1/0 | 121,483 (102) -> 30,504 (52) | 0.1108500 -> 0.0539617 | 120.028 -> 50.546 | -74.9% / -51.3% / -57.9% | Correct counts differ; Classic timed out 5/5 |
| 4o / full Small | 0/5 -> 0/5 | 0 -> 4 | 5 -> 0 | 0/0/0 -> 0/0/0 | 422,918 (0) -> 20,755 (0) | 0.5553400 -> 0.0506150 | 78.515 -> 25.173 | -95.1% / -90.9% / -67.9% | Correctness-equivalent by count (0/5 each); neither correct, no efficiency win |
| Sol / full Small | 0/5 -> 5/5 | 0 -> 0 | 0 -> 0 | 0/0/0 -> 0/1/0 | 81,439 (209) -> 44,606 (81) | 0.0348349 -> 0.1132577 | 120.033 -> 100.498 | -45.2% / +225.1% / -16.3% | Correct counts differ; Classic timed out 5/5 |

Equal correct counts do not guarantee the same correct pairs or useful equivalence; 0/5 versus 0/5 never establishes an efficiency win.

### Secondary: cross-model view

Arrows here are **4o -> Sol**, same mode/scope. All deltas are descriptive; differing correctness is not equivalent work. R/V/C = median reasoning / visible-output / cached-input tokens, each a separate median. Total/output include reasoning once.

| Scope / mode | Correct | Unearned | TL | A/O/429 | Median total tokens (R / V / C) | Median USD | Analysis s | Delta tokens / cost / time |
|---|---|---|---|---|---|---|---|---|
| N=1 / Classic | 0/5 -> 5/5 | 0 -> 0 | 5 -> 0 | 0/0/0 -> 0/0/0 | 141,077 (0 / 1,179 / 137,856) -> 36,694 (1,949 / 2,244 / 32,733) | 0.1892150 -> 0.0452393 | 67.891 -> 94.098 | -74.0% / -76.1% / +38.6% |
| N=1 / CodeAct | 2/5 -> 5/5 | 3 -> 0 | 0 -> 0 | 0/0/0 -> 0/0/0 | 15,794 (0 / 2,557 / 9,984) -> 14,874 (29 / 1,787 / 9,957) | 0.0461825 -> 0.0273612 | 22.908 -> 33.013 | -5.8% / -40.8% / +44.1% |
| N=3 / Classic | 0/5 -> 0/5 | 1 -> 0 | 4 -> 0 | 0/0/0 -> 0/0/0 | 253,431 (0 / 1,161 / 246,144) -> 121,483 (102 / 6,020 / 100,003) | 0.3346050 -> 0.1108500 | 65.573 -> 120.028 | -52.1% / -66.9% / +83.0% |
| N=3 / CodeAct | 0/5 -> 5/5 | 5 -> 0 | 0 -> 0 | 0/0/0 -> 0/1/0 | 7,594 (0 / 723 / 5,888) -> 30,504 (52 / 3,527 / 18,659) | 0.0170475 -> 0.0539617 | 14.949 -> 50.546 | +301.7% / +216.5% / +238.1% |
| Full Small / Classic | 0/5 -> 0/5 | 0 -> 0 | 5 -> 0 | 0/0/0 -> 0/0/0 | 422,918 (0 / 2,278 / 415,232) -> 81,439 (209 / 1,874 / 76,824) | 0.5553400 -> 0.0348349 | 78.515 -> 120.033 | -80.7% / -93.7% / +52.9% |
| Full Small / CodeAct | 0/5 -> 5/5 | 4 -> 0 | 0 -> 0 | 0/0/0 -> 0/1/0 | 20,755 (0 / 2,369 / 15,360) -> 44,606 (81 / 7,645 / 23,302) | 0.0506150 -> 0.1132577 | 25.173 -> 100.498 | +114.9% / +123.8% / +299.2% |

### Evidence

- [Generated full results](v3-responses-results.md): full counters, token partitions, retry wait and correctness-labelled deltas.
- [Model/API/pricing and execution record](v3-enforced-cohorts.md), [same-turn parallel-call audit](v3-parallel-audit.md), [canonical methodology](benchmark-methodology.md).
- Sol publication commits: N=1 `80c35245bf926e6c26a753eab777ae5dabb1fcb1`, N=3 `1b55bea6871614a1f564381f416519582a9ae909`, full Small `6a50d817dc485fb49344133d3f081363bf45de09`.
- Comparison IDs, scopes N=1 / N=3 / full Small: Sol `small-20261117175030-08e828b0af534dfdbaea775967807011`, `small-20261117190251-4e21059d12b740ad99ebae1811d9d9d4`, `small-20261117192039-a9fbc87c067546e891e0690922317f78`; 4o `small-20261117172306-877c46337ba1469aa242d5fc11783121`, `small-20261117173158-1dda532076ea4b31b13eca81e8fa3f3f`, `small-20261117173950-867fc9f253c04d61a33121bc4a9c79b1`.
- Raw local evidence layout: `artifacts/v3-enforced-20261117/{gpt-4o,gpt-6.1-sol}/{n1,n3,full-small}/comparisons/` and sibling `runs/`. These are **ignored local evidence**, not files supplied by a fresh clone. Request a reviewed evidence bundle for independent raw-event inspection.
