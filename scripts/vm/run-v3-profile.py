#!/usr/bin/env python3
"""Run one approved v3 profile; invoke under nohup and check it before proceeding."""
import argparse
import json
import os
import subprocess
from pathlib import Path
from cohort_config import COHORTS, cohort_timeout, configure_environment, load_reference, validate_cohort_root, verify


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("profile", choices=("n1", "n3", "full-small"))
    parser.add_argument("--reference-comparison", type=Path, help="Optional historical comparison and sibling run metadata.")
    parser.add_argument("--cohort", choices=tuple(COHORTS))
    parser.add_argument("--smoke", action="store_true", help="One N=1 pair, isolated from measured profiles.")
    args = parser.parse_args()
    repo = Path(__file__).resolve().parents[2]
    report = load_reference(args.reference_comparison)
    if not args.reference_comparison and not args.cohort:
        args.cohort = "gpt-4o-v3-enforced"
    model, pricing = report["modelConfiguration"], report["pricingConfiguration"]
    timeout = cohort_timeout(args.cohort, report)
    if (model["maxTurns"], model["timeoutSeconds"], model["strict"], model["parallel_tool_calls"]) != (20, 120, False, True):
        parser.error("The reference does not use the approved limits/output settings.")
    for key in ("AZURE_OPENAI_ENDPOINT", "AZURE_OPENAI_DEPLOYMENT_NAME", "HYPERLIGHT_PYTHON_GUEST_PATH",
                "DATASET_ROOT", "ARTIFACTS_ROOT"):
        if not os.environ.get(key):
            parser.error(f"{key} must be set.")
    if not args.cohort and os.environ["AZURE_OPENAI_DEPLOYMENT_NAME"] != model["deploymentName"]:
        parser.error("Deployment differs from the reference.")
    if not Path(os.environ["HYPERLIGHT_PYTHON_GUEST_PATH"]).is_file():
        parser.error("Guest file does not exist.")
    if subprocess.check_output(["git", "status", "--porcelain"], cwd=repo, text=True).strip():
        parser.error("Commit all changes before starting a live profile.")
    if args.smoke and (not args.cohort or args.profile != "n1"):
        parser.error("--smoke requires an approved Responses cohort and profile n1.")
    count = 1 if args.smoke else 5
    cohort_root = Path(os.environ["ARTIFACTS_ROOT"]).resolve()
    validate_cohort_root(cohort_root, args.cohort)
    root = cohort_root / ("smoke-n1" if args.smoke else args.profile)
    if root.exists():
        parser.error(f"Refusing to overwrite {root}; archive a stopped attempt explicitly.")
    env = dict(os.environ)
    env.update({
        "DATASET_ROOT": str(Path(env["DATASET_ROOT"]).resolve()),
        "HYPERLIGHT_PYTHON_GUEST_PATH": str(Path(env["HYPERLIGHT_PYTHON_GUEST_PATH"]).resolve()),
        "ARTIFACTS_ROOT": str(root), "BENCHMARK_REQUIRE_LIVE": "true",
        "BENCHMARK_MAX_TURNS": "20", "BENCHMARK_TIMEOUT_SECONDS": str(timeout), "CLASSIC_BATCH_SIZE": "0",
        "PRICING_INPUT_PER_MILLION": str(pricing["inputPerMillion"]),
        "PRICING_OUTPUT_PER_MILLION": str(pricing["outputPerMillion"]),
        "PRICING_SOURCE_URL": pricing["sourceUrl"], "PRICING_VERIFIED_ON": pricing["verifiedOn"],
        "PRICING_BASIS": pricing["basis"], "OTEL_EXPORTER_OTLP_ENDPOINT": "",
    })
    verification = None
    if args.cohort:
        verification = verify(args.cohort)
        configure_environment(env, args.cohort)
    command = ["dotnet", str(repo / "src/SterlingVale.Benchmark/bin/Release/net9.0/SterlingVale.Benchmark.dll"),
               "run", "--model", "live", "--profile", "small", "--trials", str(count),
               "--log-level", "normal", "--payload-capture", "full"]
    if args.profile != "full-small":
        command += ["--households", {"n1": "1", "n3": "3"}[args.profile]]
    subprocess.run([command[0], command[1], "validate-data"], env=env, cwd=repo, check=True)
    root.mkdir(parents=True)
    commit = subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=repo, text=True).strip()
    (root / "launch.json").write_text(json.dumps({
        "sourceCommit": commit, "profile": args.profile, "trials": count, "cohort": args.cohort, "smoke": args.smoke,
        "referenceComparison": report["referenceId"], "promptHash": report["promptHash"],
        "maxTurns": 20, "timeoutSeconds": timeout,
    }, indent=2) + "\n")
    if verification:
        (root / "deployment-pricing-verification.json").write_text(json.dumps(verification, indent=2) + "\n")
    print(f"v3 {args.cohort or 'historical'} {args.profile}: {count} pairs; source {commit}", flush=True)
    os.chdir(repo)
    os.execvpe(command[0], command, env)


if __name__ == "__main__":
    main()
