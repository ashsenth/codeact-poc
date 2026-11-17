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
do not stop profiles. A real shared-output-buffer abort is explicitly recorded as
`SandboxOutputOverflow`, a sandbox/runtime infrastructure failure even when its
error envelope is returned to the model. It must not be hidden by a later answer.
Infrastructure failure invalidates the comparison and stops further launches.

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

## Execution status

Release build: zero warnings/errors. **299 offline tests and six supported real
Hyperlight tests passed**, zero failed/skipped. Native-cancellation known issue stays
excluded. The new tests cover stateless encrypted-reasoning history, non-strict
schema/parallel function tools, exact 429/retry accounting and exhaustion, pricing
tiers/caches, returned-model mismatches, and multi-message reasoning responses
consuming only one shared turn in both modes.

Preparation only at this commit. No smoke or measured cohort has started yet.
The final cross-model table will retain all five measured outcomes per scope/mode,
and label token/cost/time deltas descriptive when correctness differs.
