# Unified Implementation Sequence

Current planning companion: [next playable development milestone](NEXT-PLAYABLE-MILESTONE.md), with [numerical evaluation foundation](NUMERICAL-EVALUATION-FOUNDATION.md). Latest accepted scope excludes combat and requires individualist/hive/machine types together plus mobile Voidfarers colonies. Numerical modifiers and multilayer conditions/formulas are priorities. Engineering design and remaining NP questions are proposals; implementation remains explicitly unauthorized. Earlier hive/machine or stationary-only exclusions are superseded where they conflict; other content agreements remain. Stable main `caadc6d` is unchanged during review; current defect classifications are in [BACKLOG](BACKLOG.md).

Status as of 12 October 2026: implementation of the agreed M3 and available automated checks are complete; manual Chrome/GPU/FPS acceptance is pending. [Results](M3-INTEGRATION-VALIDATION.md), [run instructions](M3-RUN-AND-TEST.md). The M4–M10 queue remains a proposal. User request: implement graphics, UI and core systems as a coherent whole. This plan does not replace topic contracts or expand authorized scope.

## What Exists and What Does Not

M1 provides limited economy and import; M2 provides a server, two clients, observer, lobby, reconnect and economic DOM UI. M3 added in-game GPU rendering, a fixed map, fleets and movement; a full colony, construction, research and combat are still absent. The short M2 user scenario passed; separate upkeep needs visual comparison. Automated tests and reports: doc15, doc21 and M3-INTEGRATION-VALIDATION.

Doc10 defines the general stages; M3-GRAPHICAL-SLICE and the import/time/network/fixture subcontracts define M3 in detail. The full content catalog is not automatically included in M3.

The chat “Уточнить ТЗ по контенту Stellaris” uses a separate worktree, branch `feature/content-catalog`: doc22–25 in that branch describe the catalog, candidates and system selection. In the main checkout, doc22–23 are already used for M2 testing; content references cannot be transferred by document number alone. Before integration, check filenames, links, DECISIONS and OPEN-QUESTIONS; do not overwrite documents or blindly merge an active branch.

## Separation of Responsibilities

- The content plan defines **which** game definitions and mechanics enter the first game; this document defines **in what order** to implement them.
- User confirmations from the content chat count as requirements even while they remain in another worktree. Based on the discussion reviewed, separate first contact, housing/amenities/happiness/stability, basic ground warfare, expanded diplomacy without defensive pact, real political effects without full internal politics, and limited Voidfarers are confirmed. Specific whitelists, traditions/leaders/agendas and some rules are still under discussion.
- Do not revert these decisions to “proposal” status or implement unconfirmed candidates as accepted.
- Each game delivery includes server behavior, network projection, working UI, required graphics and a two-client check. Do not defer the entire client until the server is complete.

## Agreed M3 and Proposed M4–M10 Queue

| Stage | Server and content | Graphics and UI in the same delivery | Exit condition |
|---|---|---|---|
| M2 closure | Compare two fixes and regression results | Visually check separate upkeep, 0/0.5 job slots and stability | No blocking local defects; LAN/stress marked separately |
| M3. Visual game framework | Fixed map; system/body/colony/fleet as linked entities; server calendar and movement; limited ownership projection | Galaxy and system views, camera, selection, one graphics set, HUD, outliner, planet/fleet cards, route and movement | Two players control only their own fleets; view switching, pause, reconnect and observer work; no data leaks |
| M4. Colony development | Authorized resources/jobs/districts/zones/buildings; queues, Workforce, population and colony state; applicable modifiers and shortages | Working colony screen: districts, zones, buildings, population, expense breakdown and queue; real icons from the selected set | Construction charges once, completes over time and changes the economy; colony state checked against references |
| M5. Exploration and expansion | Seed generation, science/construction/colony ships, survey, deposits, sensors, first contact, outposts, stations and colonization | Unknown/explored space, available targets, survey/contact progress, in-system construction, founding a colony | Two clients complete exploration → ownership → extraction → new colony; intel is filtered server-side |
| M6. Progression and shipbuilding | Selected research, unity/traditions/perks and required dependencies; starbase/shipyards, ship designs, components, limits | Research, traditions, starbase, designer, ship queue and fleet composition | Unlock enables a working entity; a new design can be built and has verifiable stats |
| M7. Conflict | Limited space combat, repair/retreat, armies/transports/bombardment, claims, war/exhaustion/peace | Combat models/effects, targets, damage and losses from server events; armies, wars, occupation and peace | Two players complete a war through peace; ownership/occupation differ; combat rendering does not decide outcomes |
| M8. Mobile empire | Accepted Voidfarers: linked Arkship/colony/starbase, mobile economy, survey/harvest/reserves, agreed defeat outcome | Arkship, unified colony/fleet card, mobile queue and reserves, disconnected states | Movement, economy, persistence and reconnect preserve links; no soft-lock or undefined defeat outcome |
| M9. Complete game loop | Remaining accepted diplomacy, monthly deals, event whitelist, points/achievements, end-game setting | Proposals/timers, agreements, events, ranking/result, defeat/observer | Limited game has a verifiable winner; timer expiry and repeated events do not duplicate effects |
| M10. First game at target scale | Balance, generation/load for 10–20 players and 200–600 systems, stability, recovery | Readability, asset loading, FPS/memory, long session | Agreed budgets and a 4–5 hour game are checked, limitations listed |

This is a delivery sequence, not a schedule promise. Features needed before their planned stage are raised as explicit dependencies: for example, basic first contact UI in M5, and leaders required for research before dependent M6 content. Do not add unsupported effects as decorative stubs. M8 does not remove nomads from the first game; their architectural links are prepared in M3/M4.

## Cross-Cutting Work with Fixed Checkpoints

- **Script VM/import:** before each delivery, derive the transitive closure of active content and add only required operations/scopes. Do not treat Parser/AST as a full VM. Effects, RNG, timers and diagnostics are added before the first game definition that uses them. M3 needs only spatial definitions; M4 needs economic conditions/modifiers; M6/M9 need corresponding unlocks/effects/events.
- **Saves:** in M3, fix IDs, versions and phase boundaries; for M4, the proposal is to implement and check snapshot/log/recovery before long tests. In later stages, every new state type must pass save/load/replay. The “restart destroys the world” exception is explicitly agreed for M2 and M3; target saves in D27 are not canceled, and M4 scope still needs agreement.
- **Empire modes:** AFK/reconnect must not regress from M3. Kicks/bans/defeat and economic inactivity are introduced in M4 after rules are clarified; return fire and retreat of an inactive empire are required in M7.
- **Network:** authority, idempotency, ownership and bounded queues are always preserved. In M3, measure projection size and need for deltas; do not rewrite transport just to check a box. Full intel is introduced before M5 exploration. Check LAN before claiming multi-computer readiness, TLS before any internet deployment.
- **Performance:** M3 FPS/import budgets are agreed in M3-GRAPHICAL-SLICE; measurements are still pending. RAM/VRAM, network and server tick thresholds remain open. Measure server, network and client when adding entities.

## Work Sequence Without Skipping Steps

1. M3 follows the separate agreed contracts below; M4–M10 remain proposals. Before each coding step, settle its exact inputs, state, commands, errors and criteria without reopening accepted decisions.
2. Within the active stage, tasks follow IDs and dependencies. Independent server/client/content tasks with agreed contracts may run concurrently; do not develop the next stage covertly.
3. For every task: goal → inputs/state → rules/errors → network contract → UI/graphics → automated and manual tests → commit. If there is no contract, write the document before code.
4. Blocking regressions take priority. New requests go to the backlog; changing the order requires the reason, affected dependencies and user decision.
5. Stage exit requires review, rerunning prior tests, a two-client scenario, a report of implemented/limited/unchecked items, and user manual verification. A roadmap row is not complete merely because code exists.

## Questions Before Starting M3

1. M3 source and access are agreed: each participant's own installation, browser folder picker, exact 4.5.2 profile/required files, reject mismatches, no persistent cache. Contract: M3-LOCAL-ASSET-IMPORT; full import/LAN remain out of scope. Source and derived assets stay outside Git.
2. Selected stack is Three.js 0.186.1/WebGL2 + DOM, with 3D galaxy and system. Test set of corvette/station/star/continental planet/icons is agreed; exact dependencies are in M3-ASSET-MANIFEST. Visual gate is not complete; complex combat effects are planned for M7.
3. M3 budgets and environment are agreed in M3-GRAPHICAL-SLICE: current PC, Chrome, 1080p. These are not minimum requirements for the final game and do not confirm support only in Chrome.
4. The content chat is completing the near-term selection and recording leader/agenda dependencies and Arkship rules. The entire catalog need not be closed before M3; only decisions that block its architecture and visual set.

Details of the M3 graphics delivery: [client plan](CLIENT-DELIVERY-PLAN.md). General requirements: doc03–09. The specific content list remains in the content branch documents until verified integration.

M3 contracts: [import](M3-LOCAL-ASSET-IMPORT.md), [graphics slice](M3-GRAPHICAL-SLICE.md). Main boundaries are agreed: no persistent cache, movement within systems and exact days, no saves yet. Manifest, time/movement, spatial protocol and world fixture are described in M3-ASSET-MANIFEST, M3-TIME-AND-MOVEMENT, M3-SPATIAL-PROTOCOL and M3-WORLD-FIXTURE. N01 is closed; [remaining questions and checks](M3-DISPUTES.md) must close before dependent tasks. M4–M10 remain proposals.

## Next Boundary After M3

Doc10 also calls for construction, research, simple combat, inactivity and recovery in the vertical prototype. These goals are agreed at a general level, but do not form a ready numeric/content contract for the next delivery. M3 does not complete the full vertical prototype.

Proposed next slice is colony development with a construction queue and working UI, followed by exploration/expansion and progression/shipbuilding as shown above. Before implementation, confirm M4 scope and reconcile content-chat decisions without reopening already accepted mechanics:

- S18 was accepted in content commit `afe3458`; the first-game whitelist does not need reapproval. Select the nearest implementable slice within it and check its transitive dependencies rather than treating the whole set as implemented.
- Queue order/slots, charge/refund timing on cancellation, replacement/demolition/upgrade, completion effects and reference durations/modifiers from 4.5.2.
- Scope of Pop Groups/growth/migration, housing/amenities/happiness/stability and shortages in this delivery; agreeing on the final set does not authorize inventing temporary formulas.
- Whether M4 includes saves/log/recovery and which corruption scenarios to check; the M3 exception does not automatically carry forward.
- If kick/ban/defeat are introduced: fate of queues and assets during inactivity (Q07/Q13), without unilaterally defining tactical combat rules.

Victory policy, full combat/retreat and high load do not block a limited colony before a corresponding dependency exists. Proposals from another chat are not contracts merely because a file exists.

Next independent comparison: [4.5.2 construction, evidence and minimal clarifications](reports/construction-next-slice-452.md). The research does not set payment/refund/queue defaults or authorize implementing unknown rules.

Independent preparation without construction runtime: [draft contract for read-only definition import](CONSTRUCTION-DEFINITION-IMPORT.md). It separates raw data/branches/diagnostics from final prices and CanBuild; it does not yet approve M4 or imply implementation.
