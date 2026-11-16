#!/usr/bin/env python3
"""Run one approved v3 profile; invoke under nohup and check it before proceeding."""
import argparse
import json
import os
import subprocess
from pathlib import Path


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("profile", choices=("n1", "n3", "full-small"))
    parser.add_argument("--reference-comparison", type=Path, required=True)
    args = parser.parse_args()
    repo = Path(__file__).resolve().parents[2]
    reference = args.reference_comparison.resolve()
    report = json.loads(reference.read_text())
    if report["promptRevision"] != "exposure-analysis/v3" or not report["outcome"]["isValid"]:
        parser.error("The reference must be a valid v3 comparison.")
    trial = report["rawTrials"][0]
    metadata = json.loads((reference.parent.parent / "runs" / trial["runId"] / "metadata.json").read_text())
    model, pricing = metadata["modelConfiguration"], metadata["pricingConfiguration"]
    if (model["maxTurns"], model["timeoutSeconds"], model["strict"], model["parallel_tool_calls"]) != (20, 120, False, True):
        parser.error("The reference does not use the approved limits/output settings.")
    for key in ("AZURE_OPENAI_ENDPOINT", "AZURE_OPENAI_DEPLOYMENT_NAME", "HYPERLIGHT_PYTHON_GUEST_PATH",
                "DATASET_ROOT", "ARTIFACTS_ROOT"):
        if not os.environ.get(key):
            parser.error(f"{key} must be set.")
    if os.environ["AZURE_OPENAI_DEPLOYMENT_NAME"] != model["deploymentName"]:
        parser.error("Deployment differs from the reference.")
    if not Path(os.environ["HYPERLIGHT_PYTHON_GUEST_PATH"]).is_file():
        parser.error("Guest file does not exist.")
    if subprocess.check_output(["git", "status", "--porcelain"], cwd=repo, text=True).strip():
        parser.error("Commit all changes before starting a live profile.")
    root = Path(os.environ["ARTIFACTS_ROOT"]).resolve() / args.profile
    if root.exists():
        parser.error(f"Refusing to overwrite {root}; archive a stopped attempt explicitly.")
    env = dict(os.environ)
    env.update({
        "DATASET_ROOT": str(Path(env["DATASET_ROOT"]).resolve()),
        "HYPERLIGHT_PYTHON_GUEST_PATH": str(Path(env["HYPERLIGHT_PYTHON_GUEST_PATH"]).resolve()),
        "ARTIFACTS_ROOT": str(root), "BENCHMARK_REQUIRE_LIVE": "true",
        "BENCHMARK_MAX_TURNS": "20", "BENCHMARK_TIMEOUT_SECONDS": "120", "CLASSIC_BATCH_SIZE": "0",
        "PRICING_INPUT_PER_MILLION": str(pricing["inputPerMillion"]),
        "PRICING_OUTPUT_PER_MILLION": str(pricing["outputPerMillion"]),
        "PRICING_SOURCE_URL": pricing["sourceUrl"], "PRICING_VERIFIED_ON": pricing["verifiedOn"],
        "PRICING_BASIS": pricing["basis"], "OTEL_EXPORTER_OTLP_ENDPOINT": "",
    })
    command = ["dotnet", str(repo / "src/SterlingVale.Benchmark/bin/Release/net9.0/SterlingVale.Benchmark.dll"),
               "run", "--model", "live", "--profile", "small", "--trials", "5",
               "--log-level", "normal", "--payload-capture", "full"]
    if args.profile != "full-small":
        command += ["--households", {"n1": "1", "n3": "3"}[args.profile]]
    subprocess.run([command[0], command[1], "validate-data"], env=env, cwd=repo, check=True)
    root.mkdir(parents=True)
    commit = subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=repo, text=True).strip()
    (root / "launch.json").write_text(json.dumps({
        "sourceCommit": commit, "profile": args.profile, "trials": 5,
        "referenceComparison": report["comparisonId"], "promptHash": report["promptHash"],
        "maxTurns": 20, "timeoutSeconds": 120,
    }, indent=2) + "\n")
    print(f"v3 {args.profile}: five pairs; source {commit}", flush=True)
    os.chdir(repo)
    os.execvpe(command[0], command, env)


if __name__ == "__main__":
    main()
