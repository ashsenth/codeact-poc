#!/usr/bin/env python3
"""Offline behavioral checks for the committed cohort report."""
import importlib.util
import subprocess
import tempfile
import unittest
from decimal import Decimal
from pathlib import Path
from unittest import mock
from cohort_config import COHORTS, validate_function_policy

spec = importlib.util.spec_from_file_location("cohort_report", Path(__file__).with_name("report-v3-cohorts.py"))
report = importlib.util.module_from_spec(spec)
spec.loader.exec_module(report)
resume_spec = importlib.util.spec_from_file_location("resume_sol", Path(__file__).with_name("resume-sol-v3.py"))
resume = importlib.util.module_from_spec(resume_spec)
resume_spec.loader.exec_module(resume)


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
    def run_mocked(self, directory, *, infrastructure_failure=False, push_failure=False):
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
        argv = ["resume-sol-v3.py", "--reference-comparison", str(directory / "reference.json"),
                "--gpt4o-root", str(directory / "4o"), "--sol-root", str(directory / "sol"),
                "--log-root", str(directory)]
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


if __name__ == "__main__":
    unittest.main()
