#!/usr/bin/env python3
"""Offline behavioral checks for the committed cohort report."""
import importlib.util
import unittest
from decimal import Decimal
from pathlib import Path
from cohort_config import COHORTS, validate_function_policy

spec = importlib.util.spec_from_file_location("cohort_report", Path(__file__).with_name("report-v3-cohorts.py"))
report = importlib.util.module_from_spec(spec)
spec.loader.exec_module(report)


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


if __name__ == "__main__":
    unittest.main()
