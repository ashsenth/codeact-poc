# Runbook validation

Evaluated the documentation as a new developer on a clean-ish machine (Windows, .NET SDK 9.0.317).
Commands below were **actually executed** during this audit or earlier phases; results are in
`command-results.md`.

## Executed and verified

| Documented step | Command | Result |
| --- | --- | --- |
| Restore (locked) | `dotnet restore --locked-mode` | ✅ exit 0 |
| Build | `dotnet build -c Release` | ✅ 0 warnings / 0 errors |
| Format | `dotnet format --verify-no-changes` | ✅ clean |
| Test (offline) | `dotnet test -c Release --no-build` | ✅ 146 passed / 5 gated-skipped |
| Generate data | `dotnet run --project src/SterlingVale.DataGenerator -- data` | ✅ (verified in Phase 1 & benchmark smoke) |
| Benchmark validate | `benchmark validate-data` | ✅ all 3 profiles VALID |
| Benchmark run | `benchmark run --profile small --trials 3` | ✅ fairness MATCHED; `{id}.json`+`.md` written |

## Not executed here (recorded honestly)

| Step | Why not run | Note |
| --- | --- | --- |
| `dotnet run` the two APIs | audit did not start long-running servers | Endpoints are exercised by `WebApplicationFactory` integration tests |
| Live-model tests | no Azure credentials; `ENABLE_LIVE_TESTS` unset | Tests skip with an explicit reason |
| Hyperlight runtime | no guest module / virtualization on this host | Tests skip with an explicit reason |
| gitleaks + coverage upload | GitHub-Action-only steps | Defined in `ci.yml`, not runnable locally here |

## Documentation gaps found

1. **Ports (F-08).** README's `curl localhost:5000` assumes a fixed port; `dotnet run` without a
   `launchSettings.json` may bind elsewhere.
   *Proposed text:* "Set the bind address explicitly: `ASPNETCORE_URLS=http://localhost:5000 dotnet
   run --project src/SterlingVale.Classic.Api`, then `curl http://localhost:5000/health/live`."

2. **Line endings / CI (F-02).** No mention that the repo enforces CRLF and lacks `.gitattributes`;
   Linux contributors may trip the format check.
   *Proposed text (CONTRIBUTING/README troubleshooting):* "This repo uses CRLF (`.editorconfig`).
   Add `.gitattributes` (`* text=auto`, `*.cs text eol=crlf`) and run `git add --renormalize .`
   before committing on Linux/macOS."

3. **Hyperlight guest acquisition.** README states the requirement and the env var but does not link
   to where to obtain/build the Python guest module.
   *Proposed text:* add a short "Obtaining the Hyperlight guest" subsection pointing to the official
   `microsoft/agent-framework` Hyperlight sample and the guest build instructions, then
   `export HYPERLIGHT_PYTHON_GUEST_PATH=/abs/path/to/guest`.

4. **Windows vs Linux virtualization.** README says "hardware virtualization" generically; it does
   not spell out WHP (Windows) vs KVM (Linux) enablement.
   *Proposed text:* "Windows: enable the Windows Hypervisor Platform. Linux: ensure `/dev/kvm` is
   present and the user can access it."

5. **Clean-up.** README lacks an explicit clean-up step.
   *Proposed text:* "Clean up: `dotnet clean`; remove generated data/artifacts with
   `Remove-Item -Recurse data, artifacts` (both are git-ignored)."

## Overall runbook verdict

**Accurate and sufficient for offline build/test/benchmark.** A new developer can restore, build,
test, generate data, and run the benchmark by following the README. Live/Hyperlight setup is stated
but would benefit from the guest-acquisition and virtualization-enablement additions above.
Disclaimers (synthetic data, not investment advice) are present and clear.
