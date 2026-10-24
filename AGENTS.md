# Project conventions for AI agents

Invoice approval demo app: ASP.NET Core 8 Web API with a static HTML/CSS/JavaScript UI and an in-memory repository.

## Architecture and layout

- `backend/src/InvoiceApp.Domain`: business rules and invoice lifecycle. `ApprovalService` owns submit/approve/reject behavior. Keep business rules here; do not implement them in controllers, middleware, or `wwwroot` JavaScript.
- `backend/src/InvoiceApp.Api`: controllers, authentication/session handling, in-memory repository, configuration, and the UI under `wwwroot/`.
- `backend/tests/InvoiceApp.Tests`: xUnit tests. Use the existing `FakeInvoiceRepository`; extend it or add a tracking fake when mutation/persistence assertions are needed.
- `e2e`: Playwright end-to-end tests (`tests/`), configuration, and optional page objects (`pages/`).
- `docs/use-cases`: short existing use-case briefs.
- `openspec/changes/add-role-based-approval-limits`: reviewed OpenSpec proposal, specification, design, and task checklist for this change.
- `.github/agents`: role-specific agents; reviewers must not modify files unless asked.
- `.github/skills`: reusable procedures.
- `.github/hooks`: Copilot lifecycle hooks. Hooks should record auditable events without granting extra tool permissions.

## Approval rules — treat these as contractual behavior

- A Manager may finalize an invoice by themselves only when `Amount <= 50000`.
- A Director may finalize an invoice by themselves only when `Amount <= 500000`.
- An invoice with `Amount > 500000` follows a special two-stage path. The first approval is non-final and must leave the invoice `PendingApproval`; the final/second approval must be from a Director.
- Both approval records must identify distinct usernames. A user must never count as both approvers, even if their role value differs between calls in a unit test.
- Any rejected attempt must return a clear, actionable reason and must not change any invoice field, approval history, decision timestamp, or persisted repository state. Validate all preconditions before mutating state.
- Thresholds are inclusive: exactly 50,000 is within the Manager limit; exactly 500,000 is within the Director solo limit; only amounts strictly greater than 500,000 take the two-stage path.
- For amounts above 500,000, the first approval is a non-final acknowledgement under the special two-person path; an individual approver cannot finalize the invoice alone. This is the explicit interpretation used to reconcile the role caps with the requirement for a second approver.
- Only authenticated `Manager` and `Director` users may call approval endpoints. Do not treat the existing `Approver` or `Admin` demo roles as equivalent to `Director` without an explicit, reviewed policy change.

## Existing conventions to preserve

- Controllers live in `backend/src/InvoiceApp.Api/Controllers/`, use the `api/[controller]` route pattern, and define request-body `record` types in the controller when needed.
- All `/api` endpoints require a valid session except sign-in; the `DELETE` invoice action remains Admin-only. Approval endpoints for this feature must be restricted to Manager/Director.
- Controllers inherit `ApiControllerBase` and use `Handle(...)` so domain failures retain the existing 404, 409, and 400 response mapping.
- `Program.cs` registers services; do not put business logic there. Repository interfaces belong in Domain and the in-memory implementation belongs in Api.
- Existing demo identities are `demo.approver` and `demo.admin` (password `ChangeMe123!`, local demo only). Add explicit Manager/Director identities for this assignment rather than silently equating legacy roles to Director.
- Keep one xUnit test class per class under test, named `<ClassUnderTest>Tests`.

## Coding and test conventions

- Keep business logic in `InvoiceApp.Domain`; controllers pass authenticated username and role to the domain service and map failures using `ApiControllerBase.Handle(...)`.
- Preserve clear error messages. Current `InvalidOperationException` values map to HTTP 409 with an `{ error: ... }` payload; `ArgumentException` maps to 400 and missing invoices to 404.
- Use decimal comparisons directly; do not convert invoice amounts to `double` or silently round amounts before applying limits.
- Every rule needs a positive test, a negative/boundary test as appropriate, and a no-mutation assertion for rejected attempts. Specifically test both same-user rejection and a non-Director second-approver rejection.
- Tests must assert observable behavior, not implementation trivia. For rejected attempts, snapshot the complete invoice and approval history, invoke the attempt, and assert that the snapshot is unchanged. Count repository updates when that proves no persistence occurred.
- Review every AI-generated test. Ask: would this test fail if the role comparison, `>`/`>=` boundary, distinct-user comparison, or second-approver role check were wrong?
- End-to-end tests use Playwright accessible locators (`getByRole`, `getByLabel`, `getByText`) and verify a real rejection in the browser UI. Use `e2e/pages/` page objects when they materially improve reuse. Do not use CSS or XPath selectors.
- Do not change unrelated features or add a database/framework unless the approved spec explicitly calls for it.

## Commands

- Run app: `dotnet run --project backend/src/InvoiceApp.Api`
- Run unit tests: `dotnet test backend/InvoiceApp.sln`
- Build: `dotnet build backend/InvoiceApp.sln --configuration Release`
- Run Playwright: `cd e2e && npm ci && npx playwright install chromium && npm test`
- Run mutation tests from `backend/src/InvoiceApp.Domain`: `dotnet stryker --test-project ../../tests/InvoiceApp.Tests/InvoiceApp.Tests.csproj`
- Validate OpenSpec: `openspec validate add-role-based-approval-limits --strict` (if the installed OpenSpec CLI version uses a different syntax, inspect `openspec validate --help` and use its documented equivalent).

## Agent and review expectations

- Read `AGENTS.md` and the active OpenSpec proposal/spec/design/tasks before coding.
- Implement in two independent feature commits: (1) solo role limits + unit tests, then (2) high-value two-approver path + unit tests. Do not combine both into one implementation prompt or commit.
- Run tests before and after changes; do not claim a command passed unless you ran it and observed the result.
- Have a separate reviewer agent inspect the diff and a test-auditor agent inspect the tests. The implementation agent must not dismiss findings on its own without recording evidence.
- Use stable structured review findings: `ID`, `Severity`, `Finding`, `Evidence`, `Risk`, `Verdict` (`fixed`, `dismissed with reason`, or `deferred with reason`), and `Validation`.
- Never invent test results, mutation scores, PR URLs, video links, or review findings. Record actual output. Do not commit credentials, tokens, private transcript exports, or customer data.
- Keep `logs/ai-interactions.jsonl` as the hook-generated event log. Before committing any captured transcript, inspect it for secrets and remove/redact sensitive content. `ASSIGNMENT.md` must link the actual recorded demo video.

## Never

- Commit real credentials, tokens, private customer data, or an unreviewed transcript containing sensitive content.
- Put approval policy in controllers, the UI, or `Program.cs`.
- Allow a rejected approval attempt to partially update invoice state.
- Mark an OpenSpec task complete or report a test as passing without evidence.
