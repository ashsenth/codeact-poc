#!/usr/bin/env bash
set -euo pipefail

repo="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$repo"

: "${AZURE_OPENAI_ENDPOINT:?Export the Azure OpenAI endpoint}"
: "${AZURE_OPENAI_RESOURCE_ID:?Export the Azure OpenAI account resource ID}"
: "${HYPERLIGHT_PYTHON_GUEST_PATH:?Export the pinned Hyperlight guest path}"
: "${DATASET_ROOT:?Export the existing generated dataset root}"

reference_args=()
if [[ -n "${REFERENCE_COMPARISON:-}" ]]; then
    [[ -f "$REFERENCE_COMPARISON" ]] || { echo "Reference comparison missing." >&2; exit 1; }
    reference_args=(--reference-comparison "$REFERENCE_COMPARISON")
fi
baseline="${SOL_BASELINE_ROOT:-$repo/artifacts/v3-enforced-20261117/gpt-6.1-sol}"
sol="${SOL_T600_ROOT:-$repo/artifacts/gpt-6.1-sol-v3-t600}"
logs="${T600_LOG_ROOT:-$repo/artifacts/v3-t600-gate}"

if [[ ! -d "$baseline" || ! -f "$HYPERLIGHT_PYTHON_GUEST_PATH" || ! -d "$DATASET_ROOT" ]]; then
    printf '%s\n' "Required baseline, guest or dataset path is missing; no live run started." >&2
    exit 1
fi
if [[ -e "$sol/n3" || -e "$sol/full-small" ||
      -e "$logs/sol-t600-n3.log" || -e "$logs/sol-t600-full-small.log" ]]; then
    printf '%s\n' "Refusing to overwrite an existing sensitivity profile/log; inspect/archive it explicitly." >&2
    exit 1
fi

exec python3 "$repo/scripts/vm/resume-sol-v3.py" \
    "${reference_args[@]}" --cohort gpt-6.1-sol-v3-t600 \
    --sol-baseline-root "$baseline" --sol-root "$sol" --log-root "$logs"
