#!/usr/bin/env python3
"""Exercise the reference checker as a CLI, including corrupted fixture copies."""
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest

SCRIPT = Path(__file__).resolve().with_name("check-spending-reconciliation.py")
FIXTURE = SCRIPT.parent.parent / "tests/fixtures/spending-reconciliation/cases.json"
CASE_IDS = {
    "empty-register", "exact-cents", "category-hierarchy",
    "purchase-date-boundaries", "calendar-year-boundary", "distinct-register-rows",
}


class SpendingReconciliationTest(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="pennypath-reconciliation-")
        self.addCleanup(self.temp.cleanup)
        self.directory = Path(self.temp.name)
        self.document = json.loads(FIXTURE.read_text(encoding="utf-8"))

    def case(self, case_id):
        return next(case for case in self.document["cases"] if case["id"] == case_id)

    def run_check(self, document=None, path=None):
        if document is not None:
            path = self.directory / "cases.json"
            path.write_text(json.dumps(document), encoding="utf-8")
        before = path.read_bytes() if path is not None and path.is_file() else None
        command = [sys.executable, str(SCRIPT)]
        if path is not None:
            command.append(str(path))
        result = subprocess.run(command, cwd=self.directory, text=True, capture_output=True, timeout=10)
        if before is not None:
            self.assertEqual(path.read_bytes(), before, "Checker must not modify its input")
        self.assertNotIn("Traceback", result.stdout + result.stderr)
        return result

    def assert_mismatch(self, case_id, field):
        result = self.run_check(self.document)
        self.assertEqual(result.returncode, 1, result.stdout + result.stderr)
        self.assertIn(f"FAIL {case_id}:", result.stdout)
        self.assertIn(field, result.stdout)
        self.assertIn("expected=", result.stdout)
        self.assertIn("actual=", result.stdout)

    def assert_invalid(self, field):
        result = self.run_check(self.document)
        self.assertEqual(result.returncode, 2, result.stdout + result.stderr)
        self.assertIn(field, result.stderr)

    def test_reference_cases_pass(self):
        # Running from a different cwd also protects default-path resolution.
        result = self.run_check()
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        self.assertEqual(set(result.stdout.splitlines()[:-1]), {f"PASS {case_id}" for case_id in CASE_IDS})
        self.assertIn("6 cases checked; 0 failed", result.stdout)
        self.assertEqual(result.stderr, "")

    def test_valid_explicit_copy_passes(self):
        result = self.run_check(self.document)
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)

    def test_wrong_total_by_one_grosz_is_rejected(self):
        self.case("exact-cents")["expected"]["total"] = "0.32"
        self.assert_mismatch("exact-cents", "total")

    def test_swapped_subcategory_amounts_with_same_total_are_rejected(self):
        children = self.case("exact-cents")["expected"]["subcategories"]["food"]
        children["groceries"], children["drinks"] = "0.01", "0.30"
        self.assert_mismatch("exact-cents", "subcategories")

    def test_wrong_category_with_same_overall_total_is_rejected(self):
        self.case("category-hierarchy")["expenses"][0]["category_id"] = "transport"
        self.assert_mismatch("category-hierarchy", "categories")

    def test_purchase_date_moves_across_boundary_without_changing_entry_date(self):
        self.case("purchase-date-boundaries")["expenses"][1]["purchase_date"] = "2025-12-31"
        self.assert_mismatch("purchase-date-boundaries", "expense_ids")

    def test_wrong_expected_ids_are_rejected(self):
        self.case("exact-cents")["expected"]["expense_ids"][0] = "missing-id"
        self.assert_mismatch("exact-cents", "expense_ids")

    def test_record_order_has_no_effect(self):
        for case in self.document["cases"]:
            case["expenses"].reverse()
            case["expected"]["expense_ids"].reverse()
        result = self.run_check(self.document)
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)

    def test_missing_file_reports_input_error(self):
        result = self.run_check(path=self.directory / "missing.json")
        self.assertEqual(result.returncode, 2)
        self.assertIn("cannot read", result.stderr)

    def test_invalid_json_reports_input_error(self):
        path = self.directory / "invalid.json"
        path.write_text("{", encoding="utf-8")
        result = self.run_check(path=path)
        self.assertEqual(result.returncode, 2)
        self.assertIn("JSON", result.stderr)

    def test_empty_case_list_reports_input_error(self):
        self.document["cases"] = []
        self.assert_invalid("cases")

    def test_invalid_amount_formats_are_rejected(self):
        expense = self.case("exact-cents")["expenses"][0]
        for value in [0.1, "0.1", "0.001", "-0.10", "NaN", "Infinity", "0.00"]:
            with self.subTest(value=value):
                expense["amount"] = value
                self.assert_invalid("exact-cents.expenses[0].amount")

    def test_invalid_date_is_rejected(self):
        self.case("exact-cents")["expenses"][0]["purchase_date"] = "2026-02-30"
        self.assert_invalid("purchase_date")

    def test_invalid_entry_date_is_rejected_even_though_it_does_not_select_rows(self):
        self.case("exact-cents")["expenses"][0]["recorded_date"] = "invalid"
        self.assert_invalid("recorded_date")

    def test_empty_period_is_rejected(self):
        self.case("empty-register")["period"]["end_exclusive"] = "2026-01-01"
        self.assert_invalid("empty-register.period")

    def test_duplicate_expense_ids_are_rejected(self):
        self.case("exact-cents")["expenses"][1]["id"] = "c1"
        self.assert_invalid("expenses.id")

    def test_duplicate_case_ids_are_rejected(self):
        self.document["cases"][1]["id"] = "empty-register"
        self.assert_invalid("cases.id")

    def test_duplicate_expected_ids_are_rejected(self):
        self.case("exact-cents")["expected"]["expense_ids"].append("c1")
        self.assert_invalid("expected.expense_ids")

    def test_malformed_structure_reports_input_error(self):
        self.case("exact-cents")["expected"]["categories"] = []
        self.assert_invalid("expected.categories")

    def test_missing_required_field_reports_input_error(self):
        del self.case("exact-cents")["expenses"][0]["subcategory_id"]
        self.assert_invalid("subcategory_id")

    def test_unsupported_schema_and_currency_are_rejected(self):
        self.document["schema_version"] = True
        self.assert_invalid("schema_version")
        self.document["schema_version"] = 1
        self.document["currency"] = "EUR"
        self.assert_invalid("currency")

    def test_long_decimal_amounts_sum_without_rounding(self):
        case = self.case("distinct-register-rows")
        self.document["cases"] = [case]
        case["expenses"][0]["amount"] = "9999999999999999999999999999.99"
        case["expenses"][1]["amount"] = "0.01"
        total = "10000000000000000000000000000.00"
        case["expected"]["total"] = total
        case["expected"]["categories"] = {"food": total}
        case["expected"]["subcategories"] = {"food": {"drinks": total}}
        result = self.run_check(self.document)
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)


if __name__ == "__main__":
    unittest.main()
