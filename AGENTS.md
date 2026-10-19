# StellarisServer Working Rules

This project owns its server and browser client and targets Stellaris 4.5.2. It does not use Unity. Write replies, documentation and code comments in English, following current user instructions. Preserve UI localization and historical evidence. Earlier workflow attribution is in `AGENTS-DETAILS.md`; later direct human instructions take precedence.

## Task Entry and Scope

1. Check the working directory, Git branch/HEAD and existing changes. Preserve work owned by others. Read the compact `docs/DECISIONS.md` and `docs/OPEN-QUESTIONS.md`, then only relevant contracts and evidence. Repeat after context loss; do not preload all documents or chat history.
2. Before implementation, specify behavior, contracts, errors, edge cases and acceptance. Current construction/fleet/navigation authorization and rejected approaches are in DECISIONS. A plan is not permission or implementation.
3. Recover mechanics from original 4.5.2 definitions, defines, inline scripts and transitive dependencies. Our code and memory do not prove vanilla behavior. Unknown native rules remain pending; block only dependent operations. Deviations require human agreement.
4. For an experimental RC, require a clean exact commit, launch/checklist and end-to-end QA. Stable/main follows shared manual acceptance and separate authorization. See `docs/EXPERIMENTAL-RC-ACCEPTANCE.md`.
5. Follow `docs/CONTEXT-WORKFLOW.md`: one integrator, concise handoff of exact state and checks proportional to the change.

## Owners, Review and Tools

- Preserve ownership in `docs/M3-WORK-OWNERSHIP.md`. Reference questions route to `01a12f66-8bb2-7ee3-aced-76f5d06d2338`; original-game experiments route to `01a12d73-b37b-7080-943c-684cc080b1f6`, with sources, scenario and unknowns. These routes are authorized; do not repeat full audits.
- Every agent-authored code diff needs independent contract/diff review before integration; authors cannot approve their own work. Resolve findings and review again; tests do not replace review.
- Read `docs/EXECUTION-POLICY.md` before delegation. Suitable minor work uses available Luna at low effort; do not change lead/reviewer settings or automatically upgrade model/effort. No nested junior delegation; at most two junior implementations and one integrator. Waiting mode instead allows one implementation under `docs/ARCHITECTURE-WAITING-POLICY.md`. Avoid trivial dispatch and duplicate monitors.
- Use Git and separate task branches without `codex` in their names. Commit completed stages. Use meaningful messages without `codex`, `feat` or `chore`. Do not automatically include others' or unfinished changes. Do not push remotely unless requested.
- Use `apply_patch` to edit files. Keep the game installation read-only. Never commit or upload original or derived Paradox assets, DLC, screenshots, secrets or machine paths in public configuration. Keep screenshots locally in ignored `content-local/references/`, with tracked links and hashes.
- Record direct current human decisions in compact DECISIONS, then the topic contract. Detailed historical checkpoints never override newer authorization. Keep report evidence separate from requirements.
