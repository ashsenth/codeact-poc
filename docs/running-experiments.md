# Running experiments

Complete [getting started](getting-started.md), [Azure setup](azure-setup.md) and [VM setup](vm-setup.md) first. For classification and stop/continue decisions use only the [canonical methodology and validity rules](benchmark-methodology.md#validity-rules).

## Running current cohorts

Use committed [runner](../scripts/vm/run-v3-profile.py), [checker](../scripts/vm/check-v3-profile.py), and [combined reporter](../scripts/vm/report-v3-cohorts.py). Python 3 standard library only. Legacy setup/run helpers now delegate to these approved paths; they no longer assume a root-user checkout or silently regenerate lock files.

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

Fresh clones default to [configs/v3-reference.json](../configs/v3-reference.json): shared prompt/schema/tools/Small-dataset fingerprints, output/request/deadline limits, historical pricing basis and explicit current execution/concurrency policy. It is derived from the existing valid historical reference; the cohort config replaces historical uncached pricing with verified cache/context rates. A regression test regenerates the data/tools and compares current prompt/schema/policy fingerprints to this file.

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

Both isolated smoke gates must pass. Interpret VALID using the [canonical validity rules](benchmark-methodology.md#validity-rules), not as oracle success.
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

For an experiment with all 4o profiles and Sol N=1 already checked, [run-sol-remaining.sh](../scripts/vm/run-sol-remaining.sh) invokes [resume-sol-v3.py](../scripts/vm/resume-sol-v3.py). It runs Sol N=3 then full Small, checks each, regenerates the report, commits and pushes **before** proceeding.

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

Any nonzero runner/checker/report/commit/push stops the helper and prevents the next launch. Follow the [canonical validity rules](benchmark-methodology.md#validity-rules) for stop/continue decisions and preserved model outcomes. Review stopped attempts only after the process ends; archive explicitly rather than overwrite/retry in place.

Only generated results Markdown is staged by this controller. Raw evidence is ignored by Git; publication of the report does **not** publish the raw artifact tree. Distribute a separately reviewed/sanitized evidence bundle for peer raw-event inspection.

## Evidence and sharing

| Location | Meaning |
|---|---|
| [Final results](v3-responses-results.md) | Committed generated current-cohort tables; within-model first. |
| [Cohort record](v3-enforced-cohorts.md) | Model/API/pricing policy, gates and dated execution record. |
| [Diagnostics](v3-diagnostics.md) | Real-guest no-LLM control and historical failed-trial forensics. |
| [Sanitized sample](sample-run/) | Committed historical pair, inputs, reports, Python and diagnosis. |
| `<ARTIFACTS_ROOT>/<scope>/launch.json` | Source commit and configuration recorded by the runner. |
| `<scope>/deployment-pricing-verification.json` | Pre-launch metadata/meter verification; review identifiers before sharing. |
| `<scope>/comparisons/` | Comparison JSON/Markdown/CSV/HTML and per-trial scores. |
| `<scope>/runs/<runId>/` | Result, metadata, metrics, events, timeline, requests/results, generated code and streams. |

`artifacts/` is ignored, not a clean-clone input. A local raw-evidence link may not be available to peers. Preserve original trials; derived reclassification must identify its source. Do not execute captured generated programs on the host.

Full capture is explicit and bounded; generated Python may be captured independently of payload mode. Redacted/truncated source is marked unavailable as an exact program. Redaction is defense in depth, not permission to share arbitrary logs.

Before sharing remove real subscription/tenant/principal IDs, resource-specific endpoints/names, public IPs, personal machine paths, keys/tokens and internal URLs. Public price meter IDs, source hashes, synthetic report IDs and public documentation links are not credentials. Keep deploy keys and environment files outside published evidence.
