#!/usr/bin/env bash
set -euo pipefail

repo="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/../.." && pwd)"
echo 'Compatibility entry point: use run-v3-profile.py and the README cohort runbook; no implicit live scaling or lock-file changes.' >&2
exec python3 "$repo/scripts/vm/run-v3-profile.py" "$@"
