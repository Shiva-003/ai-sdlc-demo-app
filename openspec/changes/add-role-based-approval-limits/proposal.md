# Proposal

## Why

The invoice approval workflow currently accepts a single approver decision without checking role-based spending limits, so approval authority is not constrained by who is approving or whether an amount requires a second-stage review. This leaves the system exposed to over-limit approvals and makes it difficult to enforce a consistent, role-aware approval policy.

## What Changes

- Add a role-based invoice approval policy for the invoice-approval capability.
- Enforce Manager solo approval up to and including 50,000.
- Enforce Director solo approval up to and including 500,000.
- Require a two-stage approval path above 500,000 with two distinct approvers, where the second approver must be a Director.
- Reject any invalid approval attempt that reuses the same username, does not satisfy the threshold rule, or attempts to bypass the two-stage path.
- Ensure rejected attempts provide a clear reason and leave the invoice unchanged.

## Capabilities

### New Capabilities
- `invoice-approval`: Role-based approval limits and multi-stage approval enforcement for invoice decisions.

### Modified Capabilities
- None.

## Impact

- `ApprovalService` domain logic for invoice lifecycle and decision validation.
- `ApprovalsController` request handling and HTTP error semantics for rejected or invalid approval attempts.
- Demo role metadata and authentication/session context used to identify requestor role and username.
- Unit tests in the domain test suite and Playwright coverage for the approval workflow.
