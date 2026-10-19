> Historical project instructions preserved for attribution on 13 October 2026. Read AGENTS.md first; later direct instructions override historical wording. This file is not required full-context reading.

Current publication update, 19 October 2026: the user requested that all documentation and commit messages be in English. This supersedes the historical instruction below not to mass-translate existing documents.

# Detailed StellarisServer Working Rules

## Context

This is an independent project inspired by Stellaris mechanics, with its own server and fully browser-based client. Unity and Remanga architecture constraints do not apply.

## Working Language — Historical User Instruction

Effective 12 October 2026, an earlier direct user instruction required English for user-facing replies, inter-chat coordination, reports, new documentation, code comments and commit messages. That instruction superseded earlier Russian-only requirements, including task prompts and inherited directions. It required ordinary spacing and readable sentences, with IDs, paths and test counts clearly separated. Existing game UI localization, source identifiers and historical evidence were to be preserved. It also said not to mass-translate existing documents. Scope, ownership, worktrees, tests and review requirements remained unchanged. Historical source: orchestrator chat `01a12f6c-5841-7f30-8777-09a895819e03`, user turn `01a12ff2-4644-7862-84c0-c363cda2b853`.

## Required Work Order

A later language exception, user turn `01a13002-1ba1-7981-a95b-fcdf51c02a2f`, specified Russian only when addressing the human in the orchestrator chat `01a12f6c-5841-7f30-8777-09a895819e03` and manual-acceptance chat `01a12d73-b37b-7080-943c-684cc080b1f6`. Inter-chat coordination and all project work were to remain in English, including reports, new documentation, comments and commit messages. It also required notifying the user when the verified stable main checkout and test instructions were ready; readiness must not be announced based only on an integration-worktree build.

1. At the start of each task, read `docs/DECISIONS.md`, `docs/OPEN-QUESTIONS.md` and relevant specification sections. Recheck them after context loss.
2. Check Git and existing changes. Preserve work owned by others and do not include it in your changes.
3. Before implementation, document behavior, boundaries, contracts, errors, edge cases and acceptance criteria for the task. The user authorized autonomous implementation of the current M1 economy; work beyond that scope needs a separate request.
4. Ask when a material detail is missing or documents conflict. Do not turn a guess into an accepted decision.
5. Agree with the user on deviations from the target Stellaris version's economy and mechanics, and record them in the decisions document.
6. After implementation, check specification coverage and regressions. Report checks performed, limitations and unverified conditions.
7. Update documentation when an accepted decision changes. Do not present planned features as implemented.

## Reference Checks and Questions

- Before implementing a mechanic, check Stellaris 4.5.2 and its primary sources: original definitions, defines, inline scripts and transitive dependencies. Project code and agent memory do not prove original-game behavior.
- Send mechanics questions to the `Stellaris 4.5.2 Reference — Source-Based Answers` chat (`01a12f66-8bb2-7ee3-aced-76f5d06d2338`). The user authorized project chats to contact it and receive answers.
- Each answer must include the rule, version, source path/lines or ID, evidence and remaining unknowns. If the available code cannot answer precisely and an original-game check is needed, add a reproducible scenario and expected observations to `StellarisServer Prototype — Manual Acceptance` (`01a12d73-b37b-7080-943c-684cc080b1f6`) instead of directly asking the user or requiring an immediate check.
- The absence of a script definition does not prove a native mechanic is absent. Keep unknown rules pending; do not invent defaults. Continue independent, authorized work.

## Git and Tools

### Binding Task Routing — 12 October 2026

Every agent-authored code change, regardless of model or role, requires independent review of its actual diff and contracts before integration and closure. Authors cannot approve their own work, including integrator-authored code. Resolve review findings and review affected changes again; tests do not replace review. Keep review proportional, without redundant reviewer cascades or model/effort upgrades. Record reviewer, outcome and limitations in the existing handoff.

Use the [compact execution policy](docs/EXECUTION-POLICY.md) for authorized work. Route isolated mechanical code fixes, adapters, simple UI or fixtures to an available Luna model at low effort when delegation saves work. Use Terra only if available at a user-permitted tier; never invent a model ID. Keep lead/reviewer model and effort unchanged. Never increase model tier or effort automatically after failure or capacity limits; use a bounded same-tier retry, reduce/split scope or report the blocker. Do not change global settings. Brief junior tasks must name the base commit, owned files, contracts and acceptance; use `fork_turns: none`, no nested delegation, at most two junior implementations concurrently and one integrator. High-risk arithmetic, privacy, transactions and shared schemas remain with existing owners and independent review. Run relevant checks once, broadening only for regressions or new evidence. Avoid delegation overhead for trivial work and routine duplicate monitoring. Record actual model, effort and outcome; do not claim token savings without measurements. This workflow policy grants no milestone implementation authorization.

### Git and Tools — Continued

- Use the project's separate Git repository. Do not modify the Stellaris installation.
- Always use Git. Before making changes, ensure the previous completed stage is committed; commit intermediate stages of large multi-step work. The user lifted the earlier ban on autonomous commits. Do not include others' or unfinished changes automatically; clarify the contents when uncertain.
- Use meaningful commit messages without `codex` or service words such as `feat`/`chore`. Choose branches for the task; do not use `codex` in branch names for this project.
- For autonomous M1 work, the user explicitly authorized builds and automated tests. For later tasks, follow current user instructions.
- Delegate independent research and review when it reduces work. Review delegated implementation results.
- Do not add original Paradox files, binary assets or DLC to Git. Do not copy secrets or local paths into published configuration.
- Use `apply_patch` to edit files. Keep changes within the current task.

## Decision Sources

For M2, the user separately authorized implementation, builds and checks. See contracts doc17–19 and disputed questions in M2-DISPUTES.md. Keep specifications compact: as context grows, create separate topic files with links; keep validation reports separate from requirements.

The user's latest direct decision takes precedence. The decisions document records confirmed agreements. Topic specifications expand them; open questions record unresolved decisions. If documents conflict, update the documentation first, then implement.
