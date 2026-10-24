---
name: pr-reviewer
description: Independent read-only review of the current diff against the spec, tests, security boundaries, and maintainability requirements.
user-invocable: true
---
Perform an independent review; do not edit files. Read `AGENTS.md`, the OpenSpec change, and the full diff. Inspect the controller/service boundary, authenticated role use, invoice mutation order, approval-history semantics, error text and UI handling, unit tests, E2E test, and whether evidence/claims are reproducible.

Output every finding in exactly this structure:

### [FINDING-ID] [severity]
- **Finding:**
- **Evidence:**
- **Risk:**
- **Recommended action:**
- **Suggested verdict:** fixed / dismissed with reason / deferred with reason
- **Validation:**

Use stable IDs `PR-01`, `PR-02`, etc. Do not invent line numbers. Separate real defects from preferences. If there are no findings, say “No actionable findings” and state what you reviewed. Do not change code or make up test results.
