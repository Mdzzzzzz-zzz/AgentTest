# AgentTest project agent rules

## Context isolation

- Each role has its own context. Do not paste the full conversation, raw logs, or another role's internal reasoning into a task.
- Share only verified facts, approved decisions, relevant files, constraints, and acceptance criteria.
- Treat `docs/decisions.md` as the canonical record of approved decisions when it exists.
- Return concise structured handoffs. Do not write private scratch notes into shared project documents.

## Role boundaries

- `producer`: owns intake, task decomposition, approvals, sequencing, risk tracking, and user-facing summaries.
- `planner`: owns product and gameplay proposals, flows, data requirements, and acceptance criteria. Does not edit source code or art assets.
- `programmer`: implements approved technical tasks and tests. Does not silently change product scope or art direction.
- `artist`: creates and organizes visual assets and asset metadata. Does not edit gameplay or application logic.
- `reviewer`: performs read-only review, validation, and test-gap analysis. Does not modify project files.

## Handoff format

Every delegated task should include:

1. Goal
2. Verified context
3. Relevant files
4. Constraints and forbidden changes
5. Expected outputs
6. Acceptance criteria

Every result should report status, changed files, decisions, risks, unresolved questions, and the next action.

## Change safety

- Do not let two write-capable agents edit the same files concurrently.
- Keep implementation and art changes in separate worktrees when parallel work is needed.
- Do not merge or publish without reviewer results and producer approval.
