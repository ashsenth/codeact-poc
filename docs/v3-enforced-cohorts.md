# New v3 enforced, stateless Responses cohorts

November 17, 2026. Historical gpt-4o v3 is **not** the clean baseline: its
post-return-only turn checks allowed four of 15 Classic and one of 15 CodeAct
measured trials to exceed 20 requests. Those trials, scores and summaries remain
unchanged. See the [historical audit](infrastructure-gate-20261117.md).

## Preparation gate

The approved new cohorts use the **same shared Responses adapter in both modes and
both models**. Chat Completions remains selectable for historical reproduction.
No prompt, analysis output schema, local validator, oracle, scorer, tool, dataset,
guest or buffer change is part of this work. No v4/output-protocol work; no Medium/Large.

- `MODEL_API_MODE=responses`, `store=false`; no conversation or `previous_response_id`.
  Full history is sent each request, including encrypted reasoning items where emitted
  by the SDK. No server-side conversation continuation is used.
- Only function tools; no built-in/hosted tools. `parallel_tool_calls=true`.
  Existing JSON schema, `strict=false`, unchanged local v3 validation.
- One Responses call is one shared model request. The in-flight budget remains 20,
  wall-clock worker limit 120 seconds, max output 16,384 tokens, Classic batch size 0.
  Reasoning consumes that same output-token budget, not an extra allowance.
- gpt-4o retains temperature 0. gpt-6.1-sol omits unsupported sampling parameters
  and uses documented default **medium** reasoning. The model name, snapshot,
  API mode, default reasoning effort, stateless policy and limits are fingerprinted.
  gpt-4o's reasoning setting is recorded as `not-supported`, not sent as an API effort.
- Azure management metadata is checked before and after each response; returned
  model identifiers are checked too. A changed model/version/SKU is an infrastructure
  failure, invalidating the comparison rather than silently mixing snapshots.
- The pinned OpenAI SDK marks Responses experimental; only the affected adapter
  source/tests suppress that diagnostic. No SDK/dependency upgrade was made.

The [OpenAI model documentation](https://developers.openai.com/api/docs/models/gpt-6.1-sol)
states that gpt-6.1-sol tool calling requires Responses and that medium is its
default reasoning effort. [Azure reasoning documentation](https://learn.microsoft.com/en-us/azure/foundry/openai/how-to/reasoning)
describes usage accounting: reasoning is already billed as output, not an additional
token total.

### Verified deployment metadata

Both deployments are provisioned successfully in the East US account. Metadata
was retrieved November 17, 2026 through Azure management, without model inference.

| Cohort | Deployment/model | Model version | SKU | Upgrade policy |
|---|---|---|---|---|
| gpt-4o v3-enforced | gpt-4o | 2024-11-20 | GlobalStandard | OnceNewDefaultVersionAvailable; version guarded |
| gpt-6.1-sol v3 | gpt-6.1-sol | 2026-09-29 | GlobalStandard | NoAutoUpgrade |

gpt-6.1-sol reports **300 requests / 60 seconds** and **300,000 tokens / 60 seconds**.
No deployment settings were changed.

### Verified retail pricing

Source: **Azure Retail Prices API**, USD, East US, consumption, Global Standard.
Retrieved **2026-11-17**. The service is now named **Foundry Models**; filtering
only the former `Cognitive Services` name would incorrectly suggest no meter exists.
The [committed cohort configuration](../scripts/vm/cohort_config.py) contains exact
source URLs and meter names; the runner re-verifies them before each launch and
stores the matching meter IDs, effective dates, rates and deployment metadata.

| Model/context | Input USD/1M | Cached input USD/1M | Cache writes USD/1M | Output USD/1M |
|---|---:|---:|---:|---:|
| gpt-4o 2024-11-20 Global | 2.50 | 1.25 | No separate selected meter | 10.00 |
| gpt-6.1-sol short context | 2.00 | 0.10 | 2.50 | 10.00 |
| gpt-6.1-sol >272,000 input tokens/request | 4.00 | 0.20 | 5.00 | 15.00 |

gpt-4o's meters are per **1K** tokens and are multiplied by 1,000 for this table.
The eight Sol `Std Gl` meters are per **1M**, effective October 1, 2026.
Long-context pricing applies to the whole individual request above the documented
272K threshold, not the sum of input tokens across a trial. Output pricing includes
reasoning; no separate reasoning meter is added. Cached-read and reported cache-write
tokens replace their portion of ordinary input billing, not an extra ordinary-input
charge. Costs remain retail estimates, not invoices.

## Accounting and failure policy

Each new trial records input, cached input, output, reasoning, visible output and
total tokens, cache writes when returned, actual HTTP 429 responses, total actual
retry delay, sandbox-output overflows and TurnLimitReached. Output includes
reasoning; visible output is output minus reasoning when both are reported.
Absent usage remains unknown; reasoning is never counted twice.

Actual retry waits are measured around the SDK's unchanged retry policy, including
cancellation; 429 counts include the terminal exhausted response. Wait is excluded
from **analysis duration**, retained in process-inclusive duration and disclosed
separately. The 120-second deadline is unchanged and still includes waiting.
Exhausted retries/auth/transport failures remain infrastructure.

Model outcomes (including TurnLimitReached, invalid/incomplete answers and timeout)
do not stop profiles. Under the revised approved rule, a shared-output-buffer abort
is a **model-visible environment constraint** when the execution error is returned
and its matching function result is carried into a subsequent model request.
`SandboxOutputOverflow` remains a per-trial count, including warm-ups; recovery
does not erase that count. A host crash or sandbox failure without that delivery
and continuation evidence remains infrastructure. Infrastructure failure
invalidates the comparison and stops further launches.

The request journal now records function-result call IDs independently of the
65,536-character payload capture, so large history captures cannot hide delivery
identity. Legacy reclassification checks the original complete request capture.
The [parallel-call audit](v3-parallel-audit.md) confirms same-turn batching and
documents why 4o repeated calls until the enforced request cap. Sequential local
execution is unchanged and identical across both modes and APIs; it is now
explicit in configuration and its fingerprint.

### Historical overflow audit (read-only)

The committed [overflow audit](../scripts/vm/audit-sandbox-overflows.py) selects all
original historical live v3 CodeAct trials, including correct answers and the two
archived zero-trial attempts' warm-ups; it excludes offline controls and derived
reclassifications. It counts distinct execution invocation IDs from full captured
stderr, not duplicated error text in model requests/results.

The generated ledger is `artifacts/v3-responses-gate/historical-overflows.json`.
**Zero overflow errors** were found in all **15 measured CodeAct trials and five
warm-ups**. No historical trial is reclassified or edited by this audit.

## Reproduction and launch sequence

Use the committed [runner](../scripts/vm/run-v3-profile.py) and
[checker](../scripts/vm/check-v3-profile.py). Required environment:
`AZURE_OPENAI_ENDPOINT`, `AZURE_OPENAI_DEPLOYMENT_NAME`,
`AZURE_OPENAI_RESOURCE_ID`, `HYPERLIGHT_PYTHON_GUEST_PATH`, `DATASET_ROOT`,
`ARTIFACTS_ROOT`; Azure CLI and SDK authentication must have both inference access
and deployment metadata read access. No key/token belongs in a manifest or log.
Paths are caller-provided, never session-local constants in scripts.

`ARTIFACTS_ROOT` must be a **different new directory per cohort**. Set the deployment
to the matching cohort. The valid historical v3 reference is used only to preserve
prompt/schema/tool/dataset settings; new model/API/pricing fingerprints deliberately
differ. The runner requires a clean committed worktree and refuses existing outputs.

```bash
# First gpt-4o, then gpt-6.1-sol; never start the next if infrastructure fails.
python3 scripts/vm/run-v3-profile.py n1 --smoke \
  --cohort gpt-4o-v3-enforced --reference-comparison <historical-v3-comparison.json>
python3 scripts/vm/check-v3-profile.py n1 --smoke \
  --cohort gpt-4o-v3-enforced --reference-comparison <historical-v3-comparison.json>

# After BOTH smoke gates pass, use the same commands without --smoke,
# for n1, n3, full-small, five pairs per profile, independently per cohort.
# Check, summarize, document, commit and push after EACH profile.
python3 scripts/vm/check-v3-profile.py n1 --summarize \
  --cohort gpt-4o-v3-enforced --reference-comparison <historical-v3-comparison.json>
```

Invoke long-running profiles with the approved background/nohup execution mechanism.
The smoke is isolated in `smoke-n1/`, never part of the five-pair cohorts or summaries.

The [remaining-Sol helper](../scripts/vm/resume-sol-v3.py) runs N=3 then full Small,
checks each profile, regenerates the combined report, commits and pushes before
proceeding. It preserves all model outcomes and stops on infrastructure or a failed
validation/publication command. Required Azure/guest/dataset environment is the
same as above; paths are explicit command arguments:

```bash
python3 scripts/vm/resume-sol-v3.py \
  --reference-comparison "$REFERENCE_COMPARISON" \
  --gpt4o-root "$GPT4O_ENFORCED_ROOT" --sol-root "$SOL_ROOT" --log-root "$LOG_ROOT"
```

The explicit local-policy marker changes new literal model fingerprints without
changing execution behavior. The generic strict sweep merger remains unchanged
and rejects mixed literal fingerprints. For this continuation use the helper's
combined report, not `check-v3-profile.py --summarize`: it checks the explicit
policy or one of the two exact audited legacy fingerprints, keeps profile rows
separate, and never rewrites older metadata. Within-model deltas are primary,
cross-model deltas secondary. Equal correct counts alone permit the
`correctness-equivalent by count` label; equal zero counts are explicitly not an
efficiency win.

## Execution status

Current continuation gate: Release build, zero warnings/errors; **313 non-live
.NET tests, four Python report checks, and six supported real Hyperlight tests
passed**, zero failed/skipped in the final selected evidence. The Comparison
project was rerun with `xUnit.ParallelizeTestCollections=false` after an unchanged
global `Console.SetOut` test encountered concurrent log output. No unrelated
test/production logging code was changed. Native-cancellation known issue stays
excluded. The earlier preparation gate passed 300 .NET tests. Tests cover stateless encrypted-reasoning history, non-strict
schema/parallel function tools, exact 429/retry accounting and exhaustion, pricing
tiers/caches, returned-model mismatches, and multi-message reasoning responses
consuming only one shared turn in both modes. The continuation adds 52-call
same-next-request regressions for both models/modes, returned versus unreceived
overflow, warm-up handling, immutable reclassification and correctness-labelled
within-model deltas. A direct mocked comparison against
the pinned SDK's unmodified default also verifies that instrumentation preserves
its retry-exhaustion request bound; neither smoke needed HTTP retries.

Both isolated N=1 smoke gates passed on source `375c2d5`, in the required order:

- **gpt-4o:** VALID, reconciled, matched fingerprints; measured Classic Capped at
  20 requests and CodeAct Incomplete/Partial. Model failures did not stop the gate.
  Comparison `small-20261117171055-4c731da245b84fe6a57b0a3bedb6cb5a`.
- **gpt-6.1-sol:** VALID, reconciled, matched fingerprints; measured Classic
  WithinTolerance and CodeAct Exact. Recorded reasoning/visible tokens were
  1,841/2,250 and 28/1,762 respectively, with cached/read-write usage preserved.
  Comparison `small-20261117171400-a7cc7386302e40a0b1dc5a5fed8e9ecc`.

Both have zero 429s, retry waits, tool-argument errors and output overflows.
The directories are `artifacts/v3-enforced-20261117/{gpt-4o,gpt-6.1-sol}/smoke-n1/`.
They are excluded from measured cohort samples and summaries.

### Measured results and initial stop

The [generated results](v3-responses-results.md) contain all measured
outcomes, token breakdowns, operational counters and descriptive deltas. Four
five-pair profiles completed VALID, each checked, committed and pushed before the
next launch:

- gpt-4o N=1: `13c5dcce91be07d21a91d9917de8defe6b63d3b8`.
- gpt-4o N=3: `b86dd1fc21ffdb4a1f075db4c59996616c850e4e`.
- gpt-4o full Small: `b9d57964b6fb6b22b53c168895d9d3eaa0d4a3a0`.
- gpt-6.1-sol N=1: `80c35245bf926e6c26a753eab777ae5dabb1fcb1`.

**The initial policy stopped Sol N=3 during warm-up.** Original comparison
`small-20261117180346-e492436a074f480abee16ad17fe1789a` recorded INVALID:
`SandboxOutputOverflow`, with `MissingOrUnpairedTrials` because no measured pairs
were started. The CodeAct warm-up (`9afee5063f7b4c038c109d59d0dbebfd`) recorded one
guest shared-output serialization abort: **Required 16,396 bytes, Available 16,376**.
Its full captured stderr was not truncated. The model subsequently recovered across
five executions and returned an Exact answer. That overflow is now reclassified
as a model-visible constraint, not infrastructure (details below).
The Classic warm-up timed out at the unchanged 120-second
deadline; that alone was a model outcome, not the stop reason.

No Sol N=3 measured trials or Sol full Small runs were launched. No retry, buffer
workaround, v4 work, or Medium/Large run was performed. Warm-up errors are not mixed
into measured table counters.

### Derived reclassification and manual-launch hold

The stopped profile is archived as
`artifacts/v3-enforced-20261117/gpt-6.1-sol/aborted-20261117T183945Z/`.
All **56 original files are byte-identical** to the pre-archive SHA-256 manifest.
The offline reclassifier wrote a separate derived report under
`artifacts/v3-continuation-gate/sol-n3-reclassified/`; `--output-root` prevents any
source/index overwrite.

- CodeAct warm-up: still **Completed / Exact**, one overflow, infrastructure false.
- Classic warm-up: still **TimedOut / TimedOut**, zero overflows, infrastructure false.
- Warm-up infrastructure failures: **zero**, down from one.
- Measured trials: still **zero**. The derived comparison remains INVALID solely
  for `MissingOrUnpairedTrials`, not for overflow.

Both original and derived attempts are excluded from measured summaries by
explicit profile-directory selection. At the user's instruction, **do not launch**
the continuation from this session: publish the committed wrapper and exact
manual launch/progress commands instead.

Profiles used the committed runner under managed background supervision, not
literal `nohup`. Both deployments retained their pinned versions throughout all
observed requests. Independent recomputation from raw Responses usage verified
per-request cache/context pricing, reasoning/visible/total partitions, request
budgets and operational counters across all 58 new runs (40 measured, ten
non-smoke warm-ups, eight isolated smoke runs including their warm-ups). No run
exceeded 20 requests. All new runs had zero HTTP 429s and retry wait; all measured
trials had zero tool-argument and overflow errors. The sole new overflow was the
stopped warm-up above.

All 1,858 historical evidence-file hashes still match the pre-run preservation
manifest. Prompt, schema, oracle, scorer, tools, datasets, guest and dependency
versions remain unchanged.
