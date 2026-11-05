#!/usr/bin/env python3
"""Validate the synthetic spending reference cases, without running the app."""
import argparse
from datetime import date
from decimal import Decimal, localcontext
import json
from pathlib import Path
import re
import sys

DEFAULT_FIXTURE = Path(__file__).resolve().parent.parent / "tests/fixtures/spending-reconciliation/cases.json"
ZERO = Decimal("0.00")


class FixtureError(ValueError):
    """The reference document cannot be interpreted according to its contract."""


def require_object(value, path):
    if not isinstance(value, dict):
        raise FixtureError(f"{path}: expected an object")
    return value


def require_list(value, path):
    if not isinstance(value, list):
        raise FixtureError(f"{path}: expected a list")
    return value


def require_text(value, path):
    if not isinstance(value, str) or not value.strip():
        raise FixtureError(f"{path}: expected nonempty text")
    return value


def parse_date(value, path):
    require_text(value, path)
    try:
        result = date.fromisoformat(value)
    except ValueError:
        raise FixtureError(f"{path}: expected a valid YYYY-MM-DD date") from None
    if result.isoformat() != value:
        raise FixtureError(f"{path}: expected a YYYY-MM-DD date")
    return result


def parse_amount(value, path, positive=False):
    if not isinstance(value, str) or re.fullmatch(r"[0-9]+\.[0-9]{2}", value) is None:
        raise FixtureError(f"{path}: expected a decimal string with two places")
    result = Decimal(value)
    if positive and result <= ZERO:
        raise FixtureError(f"{path}: expense amount must be positive")
    return result


def unique_ids(values, path):
    result = [require_text(value, f"{path}[{index}]") for index, value in enumerate(values)]
    if len(set(result)) != len(result):
        raise FixtureError(f"{path}: duplicate IDs")
    return result


def amount_map(value, path):
    mapping = require_object(value, path)
    return {
        require_text(key, path): parse_amount(amount, f"{path}.{key}")
        for key, amount in mapping.items()
    }


def check_case(case, index):
    case = require_object(case, f"cases[{index}]")
    case_id = require_text(case.get("id"), f"cases[{index}].id")
    require_text(case.get("description"), f"{case_id}.description")
    period = require_object(case.get("period"), f"{case_id}.period")
    start = parse_date(period.get("start_inclusive"), f"{case_id}.period.start_inclusive")
    end = parse_date(period.get("end_exclusive"), f"{case_id}.period.end_exclusive")
    if start >= end:
        raise FixtureError(f"{case_id}.period: start must precede end")

    expenses = require_list(case.get("expenses"), f"{case_id}.expenses")
    rows = []
    for row_index, expense in enumerate(expenses):
        path = f"{case_id}.expenses[{row_index}]"
        expense = require_object(expense, path)
        row_id = require_text(expense.get("id"), f"{path}.id")
        amount = parse_amount(expense.get("amount"), f"{path}.amount", positive=True)
        purchased = parse_date(expense.get("purchase_date"), f"{path}.purchase_date")
        # Entry dates are deliberately validated but never used for selection.
        parse_date(expense.get("recorded_date"), f"{path}.recorded_date")
        category = require_text(expense.get("category_id"), f"{path}.category_id")
        subcategory = require_text(expense.get("subcategory_id"), f"{path}.subcategory_id")
        rows.append((row_id, amount, purchased, category, subcategory))
    unique_ids([row[0] for row in rows], f"{case_id}.expenses.id")

    expected = require_object(case.get("expected"), f"{case_id}.expected")
    expected_ids = unique_ids(
        require_list(expected.get("expense_ids"), f"{case_id}.expected.expense_ids"),
        f"{case_id}.expected.expense_ids",
    )
    expected_total = parse_amount(expected.get("total"), f"{case_id}.expected.total")
    expected_categories = amount_map(expected.get("categories"), f"{case_id}.expected.categories")
    expected_subcategories = {
        require_text(category, f"{case_id}.expected.subcategories"): amount_map(
            values, f"{case_id}.expected.subcategories.{category}"
        )
        for category, values in require_object(
            expected.get("subcategories"), f"{case_id}.expected.subcategories"
        ).items()
    }

    differences = []

    def compare(field, wanted, actual):
        if wanted != actual:
            wanted_text = json.dumps(wanted, default=str, sort_keys=True, ensure_ascii=False)
            actual_text = json.dumps(actual, default=str, sort_keys=True, ensure_ascii=False)
            differences.append(f"{field}: expected={wanted_text}, actual={actual_text}")

    # Allow enough digits to sum even long valid decimal strings without rounding.
    with localcontext() as context:
        amounts = [row[1] for row in rows] + [expected_total]
        amounts += list(expected_categories.values())
        amounts += [amount for values in expected_subcategories.values() for amount in values.values()]
        context.prec = max(28, max(len(amount.as_tuple().digits) for amount in amounts) + len(str(len(rows) + len(amounts))) + 1)
        selected_ids = []
        total = ZERO
        categories = {}
        subcategories = {}
        for row_id, amount, purchased, category, subcategory in rows:
            if start <= purchased < end:
                selected_ids.append(row_id)
                total += amount
                categories[category] = categories.get(category, ZERO) + amount
                category_children = subcategories.setdefault(category, {})
                category_children[subcategory] = category_children.get(subcategory, ZERO) + amount

        compare("expense_ids", sorted(expected_ids), sorted(selected_ids))
        compare("total", expected_total, total)
        compare("categories", expected_categories, categories)
        compare("subcategories", expected_subcategories, subcategories)
        compare("categories.total", expected_total, sum(expected_categories.values(), ZERO))
        compare("subcategories.categories", sorted(expected_categories), sorted(expected_subcategories))
        for category, amount in expected_categories.items():
            compare(
                f"subcategories.{category}.total", amount,
                sum(expected_subcategories.get(category, {}).values(), ZERO),
            )
    return case_id, differences


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("fixture", nargs="?", type=Path, default=DEFAULT_FIXTURE)
    args = parser.parse_args()
    try:
        try:
            document = json.loads(args.fixture.read_text(encoding="utf-8"))
        except OSError as error:
            raise FixtureError(f"cannot read {args.fixture}: {error}") from None
        except (json.JSONDecodeError, UnicodeError) as error:
            raise FixtureError(f"invalid JSON in {args.fixture}: {error}") from None
        document = require_object(document, "document")
        if type(document.get("schema_version")) is not int or document["schema_version"] != 1:
            raise FixtureError("schema_version: expected integer 1")
        if document.get("currency") != "PLN":
            raise FixtureError("currency: expected PLN")
        cases = require_list(document.get("cases"), "cases")
        if not cases:
            raise FixtureError("cases: expected at least one reference case")
        results = [check_case(case, index) for index, case in enumerate(cases)]
        unique_ids([case_id for case_id, _ in results], "cases.id")
    except FixtureError as error:
        print(f"ERROR: {error}", file=sys.stderr)
        return 2

    failed = 0
    for case_id, differences in results:
        if differences:
            failed += 1
            for difference in differences:
                print(f"FAIL {case_id}: {difference}")
        else:
            print(f"PASS {case_id}")
    print(f"{len(results)} cases checked; {failed} failed")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
