#!/usr/bin/env bash
set -euo pipefail

: "${LOG_FILE:?Set LOG_FILE to the remaining-Sol controller log}"
for ((i = 0; i < 150; i++)); do
    if [[ -f "$LOG_FILE" ]] &&
       grep -q '^PUBLISHED Sol n3 ' "$LOG_FILE" &&
       grep -q '^PUBLISHED Sol full-small ' "$LOG_FILE"; then
        tail -60 "$LOG_FILE"
        exit 0
    fi
    sleep 20
done
echo 'Timed out waiting for both publication markers; inspect controller status and checker results. No success inferred.' >&2
exit 1
