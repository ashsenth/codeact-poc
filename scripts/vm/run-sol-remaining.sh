#!/usr/bin/env bash
set -euo pipefail

repo="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$repo"

: "${AZURE_OPENAI_ENDPOINT:?Export the Azure OpenAI endpoint}"
: "${AZURE_OPENAI_RESOURCE_ID:?Export the Azure OpenAI account resource ID}"
: "${HYPERLIGHT_PYTHON_GUEST_PATH:?Export the pinned Hyperlight guest path}"
: "${DATASET_ROOT:?Export the existing generated dataset root}"

reference="${REFERENCE_COMPARISON:-$repo/artifacts/v3-sweep-20261116/n1/comparisons/small-20261116204456-a702d6024c2a4eb1afcc742408d492f0-reclassified.json}"
gpt4o="${GPT4O_ENFORCED_ROOT:-$repo/artifacts/v3-enforced-20261117/gpt-4o}"
sol="${SOL_ROOT:-$repo/artifacts/v3-enforced-20261117/gpt-6.1-sol}"
logs="${LOG_ROOT:-$repo/artifacts/v3-continuation-gate}"

if [[ ! -f "$reference" || ! -d "$gpt4o" || ! -d "$sol" ||
      ! -f "$HYPERLIGHT_PYTHON_GUEST_PATH" || ! -d "$DATASET_ROOT" ]]; then
    printf '%s\n' "Required reference, cohort, guest or dataset path is missing; no live run started." >&2
    exit 1
fi
if [[ -e "$sol/n3" || -e "$sol/full-small" ]]; then
    printf '%s\n' "Refusing to overwrite an existing remaining profile; inspect/archive it explicitly." >&2
    exit 1
fi

exec python3 "$repo/scripts/vm/resume-sol-v3.py" \
    --reference-comparison "$reference" \
    --gpt4o-root "$gpt4o" --sol-root "$sol" --log-root "$logs"
