---
project: PennyPath
context_type: greenfield
product_type: web-app
target_scale:
  users: medium
created: 2026-10-04
updated: 2026-10-04
timeline_budget:
  mvp_weeks: 5
  hard_deadline: 2026-12-06
  after_hours_only: true
checkpoint:
  current_phase: 8
  phases_completed: [1, 2, 3, 4, 5, 6, 7]
  gray_areas_resolved:
    - topic: Project context
      decision: User confirmed greenfield; building from scratch.
    - topic: Categorization scope
      decision: Manually categorize entries and CSV imports in the MVP; AI categorization is a later extension.
    - topic: Mixed purchases
      decision: MVP supports manually splitting a transaction across categories and subcategories.
    - topic: Primary persona scope
      decision: Individuals tracking their own spending; each person manages their own expenses.
    - topic: Authentication
      decision: Email and password login.
    - topic: Roles and data access
      decision: One user role; each person sees and manages only their own expenses.
    - topic: First working milestone
      decision: Dashboard and manual expense entry first; CSV import follows within the intended MVP.
    - topic: MVP duration
      decision: Up to five weeks at roughly ten hours per week; user explicitly accepted sustained effort.
    - topic: Budgeting scope
      decision: Users use the spending breakdown to spot habits and plan savings themselves; dedicated budgeting and savings projections are outside the MVP.
    - topic: Expense period assignment
      decision: Use purchase date rather than entry or import date.
    - topic: Split aggregation
      decision: Each allocated amount contributes to its assigned category/subcategory; count the original purchase only once.
    - topic: Product surface
      decision: Browser-based MVP; native iOS and Android apps are future work outside MVP planning.
    - topic: Initial audience and growth
      decision: Up to 10 initial users; the same domain calculations and private per-user access rules apply at 100 times that scale.
    - topic: Work schedule
      decision: Planned project work is outside regular working hours.
    - topic: MVP boundaries
      decision: No direct bank connections; PLN only; shared household accounts and offline use are possible future additions, outside the MVP.
  frs_drafted: 8
  quality_check_status: accepted
---

## Seed idea

PennyPath helps users understand where their money goes through expense categories, subcategories, and visual breakdowns. Initial data entry should support manual input and CSV import. Later, I want AI-assisted extraction and categorization of individual purchases from receipt photos and PDFs

## Vision & Problem Statement

People like the creator find it hard to track their expenses. At the end of the month, they realize they spent more than planned and want to understand where their money went. Today, they manually review bank transactions and match them to orders or shopping lists; this is difficult and time-consuming.

PennyPath provides categorized and subcategorized expense breakdowns so people can see which purchases account for their spending and potentially change their habits. The MVP supports manual entry, CSV import, manual categorization, and manual splitting of mixed purchases across categories and subcategories. AI-assisted categorization and extraction from receipt photos and PDFs are later extensions.

Scale note: the initial audience is up to 10 users. At 1,000 users, the spending calculations and per-user access rules remain unchanged.

## User & Persona

The primary persona is an individual like the creator who struggles to track personal expenses and reviews spending at month-end after exceeding their planned spending. Each person manages their own expenses. Shared household tracking is outside the selected initial persona scope.

## Success Criteria

### Primary
- First milestone: a user sees current-period spending by category/subcategory and previous-period spending for comparison. The period start day is configurable, such as the 10th instead of the 1st. Adding a manual expense with a name, amount, and category/subcategory adds it to the expense register and refreshes the dashboard to include it.
- Complete MVP: a user can upload or drag in a bank CSV, preview loaded records, adjust the separator, decimal separator, and line endings, then import validated records with duplicate checking. The expense register and dashboard reflect the imported expenses. Import is available whenever the user chooses, not only at month-end.

### Secondary
- Users identify spending habits they want to change, such as spending on soda, cigarettes, or unused subscriptions, and use the breakdown to plan savings themselves, including future vacation savings. The MVP does not provide dedicated budgeting or savings projections.

### Guardrails
- Visualizations and overviews must agree with the expense register; incorrect spending summaries are blockers.
- Users must never see another person's expenses.

## User Stories

### US-01: Add and categorize an expense

- **Given** I am signed in
- **When** I add an expense manually or via CSV import and categorize it
- **Then** I see the adjusted spending widget and updated expense register

#### Acceptance Criteria
- The expense register includes the added expense.
- The spending widget reflects the added and categorized expense.

## Functional Requirements

### Account access
- FR-001: User can sign in with email and password, recover a forgotten password, and access only their own expenses. Priority: must-have
  > Socratic: Forgotten passwords could prevent access to financial history. Resolution: include password recovery.

### Spending overview
- FR-002: User can view current-period spending by category/subcategory and compare it with the previous period. Priority: must-have
  > Socratic: Comparing an unfinished current period with a complete previous period could mislead. Resolution: kept as written; no additional incomplete-period label or date-range requirement accepted.
- FR-003: User can configure the spending period's start day from days 1 through 28. Priority: must-have
  > Socratic: Days 29 through 31 do not occur every month. Resolution: restrict the MVP to start days 1 through 28.

### Expense and category management
- FR-004: User can add, view, edit, and delete expenses with confirmation before deletion and see dashboard totals update. Priority: must-have
  > Socratic: Accidental deletion could corrupt the user's records. Resolution: require confirmation before deletion.
- FR-005: User can split a mixed purchase across categories/subcategories with split amounts required to equal the original purchase total. Priority: must-have
  > Socratic: Split allocations could exceed or fall short of the original total. Resolution: require allocations to equal the original total.
- FR-006: User can configure personal categories/subcategories, starting with a provided set, and reassign affected expenses before deleting an in-use category or subcategory. Priority: must-have
  > Socratic: Reassignment could silently change historical breakdowns. Resolution: kept as written; no additional impact-preview requirement accepted.

### CSV import
- FR-007: User can upload or drag in a bank CSV using one agreed layout and preview records with adjustable separator, decimal separator, and line-ending settings. Priority: must-have
  > Socratic: Supporting arbitrary bank layouts could consume the MVP timeline. Resolution: support one agreed bank CSV layout initially while retaining the requested parsing settings.
- FR-008: User can import validated CSV records, review suspected duplicates rather than have them silently discarded, and see the expense register and dashboard update. Priority: must-have
  > Socratic: Legitimate purchases can share a date, description, and amount. Resolution: flag suspected duplicates for user review rather than silently discard them.

## Non-Functional Requirements

- The MVP is available through a browser.
- Spending visualizations and overviews agree with the expense register for the selected period and categorization.
- No user can view another user's expenses.

## Business Logic

PennyPath groups expenses by category for the selected spending period using purchase dates, sums their amounts, displays category totals in a pie chart, and drills down to subcategory totals when the user clicks a category.

Inputs are the user's manually entered or CSV-imported expenses, their purchase dates, category/subcategory assignments, and the selected spending period with its configurable start day. For split purchases, each allocated amount contributes to its assigned category/subcategory, and the original purchase is counted only once; split allocations must equal the original purchase total.

Outputs are category totals in a pie chart and subcategory totals after selecting a category. These appear in the spending overview, which also supports comparison with the previous period and refreshes after expense changes or imports.

## Access Control

Users sign in with email and password. There is one user role. Each user can see and manage only their own expenses.

## Non-Goals

- AI-assisted categorization and receipt/photo/PDF extraction are later extensions; the MVP uses manual categorization.
- Native iOS and Android apps are future work and are not required or planned for this MVP; the initial product is browser-based.
- Dedicated budgeting and savings projections are outside the MVP; users plan savings themselves using the spending breakdown.
- Direct bank connections are excluded from the MVP; manual entry and CSV import provide initial expense input.
- Shared household accounts are a possible future addition; the MVP serves individuals managing private expenses.
- Multiple currencies and currency conversion are excluded; the MVP uses PLN only.
- Guaranteed offline use is outside the MVP and may be considered later; offline functionality is not an initial commitment.
- Arbitrary bank CSV layouts are excluded from the MVP; one agreed layout is supported with the specified parsing settings.

## Timeline acknowledgment

Acknowledged on 2026-10-04: 5-week MVP requires sustained dedication; user accepted. Planned availability is roughly 10 hours per week.

## Open Questions

1. **Which bank CSV layout will the MVP support?** — Owner: user. Resolve before implementing CSV import.

## Quality cross-check

- Access Control: present — email/password authentication, one role, private per-user expenses.
- Business Logic: present — purchase-date period aggregation, category pie chart, subcategory drilldown, and balanced splits counted once.
- Project artifacts: present — shape-notes.md contains a resumable checkpoint and confirmed greenfield context.
- Timeline-cost acknowledgment: present — user accepted a five-week MVP at roughly ten hours per week outside regular work.
- Non-Goals: present — MVP boundaries are explicitly recorded.
- Preserved behavior: not applicable — greenfield project.
- User accepted finalization with the bank CSV layout deferred until before import implementation. No gaps in the quality checks remain; the CSV layout is an explicit open product decision.
