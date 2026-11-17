# v3 diagnostics: no-LLM feasibility control and CodeAct failure forensics

November 16, 2026. **No live model calls and no prompt changes.** These controls are
not model trials, rates, paired comparisons, or additions to the v3/v2 summaries.
All existing model evidence and summaries are read-only inputs.

## 1. Hand-authored feasibility control

The [reference Python](../scripts/vm/reference-analysis-control.py) performs valuation,
all policy-target allocations/drifts, all four breach kinds, per-symbol concentration,
all target-class trades, and derived flags. Its only data access is `call_tool`:
`list_households`, `get_policy`, `list_accounts`, `list_positions`, `get_price`, `get_fx`.
Asset classes come from positions, base currencies from households, and valuation
currencies from price quotes. Prices and directed FX rates are cached inside the guest.
It computes absent-holding class values as zero; it does not fill uncomputed analysis.

The [diagnostic host](../scripts/vm/Diagnosis/FeasibilityControl.cs) calls the same
`BenchmarkWorker.RunAsync` / `ExecuteAsync`, `AnalysisService`, `CodeActAgentFactory`,
`HyperlightCodeActProvider`, `ObservedFunctionClient`, `RecordingAIFunction`,
`ToolCallRecorder`, `AnalysisRunner`, and worker-tree watchdog as CodeAct.
The only substitutions are the diagnostic executable's worker re-entry point and
a [scripted client](../scripts/vm/Diagnosis/ReferenceProgramClient.cs) instead of Azure:
one `execute_code` request, followed by its **verbatim stdout**, without correction,
completion, retries, or oracle substitution. The diagnostic re-entry point only
accepts `--model fake`; the oracle is used in the parent after worker completion.
The two recorded chat turns are scheduling events, not LLM requests or token usage.
The existing production worker/client choices remain the defaults.

Both the runner deadline and outer worker budget remain **120 seconds**; configured
max turns remain 20 (the post-return enforcement caveat is documented below).
The unchanged provider options still deny network and host file mounts.
The control requires a real guest and refuses an existing output directory.
The unchanged schema validator, `ExposureOracle`, and `CorrectnessScorer` score it.
`Exact` below is the existing scorer's classification, not byte equality with the oracle.

### Results

One execution per scope; all schema/telemetry checks pass, flags/breach precision and
recall are 1, allocation/trade errors are zero, and missing-component maps are empty.
All final output equals the captured stdout exactly.

| Small scope | Match | Sandbox execution s | Analysis s | Worker wall s | Callbacks | Stdout = final answer, UTF-8 bytes | Host result capture, chars | Truncated stdout/result |
|---|---|---:|---:|---:|---:|---:|---:|---|
| N=1 | Exact | 0.272 | 0.362 | 1.200 | 23 | 1,638 | 2,758 | No / No |
| N=3 | Exact | 0.285 | 0.377 | 1.049 | 39 | 4,698 | 7,838 | No / No |
| Full Small, N=10 | Exact | 0.326 | 0.420 | 1.096 | 84 | 15,274 | 25,334 | No / No |

Execution time is the recorded `CODE_EXECUTION_COMPLETED` duration, including callbacks;
analysis time is the same runner timer used in model trials; worker wall time includes
process startup/transport but excludes subsequent parent-side scoring.
All reported output is ASCII, so byte and character counts coincide.

There are **two distinct output constraints**, neither increased for this control:

- Telemetry's `PayloadPolicy` captures at most **65,536 characters per payload**.
  Both each stdout capture and the separately JSON-escaped host tool-result capture
  fit, including the 25,334-character full result. The host envelope is not the guest buffer.
- The guest has a smaller shared output buffer. A preserved verbose-detail prototype
  aborted on full Small with **Required: 16,568; Available: 16,376** shared-buffer bytes.
  The committed version uses concise breach descriptions, with exactly the same computed
  fields, all five policy classes/trades, households and breach evaluations; it fits
  without chunking, dropping required data, changing limits, or truncating output.

An earlier prototype also established that this guest does **not** provide Python's
`decimal` module: all three scopes failed import before any callbacks. The final control
uses the guest's numeric values, binary floating-point arithmetic, ten-place ratio
rounding and currency-minor-unit money rounding, and passes the unchanged decimal
oracle/scorer at all three scopes. This is demonstrated feasibility for these inputs,
not proof that binary floats match decimal arithmetic on every future boundary case.
Both failed prototypes are preserved rather than hidden:
`artifacts/v3-feasibility-control-20261116-unsupported-decimal/` and
`artifacts/v3-feasibility-control-20261116-verbose-output-overflow/`, with adjacent logs.
They are not included in the successful control table or any model summary.

### Evidence and reproduction

[Control results JSON](../artifacts/v3-feasibility-control-20261116/control-results.json)
and [log](../artifacts/v3-feasibility-control-20261116.log); run IDs:

- N=1: `1755da8c2db244f08fcdbf7ca8d3860c`
- N=3: `1a2f3fd624c24564a0eb493705344c9b`
- Full Small: `136b3be529154b87807a0b1d9d7e0fe1`

Each run retains source, executions, streams, callback invocations, timeline, report,
metrics and metadata in this **separate control root**, with no comparison files.
Program SHA-256: `dd33077854b7db2d795621374b1b619baf3ff328ff1cec16935afd2ff9cd5880`.
Dataset SHA-256: `f686013740692996ccae5dcc82d626b76ffba902d27a84e07842666100c7f684`.
Same configured 0.4.0 guest as v3; guest SHA-256:
`c78f780ccdccabf5f8741fc3902190210e0e33ac5767ca79e54ca485fbf4ee4d`.

From the repository root, use absolute paths for the two environment variables:

```bash
export DATASET_ROOT="<existing-generated-data-root>"
export HYPERLIGHT_PYTHON_GUEST_PATH="<absolute-path-to-python-sandbox.aot>"
dotnet build -c Release
dotnet scripts/vm/Diagnosis/bin/Release/net9.0/Diagnosis.dll control \
  scripts/vm/reference-analysis-control.py <new-control-artifacts-directory>
```

No Azure configuration is required or used. The output directory is intentionally
separate from any model sweep. The control explicitly selects the fake/scripted
client, full capture, 20 turns and 120 seconds; the Python is executed only in Hyperlight.

## 2. All non-correct measured v3 CodeAct trials

Coverage: **13/13**, namely N=1 pairs 2/4/5, and all five N=3 and full-Small pairs.
The two correct N=1 trials, unmeasured warm-ups and aborted zero-trial attempts are
not counted in this measured-trial forensic denominator. No model trial was rerun,
rescored, edited or removed.

Method: inspect source, every execution result/stdout/stderr, events, final raw output,
and the saved scorer missing-component map. "Executed" means all six analytical
household fields match recorded stdout exactly: currency, value, flag, drifts, breaches,
trades. It does **not** imply that those values were actually calculated: executed
placeholders are identified separately. Model-added run IDs/timestamps/wrapping are
not treated as analytical provenance. Equality supports copying from stdout; it does
not expose the model's internal reasoning.

The first missing component follows the saved scorer map; schema-invalid/time-out
outputs are unassessed, so their structural blocker is stated instead.
Every row's **stdout and tool-result capture is untruncated**. The timeout row
has a separate bounded *request telemetry* capture, explained below.

| Trial / timeline | Executions | Execution error, exception or timeout | Analytical final-answer provenance | First missing component / blocker |
|---|---:|---|---|---|
| [N=1 pair 2, eda00036](../artifacts/v3-sweep-20261116/n1/runs/eda00036e5ee497692d7d48250a1b08a/timeline.md) | 1 | None; no stdout/result truncation | Executed placeholder: zero total, empty analysis; source explicitly labels them placeholders | H0001 allocation/drift: Equity |
| [N=1 pair 4, 1ce4d3b4](../artifacts/v3-sweep-20261116/n1/runs/1ce4d3b4a7bc470faca2a9b104f6261b/timeline.md) | 1 | None; no stdout/result truncation | Executed valuation/drifts/trades copied, including omitted breaches and inconsistent flag | H0001 AllocationDrift breaches (0/2) |
| [N=1 pair 5, 58889d35](../artifacts/v3-sweep-20261116/n1/runs/58889d35735a44cd9ce40a765a12bf3e/timeline.md) | 3 | Two `KeyError: 'baseCurrency'`, then successful execution; no truncation | Executed total/null currency/empty analysis copied into model's wrapper | Valid H0001 baseCurrency: null fails schema; completeness unassessed |
| [N=3 pair 1, c66cf14d](../artifacts/v3-sweep-20261116/n3/runs/c66cf14d1c744339ba5348075ebcf359/timeline.md) | 5 | Two baseCurrency KeyErrors; two FX validation RuntimeErrors (`Unknown`, empty currency); fifth returns; no truncation | Executed zero/empty placeholders copied for all three households | H0001 allocation/drift: Equity |
| [N=3 pair 2, b16aa4a0](../artifacts/v3-sweep-20261116/n3/runs/b16aa4a017b84c84b33ecd9423b9c7fd/timeline.md) | 2 | None; no stdout/result truncation | **Model-direct analytics:** stdout only IDs/status/missingItems; final invents 1,000,000 totals and allocations | H0001 allocation/drift: Alternative |
| [N=3 pair 3, fd31c4d5](../artifacts/v3-sweep-20261116/n3/runs/fd31c4d52e404876a11516c7e79e5211/timeline.md) | 6 | Two baseCurrency KeyErrors; three FX validation RuntimeErrors (`Unknown` twice, empty once); sixth returns; no truncation | **Model-direct analytics:** stdout only IDs/incomplete-status/missingItems; final adds Unknown currency, zeros/empty arrays | Valid H0001 baseCurrency: Unknown fails schema; completeness unassessed |
| [N=3 pair 4, ee2f1a11](../artifacts/v3-sweep-20261116/n3/runs/ee2f1a11aebc4839b4b245d70e6fe5a7/timeline.md) | 2 | None; no stdout/result truncation | **Model-direct analytics:** stdout only IDs/status/missingItems; final invents 1,000,000 totals and drifts/breaches | H0001 allocation/drift: Alternative |
| [N=3 pair 5, e8e129de](../artifacts/v3-sweep-20261116/n3/runs/e8e129def752427f9e25c2b913ef0ba0/timeline.md) | 5 | Two baseCurrency KeyErrors; three FX validation RuntimeErrors (`Unknown`, empty, `Unknown`); no successful execution/stdout; no truncation | Model-written incomplete response with no households, not executed analytic output | H0001 household |
| [Full pair 1, abd750b1](../artifacts/v3-sweep-20261116/full-small/runs/abd750b160fd4ca9acc2bc893b5ace32/timeline.md) | 3 | None; no stdout/result truncation | Executed partial computations copied for all ten households; no AllocationDrift breach records | H0001 AllocationDrift breaches (0/2) |
| [Full pair 2, 473a3eff](../artifacts/v3-sweep-20261116/full-small/runs/473a3eff695c4d78921bd2c7b73bcd15/timeline.md) | 3 | None; no stdout/result truncation | Executed hard-coded 100,000 totals and empty analysis copied for all ten households | H0001 allocation/drift: Equity |
| [Full pair 3, 8bad0feb](../artifacts/v3-sweep-20261116/full-small/runs/8bad0feb54e6468298b331708f504d41/timeline.md) | 4 | Initial `NameError: name 'json' is not defined`, then three successful executions; no truncation | Executed partial computations copied for all ten households; no AllocationDrift breach records | H0001 AllocationDrift breaches (0/2) |
| [Full pair 4, 9688b2aa](../artifacts/v3-sweep-20261116/full-small/runs/9688b2aad48145a2909f1b4f6c154c31/timeline.md) | 37 | Initial json NameError, then **36 successful empty `[]` outputs**; 120-second worker deadline while awaiting model request 38; no stdout/result truncation | No final model answer (raw-output file contains an unavailable sentinel); loop continues through off-scope empty batches, including `[175:180]` | Final report, including H0001 household; completeness unassessed |
| [Full pair 5, a323cb9b](../artifacts/v3-sweep-20261116/full-small/runs/a323cb9b0d374de3bbab06b92af0aa4c/timeline.md) | 3 | None; no stdout/result truncation | Executed allocation-drift-only analysis copied for all ten households; FX/crypto checks absent | H0001 FxExposure breach (0/1) |

**Capture nuance:** Full pair 4's final recorded request telemetry has original
length 67,330 characters, captured to 65,536. This is not truncated stdout, a tool
result, or evidence that the actual model input was truncated: `ObservedChatClient`
records a bounded projection, then forwards the original input to the client.
All 37 Python executions completed; the worker deadline occurred during the next
model request, not during an executing Python program.

**Turn-limit caveat:** the existing `AnalysisRunner` checks `MaxTurns` only after
`agent.RunAsync` returns; it does not interrupt the inner function-invocation loop
at turn 20. That explains 37 executions despite recorded `MaxTurns=20`: the
120-second worker deadline, not a proactive turn cap, stopped this loop.
This is a diagnostic finding, not a change to the loop, limits or historical results.

The largest observed stdout among these 13 trials is **14,031 bytes** (full pair 5),
with no stdout/tool-result capture truncation or guest output-buffer overflow observed.

### Dominant failure patterns

- **Incomplete analysis, not merely failed execution:** 9/13 are UnearnedCompleteness.
  Eight final reports reproduce executed household values, including explicit zero or
  100,000 placeholders and computations that omit policy-breach families. Seven of the
  13 trials have no execution exception at all.
- **Wrong field assumptions and unsupported finalization:** repeated lookup of absent
  `baseCurrency` fields leads to KeyErrors, guessed `Unknown`/empty FX inputs and eight
  recorded tool-argument errors. Three final answers add analytics absent from stdout;
  another acknowledges incompleteness with an empty household list. Two final outputs
  fail schema validation on currency values.
- **Termination is a distinct failure; truncation is not the observed explanation:**
  one trial runs 37 programs, mostly empty out-of-range batches, without reaching a final
  answer before its deadline; the configured turn cap is only checked after return.
  None of the 13 loses stdout/tool-result capture. The
  compact control proves these exact scopes are feasible under the same runtime/budget,
  while the preserved verbose prototype shows that the smaller guest output buffer
  is a real constraint, not an unlimited channel.

Reproduce the read-only mechanical ledger (no Python/model execution of artifacts):

```bash
python3 scripts/vm/inspect-v3-failures.py --sweep-root <existing-v3-sweep-root> \
  > <new-forensics-ledger.jsonl>
```

[Captured ledger](../artifacts/v3-codeact-forensics.jsonl).
The helper checks the explicit three profiles, prefers the separate reclassification,
and excludes correct trials, warm-ups and aborted directories. It distinguishes
stream/result truncation from other telemetry truncation; source interpretation and
the schema blockers in the table are manually checked, not inferred from match labels alone.

## Validation and preservation

- Release solution build: zero warnings/errors; Diagnosis is now in the solution because
  its control client is directly tested by the comparison test project.
- **283 offline non-live tests passed**, zero failed/skipped; includes four focused
  tests for exact program submission, verbatim stdout, absent stdout and failed execution.
- **Five supported real-Hyperlight tests passed**, zero failed/skipped, with no model.
  The existing native-cancellation known issue remains explicitly excluded, not counted
  as passed. An initial relative guest path failed two pre-execution file checks because
  testhost changes its working directory; rerunning the whole five-test selection with
  the documented absolute guest path passed.
- TRX: `artifacts/v3-diagnostics-tests/offline/` and
  `artifacts/v3-diagnostics-tests/hyperlight-absolute-path/`.
- The 1,001 source model/profile/summary files are protected by
  `artifacts/v3-diagnostics-inputs.sha256`; diagnostic outputs live outside that tree.
  No prompt, schema, oracle/scorer, tool catalog, datasets or model cohort changed.
