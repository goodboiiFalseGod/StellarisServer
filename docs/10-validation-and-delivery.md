# Validation and Delivery Stages

The stages below are high-level and do not form a detailed task queue. The sequence after M2 is in the [implementation roadmap](IMPLEMENTATION-ROADMAP.md); graphics and UI are covered by [client delivery](CLIENT-DELIVERY-PLAN.md). M3 boundaries and N01 are agreed, with supporting contracts documented. M4–M10 remain proposals; expanding scope requires a separate decision.

## Definition of Done

Before implementation, requirements and edge cases are documented, dependent open questions are closed, and acceptance criteria and required reference examples are prepared. After implementation, review contract compliance and errors/regressions, report results and update documentation.

If an agent is not permitted to build independently, it must not claim that tests passed. It should give the commands to the user and record only results that were actually obtained. If a build needs to be run, request separate authorization.

## Stage 0 — Research and Contracts

The project target is pinned to 4.5.2. Contracts cover the limited M1/M2 formulas, M3 renderer/environment, local import and test set. Remaining dependencies for later stages include full content/DLC and template selection, final ECS choices, controls and victory. The list below describes research requirements and does not undo completed work.

Acceptance: reproducible manifest, script-operation matrix, economy specification, protocol prototype and measurable load-test plans. Do not begin engine implementation without a separate request.

## Stage 1 — Foundation

Parser for limited content, typed references, ECS and relationships, server calendar, resources, Pop Groups/Workforce, jobs and modifiers. Establish a correct baseline before building a comparable parallel version.

Acceptance: reference calculations are confirmed; unsupported behavior produces diagnostics; a scenario with 20 empires and 600 systems is measured, including the monthly tick. Define planet, group and job counts for load testing in advance.

## Stage 2 — Vertical Prototype

An agreed smaller stage, [M2 — networked economy sandbox](17-m2-network-sandbox.md), precedes the full vertical prototype. M2 does not include the fleets, combat, construction and saves listed below.

Two browser clients, lobby, fixed map, one planet and fleet per player, construction, research, movement, simple combat, AFK and return, stasis, snapshot and recovery. Assets may use a limited agreed set.

Acceptance: both clients complete the scenario; the server rejects invalid commands; visibility is checked in messages; disconnection grants no advantage; recovery matches the baseline.

## Stage 3 — First Playable Game

Map generation, limited empires, exploration, expansion, colonization, economy, research, basic traditions, ship designer and shipyards, fleets, combat, wars, occupation and planet conquest, peace, resource exchange, limited events, observer, authority transfer and agreed victory conditions.

Acceptance: complete a game with a winner, then run a load test with 10–20 clients. Verify the 4–5 hour target in a long session; a short synthetic test does not prove it.

## Regression Suite

- Parser: order, repeated keys, variables and nested calls.
- Scope: valid transitions, event targets and deleted entities.
- Economy: Workforce allocation, production, upkeep, caps and shortages.
- Modifiers: ownership changes and correct invalidation.
- Combat: targets, damage, destruction, retreat and stasis.
- Controls: timeout while paused, return, kick, ban, surrender and authority transfer.
- Network: duplicate commands, delta recovery, slow client and intel filtering.
- Saves: RNG, timers, replay and damaged writes.
- Client: main UI flows, reconnection, combat visuals and surrender.
- Victory: periodic standings and tie boundaries, achievements without duplicate scoring, score-log recovery and final calculation. Detailed cases are in 11-victory-and-scoring.md; disputed rules require agreement before setting test expectations.

## Load Metrics

Measure day and month time at p50/p95/p99/max, CPU, memory, GC pauses, network traffic, snapshot size, recovery time and client FPS. Measure the server without a client and while the host PC also runs a client.

Reference target: Ryzen 5 3600 and 16 GB DDR4. Upper configuration: 600 systems and 20 empires. Maximum ship, colony and Pop Group counts are not yet known.

Playing 50–60 in-game years in 4–5 hours implies a rough average of 2.5–3.75 in-game days per real second on a 360-day calendar. This is a session average, not an approved tick budget; pauses, speed settings and the reference calendar must be considered.

Do not promise stage dates or speedups before initial measurements and a definition of active content scope.
