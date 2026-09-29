#!/usr/bin/env bash
# Generate data (once) and run the benchmark for the given profiles, DETACHED.
# Usage: bash run-benchmark.sh "small"   |   bash run-benchmark.sh "medium large"
# Logs to /tmp/bench.log. Auth: DefaultAzureCredential -> VM managed identity (granted OpenAI role).
PROFILES="${1:-small medium large}"
TRIALS="${2:-3}"
nohup bash -s "$PROFILES" "$TRIALS" <<'EOF' > /tmp/bench.log 2>&1 &
set -o pipefail
PROFILES="$1"; TRIALS="$2"
export HOME=/root
export PATH="/usr/local/bin:/root/.dotnet:$PATH"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
export AZURE_OPENAI_ENDPOINT="https://codeact-openai.services.ai.azure.com/"
export AZURE_OPENAI_DEPLOYMENT_NAME="gpt-4o"
export HYPERLIGHT_PYTHON_GUEST_PATH="$(cat /root/guest_path.txt)"
# CodeAct chunking makes many execute_code calls (one per household batch); raise the turn cap + timeout.
export BENCHMARK_MAX_TURNS="${BENCHMARK_MAX_TURNS:-40}"
export BENCHMARK_TIMEOUT_SECONDS="${BENCHMARK_TIMEOUT_SECONDS:-300}"
echo "=== $(date -u) bench start ==="
echo "PROFILES=$PROFILES TRIALS=$TRIALS"
echo "GUEST=$HYPERLIGHT_PYTHON_GUEST_PATH"
[ -f "$HYPERLIGHT_PYTHON_GUEST_PATH" ] && echo "GUEST_EXISTS=yes" || echo "GUEST_EXISTS=NO"
cd /root/codeact-poc || { echo "NO_REPO"; exit 1; }

echo "=== restore ==="
dotnet restore SterlingVale.sln 2>&1 | tail -6
RC=${PIPESTATUS[0]}
if [ "$RC" -ne 0 ]; then
  echo "locked restore failed (rc=$RC); retrying without lock enforcement"
  find . -name packages.lock.json -delete
  dotnet restore SterlingVale.sln 2>&1 | tail -6
fi

echo "=== data-gen ==="
dotnet run --project src/SterlingVale.DataGenerator -c Release -- data 2>&1 | tail -8

for P in $PROFILES; do
  echo "=== $(date -u) profile=$P trials=$TRIALS ==="
  dotnet run --project src/SterlingVale.Benchmark -c Release -- run --profile "$P" --trials "$TRIALS"
  echo "=== profile=$P exit=$? ==="
done

echo "=== comparison artifacts ==="
ls -1 artifacts/comparisons/*.json 2>/dev/null | tail -20
echo "=== $(date -u) bench done ==="
EOF
echo "bench launched in background (pid $!) profiles=[$PROFILES]"
