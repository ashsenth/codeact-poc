# Getting started

Start here for prerequisites and a no-Azure, no-virtualization harness check. For live inference continue with [Azure setup](azure-setup.md), [the VM guide](vm-setup.md) and [running experiments](running-experiments.md).

## Quickstart without Azure

Requires Git, Bash and a .NET 9 SDK selected by [global.json](../global.json): 9.0.317 with `latestFeature` roll-forward. Initial locked restore needs package-feed access. No Azure, Python, guest or hypervisor is needed for this demo. Windows users can use an approved Bash/WSL environment with its own .NET installation; do not paste Bash syntax into PowerShell.

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
Historical Windows ARM64 and Ubuntu x64 local-source clean-clone checks are recorded in [the x64 workflow](review-current/x64-validation.md). A remote-origin, completely fresh OS reproduction is not certified.
