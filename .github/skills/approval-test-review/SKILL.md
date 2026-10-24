---
name: approval-test-review
description: Review AI-generated invoice approval tests for boundary errors, approval identity bugs, and rejected-attempt mutation. Use after generating or changing unit or E2E tests for invoice approvals.
---
# Approval test review skill

When reviewing generated tests:

1. Read `AGENTS.md` and `openspec/changes/add-role-based-approval-limits/specs/invoice-approval/spec.md` first.
2. Build a requirement-to-test matrix. Do not accept a test merely because it exercises a code path; write down the defect it would catch.
3. Check threshold boundaries explicitly: Manager 50,000 succeeds solo; Manager 50,000.01 cannot finalize solo; Director 500,000 succeeds solo; amounts greater than 500,000 remain pending after the first approval.
4. Check the high-value positive path: first approval is recorded but not final; a distinct Director completes it; both identities remain auditable.
5. Check both negative identity/role rules: same username cannot count twice even if the role argument differs between service calls, and a Manager/non-Director cannot be the second approver.
6. For every rejection, capture a deep snapshot of all observable invoice properties and approval history, plus repository update count where practical. Assert the snapshot and update count are unchanged. Remember the fake repository currently returns shared object references, so a shallow reference comparison is insufficient.
7. Check that validation occurs before mutation. Include test coverage for an invoice that is not PendingApproval and for unknown roles where applicable.
8. Review Playwright separately: it must sign in, create/submit an invoice through the browser, click the approval action, see the error via `getByRole('alert')`, and see Pending Approval still displayed. Use accessible role/label locators, not CSS/XPath.
9. Ask: Would the test fail if (a) `>` became `>=`, (b) the same username were allowed twice, (c) Manager were allowed to finalize 50,000.01, (d) a Manager completed the second stage, or (e) the service mutated before rejecting? If not, strengthen it.
10. Report actual execution separately from test-design review. Never describe a test as passing unless it has been run.

Expected review output:

| Requirement | Test | Defect caught | Gap | Action |
|---|---|---|---|---|

Then list any blockers with a proposed test and the exact invariant it proves.
