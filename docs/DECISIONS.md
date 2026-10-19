# Current Decisions

17 October 2026: the user explicitly instructed us to merge all worktrees into master, verify playability and provide a launch command. Authorization applies to this integration; unfinished work owned by others is preserved, and absorbed old implementations do not replace current contracts. Results and limitations are in the [integration report](reports/all-worktrees-integration-2026-10-17.md).

16 October 2026. Later decisions take precedence. [History](DECISIONS-DETAILS.md).

RC: PAUSED, presets. [Seven resources](RARE-RESOURCES-RC.md): no new sources; weights use implemented colony deposits.

[RC](EXPERIMENTAL-RC-ACCEPTANCE.md): integrator `01a13770-e8f0-7df2-8002-e456e71a74f8` combines reviewed slices; the old per-branch gate does not block this. Stable/master/main checkout remain gated on manual acceptance and separate authorization. Do not change 5192; pushing is prohibited. [Handoff](CONTEXT-HANDOFF.md).

Lobby: [M3 with ≥1 player](M3-PLAYER-COUNT.md). Only the host changes settings. Once all are Ready, hostStart generates the world without requiring Ready again. [Import](M3-LOCAL-ASSET-IMPORT.md) and privacy rules remain unchanged.

16 October: disconnecting in the lobby removes the participant; when the host leaves, rights transfer to the next connected player; the host may kick others from the lobby. Reconnect remains available after start: [lobby membership](M3-LOBBY-MEMBERSHIP.md).

## Invariants

- D01–D08, D44: 4.5.2, licensed players, own authoritative ECS server/browser Three.js/WebGL2+DOM; no Unity. Installation is read-only; original/derived assets are not in Git/uploads, and each client reads its own sources.
- D05/D06/D28/D45: verify original scripts/dependencies and scope; do not guess native rules. Reference/numerical owners investigate them.
- D26: filter private data; observer is read-only; preserve outliner/general POV/camera: [perspectives](M3-PLAYER-PERSPECTIVES.md).
- D38/D43: Effective Workforce = ordinary + bonus. Production/job upkeep scale with the effective value; output affects production only. Job/building/district/population upkeep remain separate, without double deduction: [economy](15-workforce-and-job-upkeep.md). Minerals: min 0; shortage does not stop the turn. Zones: building speed/cost.
- D09–D35: target 10–20; [M2 ≥1](M2-PLAYER-COUNT.md); 200–600 stars, configurable ending, no strategic AI; AFK/static/victory/persistence: [details](DECISIONS-DETAILS.md).

## Current Planet Priorities

Startup/authority/civic/ethic swaps: source order/conditions; home class/size from source, without a fixed layout. Day 0 save is not a gate; solar geometry belongs to the Galaxy RC. [Contract](PLANET-BASIC-REFERENCE.md).

District capacity = size + source modifiers, free = capacity − levels; recalculate after effects; distinguish global/type/shared pools. Human rules: before scripts, the engine sets the source-selected `default_starting_district`; ordinary continental gets city, authority gets +2 (level 3). Import effects, not the result. Organic base Workforce/pop = 1, trait separate. A blocker hides a deposit and gives −1 district, including other blockers.
16 October: full 4.5.2 catalog of buildings/zones; filter conditions separately from execution and jobs. Unknown blocks dependent behavior: [contract](PLANET-CONSTRUCTION-RUNTIME.md). Storage +5000 bonuses stack; off removes the bonus.

Prototype: `+/−` change WorkforceLimit by 5% of max, clamped to `0..max`; Shift selects min/max. Ctrl selects a location; WF is not fixed at 1/100: [contract](PLANET-BASIC-ECONOMY.md#граница-потребителя-workforce). Unity/3 science reserves are separate, without caps; research spends `min(stock,R)`, progress is `R+spent`, otherwise output accumulates: [storage](PLANET-BASIC-ECONOMY.md#поведение-и-граница-покрытия).

Authorized: construction, local planet/building/district/zone/feature/job icons, blockers/features, expandable jobs/output, fleet/navigation. Preserve ordinary/hive/machine and Voidfarers/mobile; explicitly exclude conditional branches or use the source base. UI is 16:9; responsive is not a gate; manual acceptance follows parity. Rejected: `8b53c8b`, suppressed economy, C01–C08, silent coverage loss.

Unknown blocks dependent behavior; a disabled catalog does not complete construction. D-IDs: commit/path/S-ID. [Construction](PLANET-CONSTRUCTION-STAGE.md), [fleet](FLEET-NAVIGATION-STAGE.md), [UI](PLANET-UI-CONTRACT.md).

## D45 — Planet Menu and Construction, Decisions of 13–15 October

16 October: port the finished HTML/CSS as a whole; connect only data, assets and commands. [Full plan](PLANET-MENU-INTEGRATION-PLAN.md): all tabs, windows, districts/zones/buildings, catalogs, queue, ownership rights, DTOs/commands, assets and acceptance. The shell is only an intermediate stage. [Consultation](PLANET-MENU-CONSULTATION.md) is complete and [logic questions](PLANET-CONSTRUCTION-LOGIC-QUESTIONS.md) are closed; Arkship layout is deferred.

Lifecycle and upgrades: plan sections 5–9 preserve fixed quote/live speed, pay/reserve, atomic completion, refunds/caps, cancellation on transfer/loss of conditions, N/reorder, enabled inheritance, confirmations and notifications. Later answers take precedence over historical ones: no completion/full-refund notifications; auto-cancel reports reasons without listing excess. [Original answers](DECISIONS-DETAILS.md#human-construction-lifecycle-clarification--13-october-2026); [Aggregate](CONSTRUCTION-AGGREGATE.md), [runtime](PLANET-CONSTRUCTION-RUNTIME.md), [M3](PLANET-BASIC-RUNTIME.md). Native aggregation/rounding, queue-kind sharing/installed additions and source-unresolved scope/removal rules remain dependencies only for the corresponding operations.

## Presets and Work Mode

[Technology](TECHNOLOGY-SYSTEM.md): ordinary pool, per-tech progress, excess LOST, adaptive source weights, tech-gated upgrades. [Numeric effects](TECHNOLOGY-EFFECTS-RC.md) and [LOC](LOCALIZATION.md): one runtime a58; d74 is helpers/evidence, without double effects. Idle reserve is RAW; active source all/area/no-leader −.25 is additive once, not a new human rule. Unknown effects are deferred.

Presets: `trait_pc_continental_preference` → `pc_continental`; this is not native layout. Traits/effects are preserved. Editor — [roadmap](NEXT-PLAYABLE-MILESTONE.md#empire-editor-and-preset-boundary).
16 October: appearance of the three presets — HUM; store the population species snapshot.

Question unanswered for >10 minutes: wait on dependent work; elapsed time is not approval. Waiting mode: one implementation task plus independent review/research. ECON-001 → MOVE-001 → B01 → B02 → PERF-001. [Policy](ARCHITECTURE-WAITING-POLICY.md); RC override is above.

## M3 and Working Rules

M3 fixture, M01–M03/N01 exceptions and local import without persistent cache/no saves apply to M3 only; D27 persistence remains. [Fixture](M3-WORLD-FIXTURE.md), [movement](M3-TIME-AND-MOVEMENT.md), [import](M3-LOCAL-ASSET-IMPORT.md). Sources confirm 7.2 units/day and 15 preparation days; geometry 150/140 and 5-day transit are temporary, not vanilla proof.

Every diff gets independent review. [Policy](EXECUTION-POLICY.md): preserve owners/model/effort; no upgrades/nested delegation/duplicate monitors. RC: clean verified commit, launch/checklist/gaps; stable gate is above. [Language](DECISIONS-DETAILS.md#working-language).

14 October: one integrator, concise state handoffs and proportional checks were established. [Workflow](CONTEXT-WORKFLOW.md), [handoff](CONTEXT-HANDOFF.md). Gameplay scope was not expanded.
