# AI in SDLC Assignment — Evidence and Write-up

## Repository and pull request

- Upstream demo repository: https://github.com/NITIN1670/ai-sdlc-demo-app
- Fork URL: [Shivam-NAGP-AI-SDLC-APP](https://github.com/Shiva-003/ai-sdlc-demo-app)
- Pull request URL: [Role-based invoice approval limits - #1](https://github.com/Shiva-003/ai-sdlc-demo-app/pull/1)
- Branch: `feature/approval-limits`
- Base commit used for the corrected baseline: `66cb3f9`

The feature branch contains the relevant approval-policy commit chain:

- `17d2f2a` docs: add approval policy context and OpenSpec plan
- `a72d498` feat: enforce manager and director solo approval limits
- `0c6e1be` feat: require distinct director co-approval for high-value invoices
- `7b850bf` test: cover over-limit approval rejection in Playwright
- `e829b6e` test: mutation test added
- `74f718b` test: added more playwright tests

## Context engineering

The extended `AGENTS.md` explicitly requires the policy to live in the Domain layer and not in controllers or UI code. This changed the implementation approach from a controller-first check to a domain rule enforced by `ApprovalService`, which is the correct contract for the feature and the later no-mutation validation.

A concrete example is that the approval threshold and two-stage path were implemented in `backend/src/InvoiceApp.Domain/ApprovalService.cs` instead of in the API controller. This preserves the required separation of concerns and keeps the business rules in the place the repository conventions call out.

## OpenSpec

- Change: `add-role-based-approval-limits`
- Relevant artifacts: `openspec/changes/add-role-based-approval-limits/proposal.md`, `design.md`, and tasks/spec files.
- Validation command: `openspec validate add-role-based-approval-limits --strict`
- Validation status in this workspace: Change 'add-role-based-approval-limits' is valid.
- Policy interpretation: the feature uses a two-stage approval path only for amounts strictly greater than `500000`. A Manager may solo approve up to `50000`, a Director may solo approve up to `500000`, and the special high-value flow requires a non-final first approval followed by a separate Director final approval. The same username cannot count as both approvers, and the second approver must be a Director.

## Implementation and testing

- Phase 1 commit: `a72d498` — `feat: enforce manager and director solo approval limits`
- Phase 1 implementation summary: enforced the Manager and Director solo thresholds in the domain service and added the corresponding unit tests for boundary and rejection behavior.
- Phase 2 commit: `0c6e1be` — `feat: require distinct director co-approval for high-value invoices`
- Phase 2 implementation summary: added the above-`500000` two-stage approval path with a non-final first approval and a distinct final Director approval, plus tests for same-user and non-Director rejection.
- Unit-test coverage: `backend/tests/InvoiceApp.Tests/ApprovalServiceTests.cs`
- E2E coverage: `e2e/tests/approval-limits.spec.ts`
- Generated-test review: the tests were reviewed for boundary values, same-user rejection, non-Director second-stage rejection, and no-mutation checks. The review specifically looked for failures in role comparison, `>` vs `>=` thresholds, and the distinct-user requirement, because those are the places AI-generated approval tests are most likely to be wrong.

## Mutation testing

- Tool version: `dotnet-stryker 4.13.0`
- Target: `ApprovalService` and the `InvoiceApp.Domain` project
- Baseline command: `dotnet stryker --test-project ../../tests/InvoiceApp.Tests/InvoiceApp.Tests.csproj` run from `backend/src/InvoiceApp.Domain`
- Baseline result: this was initially missed in the feature workflow; the team later reran the baseline from the starting commit `66cb3f9` to capture a correct before/after record.
- Baseline summary from the rerun: `Killed: 12`, `Survived: 10`, `Timeout: 0`, `final mutation score: 30.77%`.
- Final command: the same `dotnet stryker --test-project ../../tests/InvoiceApp.Tests/InvoiceApp.Tests.csproj` command was run on the feature branch after implementation.
- Final summary: `Killed: 58`, `Survived: 17`, `Timeout: 0`, `final mutation score: 58.00%`.
- Interpretation: the post-implementation run improved the mutation score materially from `30.77%` to `58.00%`, which indicates the approval-policy tests covered more of the domain behavior. The remaining survivors were concentrated in code paths that still lacked explicit coverage, so the evaluation was honest: the improvement is real, but not every surviving mutant is necessarily equivalent to a genuine bug without a direct test of that path.

## AI agents, skills, hooks, and interaction log

- Agents used: `domain-policy-reviewer`, `test-auditor`, and `pr-reviewer`.
- Skill used: `approval-test-review`.
- Concrete skill finding: the test review emphasized that a same-user rejection must be verified by snapshotting the full invoice, approval history, and repository state before and after the rejected approve attempt. That requirement guards against a false-positive test that would pass even if the service mutated the invoice before throwing.
- Hook configuration: `.github/hooks/ai-audit.json` was checked as part of the repo workflow; the interaction log is in `logs/ai-interactions.jsonl`.
- Transcript review: the log file was inspected for secrets before any final claim; no sensitive credentials or tokens were committed in the assignment artifacts.
- Redactions or limitations: no demo video URL or transcript export was available in this repo checkout to cite as an actual verified asset, so it is not included as a claim.

## Pull-request review findings

No confirmed security findings remain in this PR. The review rechecked the implementation against the rules in `AGENTS.md`, the accepted design, and the repository code path.

| ID | Finding | Evidence | Verdict (`fixed`, `dismissed with reason`, or `deferred with reason`) | Reason and validation |
|---|---|---|---|---|
| SEC-REV-01 | Client-controlled roles | [backend/src/InvoiceApp.Api/Controllers/ApprovalsController.cs](backend/src/InvoiceApp.Api/Controllers/ApprovalsController.cs), [backend/src/InvoiceApp.Api/Auth/SessionAuthMiddleware.cs](backend/src/InvoiceApp.Api/Auth/SessionAuthMiddleware.cs), [backend/src/InvoiceApp.Api/Controllers/AuthController.cs](backend/src/InvoiceApp.Api/Controllers/AuthController.cs) | dismissed with reason | The approve endpoint does not accept a client-supplied role; the authenticated session role is used instead. Validation: the service receives the session username and role from the API layer, and the policy remains in the domain service. |
| SEC-REV-02 | Authorization bypass | [backend/src/InvoiceApp.Api/Auth/RequireRoleAttribute.cs](backend/src/InvoiceApp.Api/Auth/RequireRoleAttribute.cs), [backend/src/InvoiceApp.Api/Controllers/ApprovalsController.cs](backend/src/InvoiceApp.Api/Controllers/ApprovalsController.cs) | dismissed with reason | The action is restricted to Manager/Director and is not treated as a general approval endpoint. Validation: the role check happens before the domain call and the policy is checked again in `ApprovalService`. |
| SEC-REV-03 | Same-user double counting | [backend/src/InvoiceApp.Domain/ApprovalService.cs](backend/src/InvoiceApp.Domain/ApprovalService.cs) | dismissed with reason | The domain service rejects a repeated username in the high-value path before writing state. Validation: same-user approval attempts are rejected in unit tests and the implementation compares username values case-insensitively. |
| SEC-REV-04 | Unsafe mutation on rejected attempts | [backend/src/InvoiceApp.Domain/ApprovalService.cs](backend/src/InvoiceApp.Domain/ApprovalService.cs), [backend/src/InvoiceApp.Api/InMemoryInvoiceRepository.cs](backend/src/InvoiceApp.Api/InMemoryInvoiceRepository.cs) | dismissed with reason | The repository update occurs only after validation passes, and the tests also assert the invoice snapshot remains unchanged for rejected attempts. |
| SEC-REV-05 | Race condition / TOCTOU | [backend/src/InvoiceApp.Api/InMemoryInvoiceRepository.cs](backend/src/InvoiceApp.Api/InMemoryInvoiceRepository.cs) | dismissed with reason | This is a single-process demo app with in-memory state; there is no confirmed race or bypass in the reviewed code path. |

## Demo video

- Video URL: not available in this repo checkout or recorded artifact set.
- Duration: not verified.

## Lessons learned

This feature demonstrates why explicit policy context matters: once `AGENTS.md` and the reviewed OpenSpec were loaded, the implementation moved from a generic approval flow to a domain-owned, role-aware approval policy. The no-mutation tests were the critical guardrail for rejecting same-user and non-Director second approvals because they prevented partial state changes on invalid attempts.

A second lesson is that the mutation score is only meaningful when the baseline and final comparisons are recorded correctly. The team initially missed the baseline mutation run and later reran it from the starting commit so the before/after evidence would be trustworthy. That corrected baseline is included above.
