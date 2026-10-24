# Spec Delta

## Purpose

The invoice approval workflow MUST enforce role-based spending limits and prevent approval bypasses so that invoices are approved only within defined authority thresholds and rejected attempts leave the invoice unchanged.

## ADDED Requirements

### Requirement: Manager approval limit
The system MUST allow a Manager to finalize an invoice only when the invoice amount is less than or equal to 50,000.

#### Scenario: Manager approves an invoice within the solo limit
- **WHEN** a Manager submits an approval decision for an invoice amount of 50,000 or less
- **THEN** the invoice MUST be approved without requiring a second approver

#### Scenario: Manager rejects an over-limit invoice
- **WHEN** a Manager attempts to approve an invoice above 50,000
- **THEN** the system MUST reject the request with a clear reason that explains the role limit and MUST leave the invoice unchanged

### Requirement: Director approval limit
The system MUST allow a Director to finalize an invoice only when the invoice amount is less than or equal to 500,000.

#### Scenario: Director approves an invoice within the solo limit
- **WHEN** a Director submits an approval decision for an invoice amount of 500,000 or less
- **THEN** the invoice MUST be approved without requiring a second approver

#### Scenario: Director rejects an over-limit invoice
- **WHEN** a Director attempts to approve an invoice above 500,000 without the required second-stage approval flow
- **THEN** the system MUST reject the request with a clear reason and MUST leave the invoice unchanged

### Requirement: Two-stage approval above 500,000
The system MUST require a two-stage approval flow for any invoice above 500,000, with two distinct approvers and the second approver required to be a Director.

#### Scenario: High-value invoice is approved in two stages
- **WHEN** an invoice above 500,000 is approved by two distinct approvers and the second approver is a Director
- **THEN** the approval MUST be accepted only when the same username is not used in both stages

#### Scenario: Same user is used twice
- **WHEN** the same username is used for both approval stages on the same invoice
- **THEN** the system MUST reject the request with a clear reason stating that distinct approvers are required and MUST leave the invoice unchanged

#### Scenario: Second approver is not a Director
- **WHEN** the second approval stage for an invoice above 500,000 is completed by a non-Director approver
- **THEN** the system MUST reject the request with a clear reason and MUST leave the invoice unchanged

### Requirement: Rejected attempts are explicit and non-mutating
The system MUST provide a clear rejection reason for invalid or over-limit approval attempts and MUST preserve the invoice state exactly as it was before the rejected decision.

#### Scenario: Rejected approval returns a clear reason
- **WHEN** an approval attempt fails because the amount, role, or approver combination is invalid
- **THEN** the response MUST contain a reason that identifies the rule violation

#### Scenario: Rejected approval does not mutate the invoice
- **WHEN** an approval attempt is rejected before finalization
- **THEN** the invoice status, decision metadata, and any other invoice fields MUST remain unchanged
