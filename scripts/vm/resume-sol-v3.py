#!/usr/bin/env python3
"""Run approved Sol profiles sequentially; validate, report, commit and push after each."""
import argparse
import os
import subprocess
from datetime import datetime, timezone
from pathlib import Path


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--reference-comparison", required=True, type=Path)
    parser.add_argument("--gpt4o-root", required=True, type=Path)
    parser.add_argument("--sol-root", required=True, type=Path)
    parser.add_argument("--log-root", required=True, type=Path)
    parser.add_argument("--profiles", nargs="+", choices=("n3", "full-small"), default=["n3", "full-small"])
    args = parser.parse_args()
    repo = Path(__file__).resolve().parents[2]
    reference, gpt4o, sol, logs = (path.resolve() for path in
                                 (args.reference_comparison, args.gpt4o_root, args.sol_root, args.log_root))
    logs.mkdir(parents=True, exist_ok=True)
    env = dict(os.environ, ARTIFACTS_ROOT=str(sol), AZURE_OPENAI_DEPLOYMENT_NAME="gpt-6.1-sol")
    scripts = repo / "scripts" / "vm"
    output = repo / "docs" / "v3-responses-results.md"
    for profile in args.profiles:
        print(f"START Sol {profile} {datetime.now(timezone.utc).isoformat()}", flush=True)
        with (logs / f"sol-{profile}.log").open("x") as log:
            for name in ("run-v3-profile.py", "check-v3-profile.py"):
                subprocess.run(["python3", str(scripts / name), profile, "--cohort", "gpt-6.1-sol-v3",
                                "--reference-comparison", str(reference)],
                               cwd=repo, env=env, stdout=log, stderr=subprocess.STDOUT, check=True)
        subprocess.run(["python3", str(scripts / "report-v3-cohorts.py"), "--gpt4o-root", str(gpt4o),
                        "--sol-root", str(sol), "--output", str(output)], cwd=repo, check=True)
        subprocess.run(["git", "add", str(output)], cwd=repo, check=True)
        subprocess.run(["git", "commit", "-m", f"Record resumed Sol v3 {profile} five-pair results",
                        "-m", "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"],
                       cwd=repo, check=True)
        subprocess.run(["git", "push", "origin", "HEAD"], cwd=repo, check=True)
        commit = subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=repo, text=True).strip()
        print(f"PUBLISHED Sol {profile} {commit} {datetime.now(timezone.utc).isoformat()}", flush=True)


if __name__ == "__main__":
    main()
