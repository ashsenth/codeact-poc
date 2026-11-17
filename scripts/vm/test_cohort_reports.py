#!/usr/bin/env python3
"""Offline behavioral checks for the committed cohort report."""
import importlib.util
import json
import subprocess
import tempfile
import unittest
from decimal import Decimal
from pathlib import Path
from unittest import mock
from cohort_config import COHORTS, DEFAULT_REFERENCE, configure_environment, load_reference, validate_cohort_root, validate_function_policy

spec = importlib.util.spec_from_file_location("cohort_report", Path(__file__).with_name("report-v3-cohorts.py"))
report = importlib.util.module_from_spec(spec)
spec.loader.exec_module(report)
resume_spec = importlib.util.spec_from_file_location("resume_sol", Path(__file__).with_name("resume-sol-v3.py"))
resume = importlib.util.module_from_spec(resume_spec)
resume_spec.loader.exec_module(resume)
runner_spec = importlib.util.spec_from_file_location("profile_runner", Path(__file__).with_name("run-v3-profile.py"))
runner = importlib.util.module_from_spec(runner_spec)
runner_spec.loader.exec_module(runner)
checker_spec = importlib.util.spec_from_file_location("profile_checker", Path(__file__).with_name("check-v3-profile.py"))
checker = importlib.util.module_from_spec(checker_spec)
checker_spec.loader.exec_module(checker)


class ReferenceTests(unittest.TestCase):
    def test_profile_launch_defaults_to_committed_reference_without_historical_artifacts(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            guest = root / "guest.aot"
            guest.touch()
            env = dict(AZURE_OPENAI_ENDPOINT="https://example.invalid/",
                       AZURE_OPENAI_DEPLOYMENT_NAME="gpt-6.1-sol",
                       HYPERLIGHT_PYTHON_GUEST_PATH=str(guest), DATASET_ROOT=str(root / "data"),
                       ARTIFACTS_ROOT=str(root / "cohort"))
            argv = ["run-v3-profile.py", "n1", "--cohort", "gpt-6.1-sol-v3"]
            with mock.patch("sys.argv", argv), mock.patch.dict(runner.os.environ, env, clear=True), \
                    mock.patch.object(runner.subprocess, "check_output", side_effect=["", "a" * 40]), \
                    mock.patch.object(runner.subprocess, "run") as validate, \
                    mock.patch.object(runner, "verify", return_value={"verified": True}), \
                    mock.patch.object(runner.os, "chdir"), mock.patch.object(runner.os, "execvpe") as execute, \
                    mock.patch("builtins.print"):
                runner.main()
            validate.assert_called_once()
            launched = execute.call_args.args[2]
            self.assertEqual("responses", launched["MODEL_API_MODE"])
            self.assertEqual("20", launched["BENCHMARK_MAX_TURNS"])
            self.assertEqual("0", launched["CLASSIC_BATCH_SIZE"])
            self.assertEqual("medium", launched["MODEL_DEFAULT_REASONING_EFFORT"])
            launch = json.loads((root / "cohort" / "n1" / "launch.json").read_text())
            self.assertEqual(load_reference()["referenceId"], launch["referenceComparison"])

    def test_default_reference_is_portable_without_any_run_metadata(self):
        value = load_reference()
        self.assertEqual("exposure-analysis/v3", value["promptRevision"])
        self.assertEqual(value["promptHash"], value["fingerprints"]["prompt"])
        self.assertTrue(DEFAULT_REFERENCE.is_file())
        self.assertNotIn("rawTrials", value)

    def test_explicit_historical_reference_preserves_its_fingerprints_and_metadata(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / "comparisons").mkdir()
            (root / "runs" / "trial").mkdir(parents=True)
            metadata = dict(modelConfiguration=dict(maxTurns=20), pricingConfiguration=dict(inputPerMillion=2.5))
            (root / "runs" / "trial" / "metadata.json").write_text(json.dumps(metadata))
            fingerprints = dict(prompt="hash", schema="schema", tools="tools", dataset="data")
            value = dict(comparisonId="historical", promptRevision="exposure-analysis/v3",
                         promptHash="hash", outcome=dict(isValid=True), rawTrials=[dict(runId="trial")],
                         fairness=dict(classicFingerprints=fingerprints, codeActFingerprints=fingerprints))
            path = root / "comparisons" / "comparison.json"
            path.write_text(json.dumps(value))
            loaded = load_reference(path)
            self.assertEqual(fingerprints, loaded["fingerprints"])
            self.assertEqual(metadata["modelConfiguration"], loaded["modelConfiguration"])
            value["outcome"]["isValid"] = False
            path.write_text(json.dumps(value))
            with self.assertRaises(ValueError):
                load_reference(path)


class ProvisionPlanTests(unittest.TestCase):
    def test_plan_uses_corporate_sources_on_both_nsgs_and_accepts_public_key_formats(self):
        script = Path(__file__).resolve().parents[1] / "provision-vm.sh"
        with tempfile.TemporaryDirectory() as directory:
            public_key = Path(directory) / "fixture.pub"
            for algorithm in ("ssh-ed25519", "ssh-rsa", "ecdsa-sha2-nistp256",
                              "ecdsa-sha2-nistp384", "ecdsa-sha2-nistp521"):
                with self.subTest(algorithm=algorithm):
                    public_key.write_text(f"{algorithm} fixture\n")
                    result = subprocess.run(["bash", str(script), "--subscription", "fixture",
                                             "--ssh-public-key", str(public_key)], check=True,
                                            capture_output=True, text=True)
                    rules = [line for line in result.stdout.splitlines()
                             if line.startswith("az network nsg rule create") and "AllowSshFromCorpNet" in line]
                    self.assertEqual(2, len(rules))
                    self.assertTrue(all("--source-address-prefixes CorpNetPublic" in line for line in rules))
                    self.assertTrue(any("--nsg-name codeact-validation-subnet-nsg" in line for line in rules))
                    self.assertIn("--network-security-group codeact-validation-subnet-nsg", result.stdout)
                    self.assertIn("--nsg codeact-validation-nsg", result.stdout)
                    self.assertIn("AllowBringYourOwnPublicIpAddress", result.stdout)
                    self.assertNotIn("CurrentIp", result.stdout)
                    self.assertIn("PLAN ONLY", result.stdout)

    def test_private_or_unsupported_key_input_is_rejected_without_provisioning(self):
        with tempfile.TemporaryDirectory() as directory:
            key = Path(directory) / "fixture"
            key.write_text("not-a-public-key\n")
            result = subprocess.run(["bash", str(Path(__file__).resolve().parents[1] / "provision-vm.sh"),
                                     "--subscription", "fixture", "--ssh-public-key", str(key)],
                                    capture_output=True, text=True)
            self.assertNotEqual(0, result.returncode)
            self.assertIn("PUBLIC key", result.stderr)


class WithinModelTests(unittest.TestCase):
    def test_implicit_local_policy_requires_an_audited_legacy_fingerprint(self):
        cohort = COHORTS["gpt-6.1-sol-v3"]
        validate_function_policy({}, cohort["auditedLegacySequentialFingerprint"], cohort)
        with self.assertRaises(ValueError):
            validate_function_policy({}, "unknown", cohort)
        with self.assertRaises(ValueError):
            validate_function_policy(dict(functionInvocationPolicy="concurrent"), "unknown", cohort)

    def comparison(self):
        trials = []
        for mode, factor in (("classic", 1), ("codeact", Decimal(".5"))):
            for tokens, match in ((100, "Exact"), (300, "UnearnedCompleteness")):
                trials.append(dict(mode=mode, totalTokens=tokens * factor,
                                   estimatedCostUsd=Decimal(tokens) / 100 * factor,
                                   analysisDurationMs=tokens * 10 * factor, retryWaitMs=0,
                                   turnLimitReached=0, toolArgumentErrors=0,
                                   sandboxOutputOverflowErrors=0, http429Count=0,
                                   score=dict(matchClass=match)))
        return dict(rawTrials=trials, outcome=dict(isValid=True))

    def test_equal_counts_include_incorrect_trials_in_resource_deltas(self):
        cells = report.within_model_cells("cohort", "n1", self.comparison())
        self.assertIn("200 -> 100", cells)
        self.assertEqual("-50.0% / -50.0% / -50.0%", cells[-2])
        self.assertEqual("correctness-equivalent by count (1/2 each)", cells[-1])

    def test_unequal_counts_are_not_labelled_equivalent(self):
        data = self.comparison()
        data["rawTrials"][-1]["score"]["matchClass"] = "Exact"
        self.assertEqual("not correctness-equivalent: correct counts differ",
                         report.within_model_cells("cohort", "n1", data)[-1])

    def test_invalid_or_empty_evidence_has_no_equivalence_or_resource_delta(self):
        data = self.comparison()
        data["outcome"]["isValid"] = False
        cells = report.within_model_cells("cohort", "n3", data)
        self.assertNotIn("correctness-equivalent", cells[-1])
        self.assertEqual("unavailable / unavailable / unavailable", cells[-2])
        data["rawTrials"] = []
        self.assertEqual("unavailable / unavailable / unavailable",
                         report.within_model_cells("cohort", "n3", data)[-2])


class ContinuationTests(unittest.TestCase):
    def run_mocked(self, directory, *, infrastructure_failure=False, push_failure=False, reference=True,
                   sensitivity=False):
        commands = []
        def start(command, **kwargs):
            commands.append(command)
            process = mock.MagicMock()
            process.__enter__.return_value = process
            process.stdout = iter(["persisted model progress\n"])
            process.wait.return_value = 1 if infrastructure_failure else 0
            return process
        def run(command, **kwargs):
            commands.append(command)
            if push_failure and command[:2] == ["git", "push"]:
                raise subprocess.CalledProcessError(1, command)
        argv = ["resume-sol-v3.py", "--gpt4o-root", str(directory / "4o"), "--sol-root", str(directory / "sol"),
                "--log-root", str(directory)]
        if reference:
            argv += ["--reference-comparison", str(directory / "reference.json")]
        if sensitivity:
            argv += ["--cohort", "gpt-6.1-sol-v3-t600", "--sol-baseline-root", str(directory / "baseline")]
        with mock.patch("sys.argv", argv), mock.patch.object(resume.subprocess, "Popen", side_effect=start), \
                mock.patch.object(resume.subprocess, "run", side_effect=run), \
                mock.patch.object(resume.subprocess, "check_output", return_value="a" * 40), \
                mock.patch("builtins.print"):
            if infrastructure_failure or push_failure:
                with self.assertRaises(subprocess.CalledProcessError):
                    resume.main()
            else:
                resume.main()
        return commands

    def test_continuation_uses_committed_reference_by_default(self):
        with tempfile.TemporaryDirectory() as directory:
            commands = self.run_mocked(Path(directory), reference=False)
            children = [command for command in commands if command[0] == "python3"
                        and command[1].endswith(("run-v3-profile.py", "check-v3-profile.py"))]
            self.assertEqual(4, len(children))
            self.assertTrue(all("--reference-comparison" not in command for command in children))

    def test_check_report_commit_and_push_precede_the_next_profile_and_progress_is_persisted(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            commands = self.run_mocked(root)
            full = next(i for i, command in enumerate(commands)
                        if command[1].endswith("run-v3-profile.py") and command[2] == "full-small")
            for name in ("check-v3-profile.py", "report-v3-cohorts.py"):
                self.assertTrue(any(command[1].endswith(name) for command in commands[:full]))
            self.assertTrue(any(command[:2] == ["git", "commit"] for command in commands[:full]))
            self.assertTrue(any(command[:2] == ["git", "push"] for command in commands[:full]))
            self.assertEqual(2, sum(command[:2] == ["git", "push"] for command in commands))
            self.assertEqual("persisted model progress\n" * 2, (root / "sol-n3.log").read_text())

    def test_infrastructure_failure_stops_before_checker_or_next_profile(self):
        with tempfile.TemporaryDirectory() as directory:
            commands = self.run_mocked(Path(directory), infrastructure_failure=True)
            self.assertEqual(1, len(commands))
            self.assertIn("n3", commands[0])

    def test_failed_publication_prevents_the_next_profile(self):
        with tempfile.TemporaryDirectory() as directory:
            commands = self.run_mocked(Path(directory), push_failure=True)
            self.assertFalse(any("full-small" in command for command in commands))
            self.assertEqual(["git", "push"], commands[-1][:2])

    def test_sensitivity_checks_and_publishes_separately_before_the_next_profile(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            commands = self.run_mocked(root, sensitivity=True, reference=False)
            children = [command for command in commands if command[0] == "python3"
                        and command[1].endswith(("run-v3-profile.py", "check-v3-profile.py"))]
            self.assertEqual(4, len(children))
            self.assertTrue(all(command[command.index("--cohort") + 1] == "gpt-6.1-sol-v3-t600"
                                for command in children))
            reports = [command for command in commands if command[1].endswith("report-v3-cohorts.py")]
            self.assertEqual(2, len(reports))
            self.assertTrue(all("--sol-t600-root" in command and command[-1].endswith("v3-time-budget-sensitivity.md")
                                for command in reports))
            full = commands.index(children[2])
            self.assertTrue(any(command[:2] == ["git", "push"] for command in commands[:full]))
            self.assertTrue((root / "sol-t600-n3.log").is_file())
            self.assertFalse((root / "sol-n3.log").exists())

    def test_sensitivity_infrastructure_failure_does_not_launch_full_small(self):
        with tempfile.TemporaryDirectory() as directory:
            commands = self.run_mocked(Path(directory), sensitivity=True, infrastructure_failure=True)
            self.assertEqual(1, len(commands))


class SensitivityTests(unittest.TestCase):
    def test_checker_accepts_only_the_named_timeout_in_manifest_and_all_run_metadata(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            profile = root / "n3"
            comparisons = profile / "comparisons"
            comparisons.mkdir(parents=True)
            reference = load_reference()
            cohort = "gpt-6.1-sol-v3-t600"
            spec = COHORTS[cohort]
            config = dict(deploymentName=spec["deployment"], modelName=spec["model"], modelVersion=spec["version"],
                          defaultReasoningEffort=spec["reasoning"], apiMode="responses", store=False,
                          contextPolicy="full-context/no-previous-response-id/v1", turnLimitEnforcement="model-request-boundary/v1",
                          functionInvocationPolicy="sequential-within-response/v1", maxTurns=20, timeoutSeconds=600,
                          maxOutputTokens=16384, strict=False, parallel_tool_calls=True, temperature=None)
            metadata = dict(modelConfiguration=config, pricingConfiguration=spec["rates"])
            trials = [dict(mode=mode, pairId=pair, runId=f"{mode}-{pair}", toolArgumentErrors=0,
                           telemetryValid=True, infrastructureFailed=False, modelTurnCount=0, http429Count=0,
                           turnLimitReached=0, sandboxOutputOverflowErrors=0, retryWaitMs=0, status="TimedOut",
                           totalTokens=None, estimatedCostUsd=None, analysisDurationMs=600000,
                           score=dict(matchClass="TimedOut"))
                      for pair in range(6) for mode in ("classic", "codeact")]
            fingerprints = reference["fingerprints"]
            data = dict(outcome=dict(isValid=True), promptRevision=reference["promptRevision"],
                        promptHash=reference["promptHash"], executionKind="live-model-hyperlight",
                        scopedHouseholdCount=3, measuredTrials=5, warmupsPerMode=1, modelFingerprint="t600-model",
                        rawTrials=trials[2:], warmupTrials=trials[:2],
                        fairness=dict(classicFingerprints=fingerprints, codeActFingerprints=fingerprints))
            (comparisons / "comparison.json").write_text(json.dumps(data, default=float))
            launch = dict(cohort=cohort, timeoutSeconds=600, trials=5, smoke=False)
            manifest = profile / "launch.json"
            manifest.write_text(json.dumps(launch))
            (profile / "deployment-pricing-verification.json").write_text(json.dumps(dict(cohort=cohort)))
            for trial in trials:
                run = profile / "runs" / trial["runId"]
                run.mkdir(parents=True)
                (run / "events.jsonl").write_text("")
                (run / "timeline.md").touch()
                (run / "metadata.json").write_text(json.dumps(metadata))
            with mock.patch("sys.argv", ["check-v3-profile.py", "n3", "--cohort", cohort]), \
                    mock.patch.dict(checker.os.environ, dict(ARTIFACTS_ROOT=str(root))), mock.patch("builtins.print"):
                checker.main()
                launch["timeoutSeconds"] = 120
                manifest.write_text(json.dumps(launch))
                with self.assertRaises(AssertionError):
                    checker.main()
                launch["timeoutSeconds"] = 600
                manifest.write_text(json.dumps(launch))
                config["timeoutSeconds"] = 120
                (profile / "runs" / trials[-1]["runId"] / "metadata.json").write_text(json.dumps(metadata))
                with self.assertRaises(AssertionError):
                    checker.main()

    def test_sensitivity_matches_sol_except_timeout_and_has_no_legacy_policy_fallback(self):
        baseline = COHORTS["gpt-6.1-sol-v3"]
        sensitivity = COHORTS["gpt-6.1-sol-v3-t600"]
        self.assertEqual({key: value for key, value in baseline.items()
                          if key not in ("timeoutSeconds", "auditedLegacySequentialFingerprint")},
                         {key: value for key, value in sensitivity.items() if key != "timeoutSeconds"})
        with self.assertRaises(ValueError):
            validate_function_policy({}, baseline["auditedLegacySequentialFingerprint"], sensitivity)
        reference = load_reference()
        self.assertEqual(600, reference["cohortTimeoutSeconds"]["gpt-6.1-sol-v3-t600"])
        for cohort, timeout in (("gpt-6.1-sol-v3", "120"), ("gpt-6.1-sol-v3-t600", "600")):
            env = dict(BENCHMARK_TIMEOUT_SECONDS="999")
            configure_environment(env, cohort)
            self.assertEqual(timeout, env["BENCHMARK_TIMEOUT_SECONDS"])

    def test_t600_launch_pins_timeout_and_separate_cohort_in_environment_and_manifest(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            guest = root / "guest.aot"
            guest.touch()
            env = dict(AZURE_OPENAI_ENDPOINT="https://example.invalid/", AZURE_OPENAI_DEPLOYMENT_NAME="gpt-6.1-sol",
                       HYPERLIGHT_PYTHON_GUEST_PATH=str(guest), DATASET_ROOT=str(root / "data"),
                       ARTIFACTS_ROOT=str(root / "cohort"), BENCHMARK_TIMEOUT_SECONDS="120")
            with mock.patch("sys.argv", ["run-v3-profile.py", "n3", "--cohort", "gpt-6.1-sol-v3-t600"]), \
                    mock.patch.dict(runner.os.environ, env, clear=True), \
                    mock.patch.object(runner.subprocess, "check_output", side_effect=["", "a" * 40]), \
                    mock.patch.object(runner.subprocess, "run"), mock.patch.object(runner, "verify", return_value={}), \
                    mock.patch.object(runner.os, "chdir"), mock.patch.object(runner.os, "execvpe") as execute, \
                    mock.patch("builtins.print"):
                runner.main()
            launched = execute.call_args.args[2]
            self.assertEqual("600", launched["BENCHMARK_TIMEOUT_SECONDS"])
            self.assertEqual("20", launched["BENCHMARK_MAX_TURNS"])
            self.assertEqual("0", launched["CLASSIC_BATCH_SIZE"])
            launch = json.loads((root / "cohort" / "n3" / "launch.json").read_text())
            self.assertEqual(("gpt-6.1-sol-v3-t600", 600, 5),
                             (launch["cohort"], launch["timeoutSeconds"], launch["trials"]))

    def test_other_cohort_cannot_be_written_into_existing_root(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / "n1").mkdir()
            (root / "n1" / "launch.json").write_text(json.dumps(dict(cohort="gpt-6.1-sol-v3")))
            with self.assertRaises(ValueError):
                validate_cohort_root(root, "gpt-6.1-sol-v3-t600")
            validate_cohort_root(root, "gpt-6.1-sol-v3")

    def test_sensitivity_keeps_timeout_outcomes_in_medians_and_labels_only_equal_correct_counts(self):
        baseline = WithinModelTests().comparison()
        for trial in baseline["rawTrials"]:
            trial["status"] = "TimedOut"
        sensitive = json.loads(json.dumps(baseline, default=float))
        for trial in sensitive["rawTrials"]:
            trial["analysisDurationMs"] *= 2
        row = report.sensitivity_cells("n3", "classic", 600, sensitive, baseline)
        self.assertEqual(["1/2", "2", "0", "4.000", "200", "2.0000000"], row[3:9])
        self.assertEqual("+0.0% / +0.0% / +100.0%", row[-2])
        self.assertIn("correctness-equivalent by count", row[-1])
        sensitive["rawTrials"][1]["score"]["matchClass"] = "Exact"
        self.assertEqual("not correctness-equivalent: correct counts differ",
                         report.sensitivity_cells("n3", "classic", 600, sensitive, baseline)[-1])
        sensitive["outcome"]["isValid"] = False
        row = report.sensitivity_cells("n3", "classic", 600, sensitive, baseline)
        self.assertEqual("unavailable / unavailable / unavailable", row[-2])
        self.assertNotIn("correctness-equivalent", row[-1])

    def test_sensitivity_roots_cannot_overlap_or_silently_hide_wrong_timeout(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            with self.assertRaises(ValueError):
                report.sensitivity_lines(root, root / "nested")
            comparison = root / "t600" / "n3" / "comparisons"
            comparison.mkdir(parents=True)
            (comparison / "report.json").write_text("{}")
            (comparison.parent / "launch.json").write_text(json.dumps(dict(
                cohort="gpt-6.1-sol-v3-t600", smoke=False, trials=5, timeoutSeconds=120)))
            with self.assertRaisesRegex(ValueError, "Wrong cohort timeout"):
                report.load_profile(root / "t600", "n3", "gpt-6.1-sol-v3-t600")


if __name__ == "__main__":
    unittest.main()
