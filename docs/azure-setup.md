# Azure setup

Use this guide after the [offline quickstart](getting-started.md). Provision the [VM](vm-setup.md) before assigning its identity; then follow [running experiments](running-experiments.md).

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

[cohort_config.py](../scripts/vm/cohort_config.py) contains exact official Azure Retail Prices API URLs and meter names. The runner follows pagination and fails closed if a selected USD Consumption meter is missing, ambiguous or changed. Do not filter only on the old service name `Cognitive Services`; these meters can be under `Foundry Models`.

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

Costs are retail estimates, not invoices; negotiated discounts, missing usage and VM/storage/network charges are excluded. [Full pricing provenance](v3-enforced-cohorts.md#verified-retail-pricing).
