# Tasks

## 1. Phase 1: solo approval limits and xUnit coverage

- [ ] 1.1 Implement the Manager solo approval rule for invoices up to and including 50,000 and verify the xUnit test suite for the solo-limit boundary passes.
- [ ] 1.2 Implement the Director solo approval rule for invoices up to and including 500,000 and verify the xUnit tests for the Director boundary pass.
- [ ] 1.3 Add xUnit tests for rejected over-limit solo approval attempts, including the clear rejection reason and no-mutation expectation, and verify the phase-specific tests pass.
- [ ] 1.4 Commit Phase 1 separately from Phase 2 with a summary covering the solo approval limits, boundary behavior, and verification evidence.

## 2. Phase 2: high-value two-stage approvals and xUnit coverage

- [ ] 2.1 Implement the two-stage approval rule for invoices above 500,000 and verify the new xUnit coverage for the high-value path passes.
- [ ] 2.2 Enforce the distinct-username invariant so the same user cannot count as both approvers on one invoice, and verify the invalid re-use case is rejected with a clear reason.
- [ ] 2.3 Require the second approver to be a Director for invoices above 500,000 and verify the invalid non-Director second-stage case is rejected without mutation.
- [ ] 2.4 Add xUnit tests for rejected high-value approval attempts ensuring the invoice remains unchanged and the reason clearly states the rule violation.
- [ ] 2.5 Commit Phase 2 separately from Phase 1 with a summary covering the two-stage approval flow, distinct approver enforcement, and validation evidence.

## 3. E2E and quality verification

- [ ] 3.1 Add one Playwright test covering a rejected over-limit approval and verify the browser flow records the rejection reason and preserves the invoice state.
- [ ] 3.2 Capture mutation-test baseline evidence before the change and final mutation-test evidence after the change, and confirm the acceptance criteria are preserved.
- [ ] 3.3 Complete a focused test review to verify the boundary conditions, rejection reasons, distinct-approver invariant, and no-mutation behavior are all covered by automated tests.
- [ ] 3.4 Create a PR finding-verdict summary that records the final review outcome, confirms scope matches the approved spec, and lists any remaining follow-up items.

## Workflow follow-up

- Archive the change after review requirements are satisfied and verify the archived result reflects the approved invoice-approval capability.
- Confirm that the planning artifacts remain aligned with the final implementation and that the two separated commits remain distinct and reviewable.
