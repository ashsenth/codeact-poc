# Sterling Vale: Classic tool-calling vs. CodeAct

Sterling Vale is a .NET proof of concept asking whether an AI agent analyzes a synthetic wealth portfolio more correctly or efficiently by calling seven read-only tools directly (**Classic**) or by writing Python that calls the same tools inside a **Hyperlight** micro-VM (**CodeAct**). Both modes receive the same data, shared task instructions, report schema and within-model configuration. A deterministic oracle scores their structured answers; paired trials retain failures and expose execution evidence. Correctness comes before tokens, time or estimated cost. This is an experiment, not a production service or investment advice.

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

See [cohort status and gates](docs/v3-enforced-cohorts.md), [generated within-model results](docs/v3-responses-results.md), and [known limits](#known-limits).

## What the evidence says

Read the evidence in this order; successful execution or a VALID comparison is not necessarily a correct answer.

### 1. Feasibility control: the Small task can be done

A hand-authored callback-only Python program ran through the real Hyperlight/service/worker path, without an LLM or oracle substitution. It scored **Exact** for N=1, N=3 and full Small, emitting 1,638 / 4,698 / 15,274 UTF-8 bytes with 23 / 39 / 84 host callbacks.

This demonstrates feasibility for those inputs and concise output, not model ability or a speed comparison. It is separate from the no-guest fake-model demo.
Evidence: [program, control method and failed prototypes](docs/v3-diagnostics.md#1-hand-authored-feasibility-control).

### 2. gpt-4o: both orchestrations have correctness failures

Historical prompt-v3 Chat Completions produced unearned completeness in both modes. Across five pairs per scope, Classic was correct 0/5 at N=1, N=3 and full Small; CodeAct was correct 2/5, 0/5 and 0/5. Schema-valid complete claims often omitted allocations, required breaches or trades.

Those historical runs did **not** enforce the configured 20-request cap in flight: four Classic and one CodeAct measured trials exceeded it. Their resources are not a fair enforced-budget baseline.

The newer enforced Responses cohort is separate. Its 4o results again show Classic 0/5 in every scope and CodeAct 2/5, 0/5, 0/5. Most Classic outcomes hit TurnLimitReached, while CodeAct still emitted unearned complete reports. This does not mean 4o never succeeds or establish a model-internal cause.

Evidence: [failure forensics](docs/v3-diagnostics.md#2-all-non-correct-measured-v3-codeact-trials), [historical request audit](docs/infrastructure-gate-20261117.md#2-read-only-historical-audit).

### 3. Sol: correct in both modes at N=1, CodeAct correct at larger scopes

At N=1 Sol scored **5/5 in both modes**: Classic WithinTolerance, CodeAct Exact. At N=3 and full Small, Classic timed out in **all five trials per scope**, while CodeAct scored **Exact 5/5**. No Sol measured trial claimed unearned completeness. One returned sandbox overflow was counted in CodeAct at each larger scope.

Do not generalize N=1's equivalence to the larger scopes, or count timeouts as cheap successful analyses. Evidence: [final cohort record](docs/v3-enforced-cohorts.md#final-status-november-17-2026).

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

- [Generated full results](docs/v3-responses-results.md): full counters, token partitions, retry wait and correctness-labelled deltas.
- [Model/API/pricing and execution record](docs/v3-enforced-cohorts.md), [same-turn parallel-call audit](docs/v3-parallel-audit.md), [canonical methodology](docs/benchmark-methodology.md).
- Sol publication commits: N=1 `80c35245bf926e6c26a753eab777ae5dabb1fcb1`, N=3 `1b55bea6871614a1f564381f416519582a9ae909`, full Small `6a50d817dc485fb49344133d3f081363bf45de09`.
- Comparison IDs, scopes N=1 / N=3 / full Small: Sol `small-20261117175030-08e828b0af534dfdbaea775967807011`, `small-20261117190251-4e21059d12b740ad99ebae1811d9d9d4`, `small-20261117192039-a9fbc87c067546e891e0690922317f78`; 4o `small-20261117172306-877c46337ba1469aa242d5fc11783121`, `small-20261117173158-1dda532076ea4b31b13eca81e8fa3f3f`, `small-20261117173950-867fc9f253c04d61a33121bc4a9c79b1`.
- Raw local evidence layout: `artifacts/v3-enforced-20261117/{gpt-4o,gpt-6.1-sol}/{n1,n3,full-small}/comparisons/` and sibling `runs/`. These are **ignored local evidence**, not files supplied by a fresh clone. Request a reviewed evidence bundle for independent raw-event inspection.

## Scenario and implementation

Seeded synthetic data contains:

| Profile | Households | Accounts | Positions |
|---|---:|---:|---:|
| Small | 10 | 33 | 370 |
| Medium | 40 | 156 | 2,201 |
| Large | 120 | 485 | 7,198 |

For each in-scope household the agent retrieves holdings, prices, FX and policy; values holdings in base currency; calculates allocations/drift; checks drift, concentration, FX and crypto breaches; proposes asset-class buy/sell notionals; and returns `exposure-report/v2`.

Classic exposes seven granular functions to the model. CodeAct exposes `execute_code`; Python calls those same functions via `call_tool`. Tools are direct **AIFunction callbacks, not MCP**, and expose no oracle calculations. Both modes allow multiple calls per model response; local function execution is sequential within the response, and results are returned together in the next request.

The CLI runs paired trials and scoring without separate HTTP hosts. The two optional APIs share `POST /api/v1/analyses` and run retrieval.

- [Architecture](docs/architecture.md), [oracle calculation and tolerance rules](docs/metrics.md), [SDK findings](docs/sdk-findings.md).
- [Committed sanitized live sample](docs/sample-run/README.md) and [independent household hand-check](docs/sample-run/diagnosis.md): historical v2 evidence, not current scorer results.

## Quickstart without Azure

Requires Git, Bash and a .NET 9 SDK selected by [global.json](global.json): 9.0.317 with `latestFeature` roll-forward. Initial locked restore needs package-feed access. No Azure, Python, guest or hypervisor is needed for this demo. Windows users can use an approved Bash/WSL environment with its own .NET installation; do not paste Bash syntax into PowerShell.

Clone your authorized repository URL and select the published source revision to reproduce. No private clone URL belongs in shared documentation.

```bash
git clone "<repository-url>" codeact-poc
cd codeact-poc
git switch --detach "<published-source-commit>"
```

In a new Bash shell at the repository root, stop on any failure:

```bash
set -euo pipefail
unset AZURE_OPENAI_ENDPOINT AZURE_OPENAI_API_KEY AZURE_OPENAI_DEPLOYMENT_NAME
unset AZURE_OPENAI_RESOURCE_ID AZURE_OPENAI_MODEL_NAME AZURE_OPENAI_MODEL_VERSION
unset MODEL_API_MODE MODEL_DEFAULT_REASONING_EFFORT
unset PRICING_CACHED_INPUT_PER_MILLION PRICING_CACHE_WRITE_PER_MILLION
unset PRICING_LONG_CONTEXT_THRESHOLD PRICING_LONG_INPUT_PER_MILLION
unset PRICING_LONG_OUTPUT_PER_MILLION PRICING_LONG_CACHED_INPUT_PER_MILLION
unset PRICING_LONG_CACHE_WRITE_PER_MILLION PRICING_SOURCE_URL PRICING_VERIFIED_ON PRICING_BASIS
export MODEL_MODE=fake HYPERLIGHT_PYTHON_GUEST_PATH=""
export BENCHMARK_REQUIRE_LIVE=false CLASSIC_BATCH_SIZE=0 ENABLE_LIVE_TESTS=false
export BENCHMARK_MAX_TURNS=12 BENCHMARK_TIMEOUT_SECONDS=120
export PRICING_INPUT_PER_MILLION=2.50 PRICING_OUTPUT_PER_MILLION=10.00
export DATASET_ROOT="$PWD/data" ARTIFACTS_ROOT="$PWD/artifacts/offline-demo"
export OTEL_EXPORTER_OTLP_ENDPOINT=""
test ! -e "$ARTIFACTS_ROOT"
dotnet restore --locked-mode
dotnet build -c Release --no-restore
dotnet run -c Release --no-build --project src/SterlingVale.DataGenerator -- "$DATASET_ROOT"
dotnet run -c Release --no-build --project src/SterlingVale.Benchmark -- validate-data
dotnet run -c Release --no-build --project src/SterlingVale.Benchmark -- \
  compare --trials 1 --model fake --log-level normal --payload-capture full
dotnet run -c Release --no-build --project src/SterlingVale.Benchmark -- visualize
dotnet test -c Release --no-build \
  --filter 'Category!=LiveModel&Category!=Hyperlight&Category!=HyperlightKnownIssue' \
  -- xUnit.ParallelizeTestCollections=false
```

Expected: all three datasets validate; each comparison has two measured trials (one per mode), both Exact, matched fingerprints and valid telemetry. Open the HTML path printed by `visualize`. Do not reuse a stale dashboard as evidence of success.

The fake model deliberately emits an oracle-derived answer. With the guest unset, CodeAct's stand-in runs **no Python and no host data callbacks**. Fake tokens/costs are synthetic. Medium/Large offline Exact scores do not establish real-sandbox feasibility.

For Python runner/report/provisioning-plan checks, use Python 3:

```bash
python3 scripts/vm/test_cohort_reports.py
bash -n scripts/vm/run-sol-remaining.sh
```

These are offline checks; provisioning tests inspect printed plans, not Azure resources. Counts change with source; inspect actual results, not just an old total.
Historical Windows ARM64 and Ubuntu x64 local-source clean-clone checks are recorded in [the x64 workflow](docs/review-current/x64-validation.md). A remote-origin, completely fresh OS reproduction is not certified.

## Azure setup, end to end

**Administrative templates, not automatic actions.** Obtain approval for resources, quotas, costs and access assignments. The committed cohort verifier currently accepts **East US only**. If the exact model/version/SKU is unavailable, stop; do not silently substitute another model, region or deployment type.

### Account, deployments and capacity

Run administrative commands in an authorized operator shell. Use placeholders locally; keep actual IDs/endpoints out of published logs.

```bash
export SUBSCRIPTION="<subscription-id>" AZURE_RG="<new-model-resource-group>"
export ACCOUNT="<globally-unique-account-name>" REGION=eastus
az login --tenant "<tenant-id>"
az account set --subscription "$SUBSCRIPTION"
az provider register --namespace Microsoft.CognitiveServices
az group create --name "$AZURE_RG" --location "$REGION"
az cognitiveservices account create --resource-group "$AZURE_RG" --name "$ACCOUNT" \
  --location "$REGION" --kind OpenAI --sku S0 --custom-domain "$ACCOUNT"
```

Confirm model availability, subscription quota, and approved **SKU capacity units** for each deployment before creating it. Capacity units are model-specific, not a universal TPM/RPM conversion. The old Sol account reported 300 requests and 300,000 tokens per 60 seconds; that does not document its SKU capacity. Fill capacity from approved quota; this repository does not establish a portable numeric capacity.

```bash
export GPT4O_CAPACITY="<approved-gpt4o-capacity-units>"
export SOL_CAPACITY="<approved-sol-capacity-units>"
az cognitiveservices account list-models --resource-group "$AZURE_RG" --name "$ACCOUNT"
az cognitiveservices account deployment create --resource-group "$AZURE_RG" \
  --name "$ACCOUNT" --deployment-name gpt-4o --model-format OpenAI \
  --model-name gpt-4o --model-version 2024-11-20 \
  --sku-name GlobalStandard --sku-capacity "$GPT4O_CAPACITY" \
  --version-upgrade-option NoAutoUpgrade
az cognitiveservices account deployment create --resource-group "$AZURE_RG" \
  --name "$ACCOUNT" --deployment-name gpt-6.1-sol --model-format OpenAI \
  --model-name gpt-6.1-sol --model-version 2026-09-29 \
  --sku-name GlobalStandard --sku-capacity "$SOL_CAPACITY" \
  --version-upgrade-option NoAutoUpgrade
az cognitiveservices account deployment list --resource-group "$AZURE_RG" --name "$ACCOUNT" \
  --query '[].{deployment:name,model:properties.model,sku:sku,upgrade:properties.versionUpgradeOption,limits:properties.rateLimits}'
export AZURE_OPENAI_RESOURCE_ID="$(az cognitiveservices account show \
  --resource-group "$AZURE_RG" --name "$ACCOUNT" --query id -o tsv)"
export AZURE_OPENAI_ENDPOINT="$(az cognitiveservices account show \
  --resource-group "$AZURE_RG" --name "$ACCOUNT" --query properties.endpoint -o tsv)"
```

Use the supported **base HTTPS resource endpoint**, such as `https://<resource-name>.openai.azure.com/`, with `/` and no `/openai/v1`, deployment/project path, query or fragment. A supported `services.ai.azure.com` resource endpoint may also be used. Do not invent an endpoint from a project URL.

For a new reproducible setup pin both upgrades to `NoAutoUpgrade`. Historical provenance differs: observed 4o had `OnceNewDefaultVersionAvailable` with a version guard; Sol had `NoAutoUpgrade`. The verifier checks exact model/version/GlobalStandard; it does not pin numeric capacity.

### Managed identity and permissions

After provisioning the VM below, an authorized administrator enables its system-assigned identity and grants inference access at the model account scope:

```bash
PRINCIPAL_ID="$(az vm identity assign --resource-group "$VM_RG" --name "$VM_NAME" \
  --query systemAssignedIdentity -o tsv)"
az role assignment create --assignee-object-id "$PRINCIPAL_ID" \
  --assignee-principal-type ServicePrincipal \
  --role "Cognitive Services OpenAI User" --scope "$AZURE_OPENAI_RESOURCE_ID"
```

The runner's CLI and app's SDK also need management-plane deployment metadata read access. Verify effective permissions; if necessary an administrator can grant resource-scoped Reader:

```bash
az role assignment create --assignee-object-id "$PRINCIPAL_ID" \
  --assignee-principal-type ServicePrincipal --role Reader --scope "$AZURE_OPENAI_RESOURCE_ID"
```

On the VM, unset unintended identity/key overrides if the intended identity is the VM's system identity:

```bash
unset AZURE_OPENAI_API_KEY AZURE_CLIENT_ID AZURE_CLIENT_SECRET AZURE_TENANT_ID
unset AZURE_FEDERATED_TOKEN_FILE AZURE_CLIENT_CERTIFICATE_PATH
az login --identity
az account set --subscription "<subscription-id>"
az rest --url "https://management.azure.com${AZURE_OPENAI_RESOURCE_ID}?api-version=2024-10-01" \
  --query location -o tsv
```

CLI login supplies the runner's metadata checks; the app independently uses `DefaultAzureCredential` (recorded VM selection: `ManagedIdentityCredential`). An inference API key does not remove authenticated metadata checks. Allow RBAC propagation and account network access; a 403 does not uniquely identify a missing role.

### API routes and model settings

| Setting | Current enforced cohorts | Historical reproduction |
|---|---|---|
| `MODEL_API_MODE` | `responses` | `chat-completions` |
| SDK route | `/openai/v1/responses` | `/openai/deployments/{deployment}/chat/completions` with SDK API-version handling |
| State | Full history, `store=false`, no conversation/`previous_response_id`; encrypted reasoning items preserved where emitted | Chat history |
| Tools/output | Function-only, `parallel_tool_calls=true`, non-strict JSON schema (`strict=false`), unchanged full local validation | Select historical source/configuration explicitly |
| 4o | `gpt-4o`, snapshot `2024-11-20`, temperature 0 | Separate API/enforcement cohort |
| Sol | `gpt-6.1-sol`, snapshot `2026-09-29`, medium reasoning; unsupported sampling settings omitted | Sol tool calling requires Responses |

Keep the environment endpoint at the resource base: the adapter appends its route.

### Verify prices before inference

[cohort_config.py](scripts/vm/cohort_config.py) contains exact official Azure Retail Prices API URLs and meter names. The runner follows pagination and fails closed if a selected USD Consumption meter is missing, ambiguous or changed. Do not filter only on the old service name `Cognitive Services`; these meters can be under `Foundry Models`.

Recorded November 17, 2026, East US GlobalStandard, USD per million:

| Model/request context | Input | Cached read | Cache write | Output, including reasoning |
|---|---:|---:|---:|---:|
| 4o 2024-11-20 | 2.50 | 1.25 | No separate selected meter | 10.00 |
| Sol, <=272,000 input tokens/request | 2.00 | 0.10 | 2.50 | 10.00 |
| Sol, >272,000 input tokens/request | 4.00 | 0.20 | 5.00 | 15.00 |

4o meters are per 1K, multiplied by 1,000; Sol meters are per 1M. Long-context pricing applies per request, not across a trial. Cached reads/writes replace their part of ordinary input billing; reasoning is already output. Unknown usage stays unknown.

Metadata/retail-only check from the repository root (no inference):

```bash
PYTHONDONTWRITEBYTECODE=1 python3 - <<'PY'
import sys
sys.path.insert(0, "scripts/vm")
from cohort_config import verify
for cohort in ("gpt-4o-v3-enforced", "gpt-6.1-sol-v3"):
    value = verify(cohort)
    print(cohort, value["verifiedAtUtc"], "deployment and retail meters verified")
PY
```

Costs are retail estimates, not invoices; negotiated discounts, missing usage and VM/storage/network charges are excluded. [Full pricing provenance](docs/v3-enforced-cohorts.md#verified-retail-pricing).

## Ubuntu x64 VM and real Hyperlight

Use Ubuntu 24.04 **x64**, with nested virtualization and readable/writable `/dev/kvm`. The recorded host used `Standard_D4s_v5`. SKU documentation alone is not proof of working KVM; ARM64, ordinary containers and hosted development environments are not substitutes.

### Provisioning and corporate-network SSH

Review current VM price/quota, networking and corporate policy first. The historical USD 0.192/hour estimate is not current approval.

For this corporate environment register the subscription feature before provisioning the approved public-IP setup:

```bash
az feature register --namespace Microsoft.Network --name AllowBringYourOwnPublicIpAddress
az feature show --namespace Microsoft.Network --name AllowBringYourOwnPublicIpAddress \
  --query properties.state -o tsv
# Wait for Registered; then refresh provider registration.
az provider register --namespace Microsoft.Network
```

Registration can require administrator approval. Connect the client to the corporate VPN. **Both the NIC NSG and subnet NSG must allow TCP 22 from `CorpNetPublic`.** A rule on only one NSG is insufficient.

The [provisioning script](scripts/provision-vm.sh) now creates separate NIC/subnet NSGs, the VNet/subnet association, and CorpNetPublic rules on both. It no longer discovers your current public IP or accepts `--ssh-ip`. Its ECDSA public-key token bug is fixed. It prints a plan by default; creation requires a new dedicated resource group and explicit approval:

```bash
export VM_RG="<new-vm-resource-group>" VM_NAME="<vm-name>" VM_REGION="<approved-vm-region>"
export ADMIN_USER="<ssh-user>" SSH_PUBLIC_KEY="<absolute-path-to-approved-public-key.pub>"
bash scripts/provision-vm.sh --subscription "$SUBSCRIPTION" --resource-group "$VM_RG" \
  --location "$VM_REGION" --name "$VM_NAME" --admin-user "$ADMIN_USER" \
  --ssh-public-key "$SSH_PUBLIC_KEY" --estimated-hourly-usd "<reviewed-compute-price>"
# Review the complete plan, feature registration, price/quota and policy; only then
# repeat the same command with --approved-create.
```

Corporate policy can delete SSH allow rules after creation. Before first connection and after a timeout/restart inspect BOTH associations/effective rules:

```bash
NIC_ID="$(az vm show --resource-group "$VM_RG" --name "$VM_NAME" \
  --query 'networkProfile.networkInterfaces[0].id' -o tsv)"
SUBNET_ID="$(az network nic show --ids "$NIC_ID" --query 'ipConfigurations[0].subnet.id' -o tsv)"
az network nic show --ids "$NIC_ID" --query networkSecurityGroup.id -o tsv
az network vnet subnet show --ids "$SUBNET_ID" --query networkSecurityGroup.id -o tsv
az network nic list-effective-nsg --ids "$NIC_ID"
```

If policy removed the approved allow, an authorized operator can recreate it on each affected NSG (script-created names below):

```bash
for nsg in "$VM_NAME-nsg" "$VM_NAME-subnet-nsg"; do
  az network nsg rule create --resource-group "$VM_RG" --nsg-name "$nsg" \
    --name AllowSshFromCorpNet --priority 100 --direction Inbound --access Allow \
    --protocol Tcp --source-address-prefixes CorpNetPublic --source-port-ranges '*' \
    --destination-address-prefixes '*' --destination-port-ranges 22
done
```

Resolve recurring deletion with the policy administrator; do not bypass policy with a world-open/current-IP rule. If `CorpNetPublic` is unavailable, use only the administrator-approved equivalent. Verify the SSH host-key fingerprint through a trusted channel, then connect from the VPN:

```bash
ssh -i "<client-private-key-path>" "<ssh-user>@<vm-public-ip>"
```

### Repository access, bootstrap and guest path

On the VM use a repository-specific deploy key approved by the repository administrator. Generate it in the SSH user's home, never the checkout; register only the `.pub` value in repository **Settings > Deploy keys**. Read access suffices for cloning; automatic publication requires **Allow write access**. Protect the private file and never publish key material or authentication logs.

```bash
ssh-keygen -t ed25519 -f "$HOME/.ssh/codeact-repo" -C "benchmark-repo-deploy-key"
chmod 600 "$HOME/.ssh/codeact-repo"
```

Verify GitHub host fingerprints through the organization's trusted process; never disable host-key checking. Load the key into an approved agent, or set a process-local transport:

```bash
export GIT_SSH_COMMAND="ssh -i $HOME/.ssh/codeact-repo -o IdentitiesOnly=yes"
git clone "<approved-SSH-repository-URL>" codeact-poc
cd codeact-poc
git switch "<approved-results-branch>"
git config --local user.name "<approved-commit-author>"
git config --local user.email "<approved-commit-email>"
bash scripts/bootstrap-vm.sh
# After reviewing its plan and obtaining installation approval:
bash scripts/bootstrap-vm.sh --approved-install
```

Bootstrap installs prerequisites, pinned .NET SDK, Azure CLI, PowerShell and Hyperlight Python 0.4.0, loads KVM and adds the SSH user to `kvm`. Its current ordinary-sudo calls require an approved interactive sudo authentication setup; do not run it as an unattended job that may wait for a password.

Disconnect **all** SSH/Remote-SSH sessions and reconnect so the new group applies:

```bash
cd "<clone-directory>"
id -nG
test -r /dev/kvm && test -w /dev/kvm
export HYPERLIGHT_PYTHON_GUEST_PATH="$PWD/artifacts/guest/0.4.0/runtimes/linux-x64/native/python-sandbox.aot"
test -f "$HYPERLIGHT_PYTHON_GUEST_PATH"
export DATASET_ROOT="$PWD/data"
dotnet restore --locked-mode
dotnet build -c Release --no-restore
dotnet run -c Release --no-build --project src/SterlingVale.DataGenerator -- "$DATASET_ROOT"
dotnet test tests/SterlingVale.CodeAct.IntegrationTests -c Release --no-build \
  --filter 'Category=Hyperlight&Category!=HyperlightKnownIssue' \
  --blame-hang --blame-hang-timeout 3m
```

Require actual execution with zero skips; the supported selection has six tests. **Never include the retained native-cancellation known-issue test in an unattended unfiltered run.** Always use an absolute guest path: testhost changes its working directory. Guest presence is not proof of callbacks or correct execution.

Optional no-Azure real-guest feasibility check, using a fresh directory outside model cohort roots:

```bash
dotnet scripts/vm/Diagnosis/bin/Release/net9.0/Diagnosis.dll control \
  scripts/vm/reference-analysis-control.py "<new-control-output-directory>"
```

### Auto-shutdown, deallocation and restart

Configure reviewed auto-shutdown as a cost backstop, outside the cohort run window:

```bash
az vm auto-shutdown --resource-group "$VM_RG" --name "$VM_NAME" --time "<HHmm-UTC>"
# After all runs/publication finish, explicitly stop billable compute:
az vm deallocate --resource-group "$VM_RG" --name "$VM_NAME"
az vm get-instance-view --resource-group "$VM_RG" --name "$VM_NAME" \
  --query "instanceView.statuses[?starts_with(code, 'PowerState/')].code" -o tsv
# Resume later:
az vm start --resource-group "$VM_RG" --name "$VM_NAME"
az vm show --resource-group "$VM_RG" --name "$VM_NAME" --show-details --query publicIps -o tsv
```

Verify `PowerState/deallocated`: an OS shutdown or stopped-but-allocated VM may still incur compute charges. Disks and some networking remain billable. Recheck public IP, VPN and BOTH NSGs after restart; restore shell environment, verify KVM/guest, identity access and repository state. Never deallocate while the controller is running or publishing.

## Running current cohorts

Use committed [runner](scripts/vm/run-v3-profile.py), [checker](scripts/vm/check-v3-profile.py), and [combined reporter](scripts/vm/report-v3-cohorts.py). Python 3 standard library only. Legacy setup/run helpers now delegate to these approved paths; they no longer assume a root-user checkout or silently regenerate lock files.

### Environment and committed reference

| Operator variable | Required value |
|---|---|
| `AZURE_OPENAI_ENDPOINT` | Supported account base HTTPS endpoint. |
| `AZURE_OPENAI_RESOURCE_ID` | Full account ARM resource ID; keep actual value private. |
| `AZURE_OPENAI_DEPLOYMENT_NAME` | `gpt-4o` or `gpt-6.1-sol`, matching `--cohort`. |
| `HYPERLIGHT_PYTHON_GUEST_PATH` | Absolute 0.4.0 guest path. |
| `DATASET_ROOT` | Absolute generated data root, shared by both modes. |
| `ARTIFACTS_ROOT` | Distinct new root per cohort; runner adds `smoke-n1`, `n1`, `n3`, `full-small`. |

The runner sets `BENCHMARK_REQUIRE_LIVE=true`, `BENCHMARK_MAX_TURNS=20`, `BENCHMARK_TIMEOUT_SECONDS=120`, `CLASSIC_BATCH_SIZE=0`, and clears `OTEL_EXPORTER_OTLP_ENDPOINT`. `--cohort` sets `MODEL_API_MODE=responses`, `AZURE_OPENAI_MODEL_NAME`, `AZURE_OPENAI_MODEL_VERSION`, `MODEL_DEFAULT_REASONING_EFFORT`, and all applicable pricing variables:

`PRICING_INPUT_PER_MILLION`, `PRICING_OUTPUT_PER_MILLION`, `PRICING_CACHED_INPUT_PER_MILLION`, `PRICING_CACHE_WRITE_PER_MILLION`, `PRICING_LONG_CONTEXT_THRESHOLD`, `PRICING_LONG_INPUT_PER_MILLION`, `PRICING_LONG_OUTPUT_PER_MILLION`, `PRICING_LONG_CACHED_INPUT_PER_MILLION`, `PRICING_LONG_CACHE_WRITE_PER_MILLION`, `PRICING_SOURCE_URL`, `PRICING_VERIFIED_ON`, `PRICING_BASIS`.

Fresh clones default to [configs/v3-reference.json](configs/v3-reference.json): shared prompt/schema/tools/Small-dataset fingerprints, output/request/deadline limits, historical pricing basis and explicit current execution/concurrency policy. It is derived from the existing valid historical reference; the cohort config replaces historical uncached pricing with verified cache/context rates. A regression test regenerates the data/tools and compares current prompt/schema/policy fingerprints to this file.

**No historical artifact directory or fake seed run is required.** `--reference-comparison <path>` remains optional for historical replay; that explicit path still requires its sibling `runs/<runId>/metadata.json`. Do not mix the reference/control into measured model results. Without either `--cohort` or an explicit historical reference, run/check default to `gpt-4o-v3-enforced`. Always name Sol explicitly.

### Smoke gates, then measured profiles

Build/test the clean source first. Select an approved publication branch rather than detached HEAD; ensure its commit is already pushed. The runner checks clean worktree, not whether HEAD is pushed or binaries are fresh.

```bash
set -euo pipefail
export GPT4O_ENFORCED_ROOT="$PWD/artifacts/<new-experiment>/gpt-4o"
export SOL_ROOT="$PWD/artifacts/<new-experiment>/gpt-6.1-sol"
export LOG_ROOT="$PWD/artifacts/<new-experiment>/logs"
mkdir -p "$LOG_ROOT"
export ARTIFACTS_ROOT="$GPT4O_ENFORCED_ROOT" AZURE_OPENAI_DEPLOYMENT_NAME=gpt-4o
python3 scripts/vm/run-v3-profile.py n1 --smoke --cohort gpt-4o-v3-enforced
python3 scripts/vm/check-v3-profile.py n1 --smoke --cohort gpt-4o-v3-enforced
export ARTIFACTS_ROOT="$SOL_ROOT" AZURE_OPENAI_DEPLOYMENT_NAME=gpt-6.1-sol
python3 scripts/vm/run-v3-profile.py n1 --smoke --cohort gpt-6.1-sol-v3
python3 scripts/vm/check-v3-profile.py n1 --smoke --cohort gpt-6.1-sol-v3
```

Both isolated smoke gates must pass. Interpret VALID using the [canonical validity rules](docs/benchmark-methodology.md#validity-rules), not as oracle success.
Run measured **4o n1 -> n3 -> full-small**, then **Sol n1 -> n3 -> full-small**. Each profile is five measured pairs (10 trials) plus two unmeasured warm-ups. Never start the next until the previous was checked, reported, committed and pushed.

For one direct profile use `setsid nohup`; conditional `wait` records a failed child exit before the explicit success check stops the sequence:

```bash
set -euo pipefail
export ARTIFACTS_ROOT="$GPT4O_ENFORCED_ROOT" AZURE_OPENAI_DEPLOYMENT_NAME=gpt-4o
profile=n1
log="$LOG_ROOT/gpt4o-$profile.log"
test ! -e "$log"
setsid nohup python3 scripts/vm/run-v3-profile.py "$profile" \
  --cohort gpt-4o-v3-enforced > "$log" 2>&1 < /dev/null &
pid=$!
printf '%s\n' "$pid" > "$LOG_ROOT/gpt4o-$profile.pid"
if wait "$pid"; then rc=0; else rc=$?; fi
printf '%s\n' "$rc" > "$LOG_ROOT/gpt4o-$profile.exit"
test "$rc" -eq 0
python3 scripts/vm/check-v3-profile.py "$profile" --cohort gpt-4o-v3-enforced
```

Monitor in another terminal with `tail -f "$log"` and `ps -p <recorded-pid> -o pid,etime,args`. If the launching shell disconnects, its exit file may be absent although the nohup child continues; absence of a process or exit file is not success. Inspect the completed comparison and rerun the checker. Never launch a replacement while the original is alive.

After each checked profile regenerate and publish:

```bash
python3 scripts/vm/report-v3-cohorts.py --gpt4o-root "$GPT4O_ENFORCED_ROOT" \
  --sol-root "$SOL_ROOT" --output docs/v3-responses-results.md
git add docs/v3-responses-results.md
git commit -m "Record checked v3 profile results" \
  -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
git push origin HEAD
```

These are operator actions for an approved reproduction, not instructions to interfere with an active controller. Repeat with the next profile; for Sol use its root/deployment and `--cohort gpt-6.1-sol-v3`. Inspect only intended changes before publication. Do not use generic strict `--summarize` across different literal model fingerprints: the combined reporter accepts only the explicit sequential policy or exact audited legacy fingerprints, keeping scope rows separate.

### Remaining-Sol controller and completion signals

For an experiment with all 4o profiles and Sol N=1 already checked, [run-sol-remaining.sh](scripts/vm/run-sol-remaining.sh) invokes [resume-sol-v3.py](scripts/vm/resume-sol-v3.py). It runs Sol N=3 then full Small, checks each, regenerates the report, commits and pushes **before** proceeding.

Export endpoint, resource ID, guest path, data root and explicit `GPT4O_ENFORCED_ROOT`, `SOL_ROOT`, `LOG_ROOT`. The wrapper/helper set Sol deployment/artifact root. They default to the committed reference; optional `REFERENCE_COMPARISON` overrides it for historical replay. Do not reuse the completed experiment's existing remaining-profile directories/logs:

```bash
# Only on an inactive, approved checkout with new remaining-profile directories/logs:
outer_log="$LOG_ROOT/sol-remaining.log"
test ! -e "$outer_log"
mkdir -p "$LOG_ROOT"
setsid nohup bash scripts/vm/run-sol-remaining.sh > "$outer_log" 2>&1 < /dev/null &
printf '%s\n' "$!" > "$LOG_ROOT/sol-remaining.pid"
# In a monitoring terminal:
tail -f "$LOG_ROOT/sol-remaining.log"
```

Progress includes `START Sol n3`, checker output, then `PUBLISHED Sol n3 <commit> <UTC>`; the same follows for `full-small`. Child progress is retained in `sol-n3.log` and `sol-full-small.log`. **Done means both profiles have checked VALID five-pair comparisons and both PUBLISHED lines, including the final full-small push.** A completed log line or stopped PID alone is not enough.

Any nonzero runner/checker/report/commit/push stops the helper and prevents the next launch. Follow the [canonical validity rules](docs/benchmark-methodology.md#validity-rules) for stop/continue decisions and preserved model outcomes. Review stopped attempts only after the process ends; archive explicitly rather than overwrite/retry in place.

Only generated results Markdown is staged by this controller. Raw evidence is ignored by Git; publication of the report does **not** publish the raw artifact tree. Distribute a separately reviewed/sanitized evidence bundle for peer raw-event inspection.

## Methodology and interpretation

[Benchmark methodology](docs/benchmark-methodology.md) is canonical, especially its [validity rules](docs/benchmark-methodology.md#validity-rules). Other documents link there rather than defining competing classification rules.

- **Fairness:** same data/scope, seven underlying tools, shared prompt body, schema/validator, oracle/scorer, within-model version/settings, pricing and budgets. Mechanism addenda/model-facing tool surfaces intentionally differ; matching shared hashes alone does not establish scientific equivalence.
- **Trials:** one warm-up per mode, then paired measurements in seeded randomized order. Keep five-pair cohorts distinct from historical three-pair runs and isolated smoke pairs.
- **Cohorts/hashes:** prompt v3 canonical LF-normalized SHA-256 is `f0cf5bce1c07cceb82ef7e9b2552788dc6154f7eb5e7b4a61a0e8d4d426b667c`; historical v2 is `630a3a841752d3b4475fadde5050b2884c11b59b28f87e4c43a694fc57ebbc86`. Report schema remains v2. API/model/enforcement/pricing/scope cohorts are separate; historical data is not rewritten or silently rescored.
- **Aggregation:** retain every measured outcome, slow or failed; exclude warm-ups. Show individual trials plus median/min/max. Median total tokens is the median of each trial's total, not a sum of input/output medians. Unknown stays unavailable, never dropped or invented.
- **Accounting:** reasoning is included once in output/total; visible output is output minus reasoning when known. Current analysis time excludes recorded retry wait; process-inclusive time and the 120-second deadline include it and startup/transport. Model requests, HTTP retries, model-facing calls and sandbox callbacks are distinct.
- **Primary view:** within-model, same-scope deltas. Efficiency-win cards require every measured trial Exact/WithinTolerance. Descriptive all-outcome deltas always carry both modes' correctness context; cross-model comparisons are secondary.

## Evidence and sharing

| Location | Meaning |
|---|---|
| [Final results](docs/v3-responses-results.md) | Committed generated current-cohort tables; within-model first. |
| [Cohort record](docs/v3-enforced-cohorts.md) | Model/API/pricing policy, gates and dated execution record. |
| [Diagnostics](docs/v3-diagnostics.md) | Real-guest no-LLM control and historical failed-trial forensics. |
| [Sanitized sample](docs/sample-run/) | Committed historical pair, inputs, reports, Python and diagnosis. |
| `<ARTIFACTS_ROOT>/<scope>/launch.json` | Source commit and configuration recorded by the runner. |
| `<scope>/deployment-pricing-verification.json` | Pre-launch metadata/meter verification; review identifiers before sharing. |
| `<scope>/comparisons/` | Comparison JSON/Markdown/CSV/HTML and per-trial scores. |
| `<scope>/runs/<runId>/` | Result, metadata, metrics, events, timeline, requests/results, generated code and streams. |

`artifacts/` is ignored, not a clean-clone input. A local raw-evidence link may not be available to peers. Preserve original trials; derived reclassification must identify its source. Do not execute captured generated programs on the host.

Full capture is explicit and bounded; generated Python may be captured independently of payload mode. Redacted/truncated source is marked unavailable as an exact program. Redaction is defense in depth, not permission to share arbitrary logs.

Before sharing remove real subscription/tenant/principal IDs, resource-specific endpoints/names, public IPs, personal machine paths, keys/tokens and internal URLs. Public price meter IDs, source hashes, synthetic report IDs and public documentation links are not credentials. Keep deploy keys and environment files outside published evidence.

## Known limits

1. **Guest shared output: 16,376 available bytes.** Oversize results can abort guest serialization and return an explicit failure envelope with empty stdout; this is not silent truncation. Heap/stack or output-directory settings do not resize this buffer in the pinned SDK.
2. **Medium/Large infeasible under the current output design.** Canonical compact reports are 64,764 / 196,685 UTF-8 bytes, before transport overhead. The pinned provider exposes no output-buffer control. v4 supported-buffer/output-protocol work is pending; no current live scalability claim. Full Small canonical output is 16,547 bytes, while concise feasibility output is 15,274: serialization/descriptions matter.
3. **Telemetry is a different bound:** 65,536 characters per captured payload, with explicit flags; not the guest buffer and not necessarily the actual model-input size.
4. **No native mid-execution cancellation.** The pinned provider can wait forever despite a cancellation token. Benchmark parent processes kill/reap the worker tree at the configured deadline in both modes, including warm-ups/batches. The excluded `HyperlightKnownIssue` test is not a pass.
5. **API-host watchdog gap:** standalone Classic/CodeAct API hosting lacks the benchmark's worker isolation. Do not describe direct API native execution as deadline-contained.
6. **MCP is not used.** This measures direct AIFunction integration, not MCP transport overhead or behavior.
7. Small cohorts, provider nondeterminism, cache effects, retail estimates and preview SDK behavior limit generalization. Optional Classic batching changes the experiment and remains separately labelled.

Evidence: [SDK findings](docs/sdk-findings.md), [capacity and stop decision](docs/infrastructure-gate-20261117.md), [security model](docs/security-model.md).

## Troubleshooting

| Symptom | Check/action |
|---|---|
| Azure 404 | Use the supported account base endpoint, not `/openai/v1` or a project URL; verify exact deployment/API mode. Let the adapter append its route. |
| Azure 401/403 | Check actual SDK identity/key, resource-scoped OpenAI User role, metadata-read permissions, propagation and network policy. An inference key alone does not supply management metadata access. |
| Azure configured but fake selected | Safe default: explicitly pass `--model live`; APIs use that flag or `MODEL_MODE=live`. No live-to-fake fallback. |
| Changed variables ignored in VS Code | An existing editor/Remote-SSH server, task or extension host may retain stale environment. Export in the actual launcher; reconnect/restart the relevant host. Inspect non-secret policy values/presence, never tokens or the whole environment. `.env.example` is not auto-loaded. |
| PowerShell surprises | Use `$env:NAME=...`, not Bash `export`; do not paste heredocs or `setsid nohup` into PowerShell. Quote paths/filters; check `$LASTEXITCODE` after native commands. Use `Get-Content -LiteralPath` for bracketed TRX names, resetting XML variables before recounting. Never bypass execution policy. |
| SSH timeout | Verify running/not auto-deallocated VM, current public IP, corporate VPN route and BOTH NSGs. Policy may delete CorpNetPublic allows; recreate only approved rules through an authorized operator. A timeout differs from `Permission denied (publickey)`. |
| SSH key failure | Check SSH user, intended client key and trusted host fingerprint; repository deploy key is separate from VM login authentication. Never copy private keys into the repository. |
| `/dev/kvm` denied | Confirm x64/nested virtualization and `kvm` membership; disconnect all old sessions and reconnect. Do not globally loosen device permissions. |
| Guest not found / skipped tests | Use the absolute bootstrap guest path and validate it in the test shell. Skips are not runtime passes. Exclude only the documented native-cancellation known issue. |
| Sandbox overflow | Inspect required/available bytes and matching result/continuation evidence under the canonical validity rules. Do not invent a buffer setting, drop fields or silently truncate reports. |
| Runner refuses a profile | Check clean committed source, required environment, default reference (or explicit comparison AND sibling metadata), and existing profile/log paths. Never erase/reuse an active/failed attempt. |
| Missing data / API 503 | Generate data first; use absolute roots. API-relative roots resolve under the project content root. |
| VALID but incorrect / INVALID | Read the [canonical validity rules](docs/benchmark-methodology.md#validity-rules) and inspect oracle scores versus infrastructure evidence. Accounting is not analytical success. |
| Controller stops after a profile | Check checker, report generation, commit identity, deploy-key write permission and push result. No next profile should launch after publication failure. |
| Old dashboard/counts disagree | Check comparison/source commit, cohort, scope and execution kind. Historical docs/reports are not automatically regenerated. |
| Locked restore fails | Verify selected SDK and approved package feed; do not regenerate locks merely to hide failure. |

License: [MIT](LICENSE). Reporting boundaries: [SECURITY.md](SECURITY.md).
