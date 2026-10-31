# Design

## Context

The current implementation in the Domain project has a single approval flow that moves an invoice from `PendingApproval` to `Approved` or `Rejected` based on one caller and a single status field. The API layer already reads the authenticated username and role from session metadata, but there is no role-aware threshold validation or second-stage approval path.

## Goals / Non-Goals

**Goals:**
- Enforce role-based approval thresholds for invoice finalization.
- Keep the approval decision rules inside the Domain project so controllers remain thin.
- Preserve the current lifecycle semantics while adding explicit invalid-decision handling.

**Non-Goals:**
- Introducing a new storage model or repository abstraction.
- Broadly changing the sign-in or session model beyond the role information already available in the request context.
- Reworking unrelated invoice behavior outside approval validation.

## Decisions

- Keep the policy check in `ApprovalService` because the business rule belongs with the invoice lifecycle, not the controller. This keeps the current architecture intact and prevents API-layer policy logic from drifting from the domain rules.
- Model role thresholds as an explicit decision policy: Manager up to 50,000, Director up to 500,000, and a two-stage approval requirement above 500,000. This matches the user-visible approval model without widening scope into unrelated workflow changes.
- Require two distinct usernames for the high-value path and explicitly reject reuse of the same username. This satisfies the distinct-approver invariant without leaving room for accidental self-approval or duplicate-stage bypass.
- Return clear rejection reasons for invalid approval attempts and do not mutate the invoice when a decision is rejected. The current controller base already maps domain exceptions to API statuses, so the rejection path can reuse that contract cleanly.
- Separate the work into two implementation commits: Phase 1 for solo approval limits, then Phase 2 for the higher-value two-stage flow. This keeps the risk manageable and aligns with the requested review/commit boundary.

## Risks / Trade-offs

- [Role names may differ from the demo accounts] → Use the authenticated session role metadata as the source of truth and validate it consistently at the service boundary.
- [Two-stage approval introduces additional state management complexity] → Keep the first stage and second stage as explicit approval checks within the same approval lifecycle instead of broadening the model beyond the invoice approval workflow.
- [Rejected attempts must be non-mutating] → Treat rejection as a validation failure before any decision fields are written, preserving the original invoice state.

## Migration Plan

1. Phase 1: implement solo role thresholds and the xUnit coverage for Manager and Director limits.
2. Phase 2: implement the high-value two-stage approval rule with distinct approvers and Director-required second approval, plus xUnit coverage.
3. Add a Playwright regression around a rejected over-limit approval to confirm the UI/API flow does not mutate invoice state.
4. Review mutation-test baseline and final evidence and confirm the final change review matches the approved scope.

## Open Questions

None at this planning stage; the required boundaries and validation rules are specified in the change request and the repo’s current approval model.
