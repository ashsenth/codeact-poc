#!/usr/bin/env python3
"""Run approved Sol profiles sequentially; validate, report, commit and push after each."""
import argparse
import os
import subprocess
from datetime import datetime, timezone
from pathlib import Path
from cohort_config import validate_cohort_root


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--reference-comparison", type=Path, help="Optional historical comparison and sibling run metadata.")
    parser.add_argument("--cohort", choices=("gpt-6.1-sol-v3", "gpt-6.1-sol-v3-t600"), default="gpt-6.1-sol-v3")
    parser.add_argument("--gpt4o-root", type=Path)
    parser.add_argument("--sol-baseline-root", type=Path, help="Separate 120-second baseline for sensitivity reporting.")
    parser.add_argument("--sol-root", required=True, type=Path)
    parser.add_argument("--log-root", required=True, type=Path)
    parser.add_argument("--profiles", nargs="+", choices=("n3", "full-small"), default=["n3", "full-small"])
    args = parser.parse_args()
    repo = Path(__file__).resolve().parents[2]
    sol, logs = (path.resolve() for path in (args.sol_root, args.log_root))
    sensitivity = args.cohort == "gpt-6.1-sol-v3-t600"
    if sensitivity:
        if not args.sol_baseline_root:
            parser.error("--sol-baseline-root is required for the t600 cohort.")
        baseline = args.sol_baseline_root.resolve()
        if baseline.is_relative_to(sol) or sol.is_relative_to(baseline):
            parser.error("The 120-second and 600-second roots must be separate, non-nested directories.")
        validate_cohort_root(baseline, "gpt-6.1-sol-v3")
    elif not args.gpt4o_root:
        parser.error("--gpt4o-root is required for the 120-second cross-model report.")
    validate_cohort_root(sol, args.cohort)
    reference_args = (["--reference-comparison", str(args.reference_comparison.resolve())]
                      if args.reference_comparison else [])
    logs.mkdir(parents=True, exist_ok=True)
    env = dict(os.environ, ARTIFACTS_ROOT=str(sol), AZURE_OPENAI_DEPLOYMENT_NAME="gpt-6.1-sol")
    scripts = repo / "scripts" / "vm"
    output = repo / "docs" / ("v3-time-budget-sensitivity.md" if sensitivity else "v3-responses-results.md")
    report_args = (["--sol-root", str(baseline), "--sol-t600-root", str(sol)] if sensitivity else
                   ["--gpt4o-root", str(args.gpt4o_root.resolve()), "--sol-root", str(sol)])
    prefix = "sol-t600" if sensitivity else "sol"
    for profile in args.profiles:
        print(f"START {args.cohort} {profile} {datetime.now(timezone.utc).isoformat()}", flush=True)
        with (logs / f"{prefix}-{profile}.log").open("x") as log:
            for name in ("run-v3-profile.py", "check-v3-profile.py"):
                command = ["python3", str(scripts / name), profile, "--cohort", args.cohort,
                           *reference_args]
                with subprocess.Popen(command, cwd=repo, env=env, stdout=subprocess.PIPE,
                                      stderr=subprocess.STDOUT, text=True, bufsize=1) as process:
                    for line in process.stdout:
                        print(line, end="", flush=True)
                        log.write(line)
                        log.flush()
                    result = process.wait()
                    if result:
                        raise subprocess.CalledProcessError(result, command)
        subprocess.run(["python3", str(scripts / "report-v3-cohorts.py"), *report_args,
                        "--output", str(output)], cwd=repo, check=True)
        subprocess.run(["git", "add", str(output)], cwd=repo, check=True)
        subprocess.run(["git", "commit", "-m", f"Record {args.cohort} {profile} five-pair results",
                        "-m", "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"],
                       cwd=repo, check=True)
        subprocess.run(["git", "push", "origin", "HEAD"], cwd=repo, check=True)
        commit = subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=repo, text=True).strip()
        print(f"PUBLISHED {args.cohort} {profile} {commit} {datetime.now(timezone.utc).isoformat()}", flush=True)


if __name__ == "__main__":
    main()
