# Next playable development milestone — proposal

Status: **SCOPE PARTLY ACCEPTED / SPECIFICATION ONLY**. On 12 October 2026 the user excluded combat from this milestone, required all three empire types together plus Voidfarers/mobile colonies, prioritized numerical modifier systems, and requested a sound foundation for multilayer formulas and condition checks. Engineering design and remaining scope choices below are proposals. The user explicitly authorizes no implementation. Stable main `caadc6d` and paused monitoring remain unchanged.

## Goal and preserved decisions

### Empire editor and preset boundary

Human clarification, 13 October 2026: the empire editor remains agreed roadmap scope. Players choose legal supported homeworld preference, authority, species, origin, ethics, civics and traits through validated configuration. The three existing ordinary economic/scientific/military templates are quick-start/testing presets filling that same chosen-configuration model, not exclusive empire options or a replacement for the editor. Original startup scripts derive realized planets/layout/population/resources from the chosen configuration.

The human-selected continental preference applies only to those three presets; it creates no global/editor default or hive/machine/mobile restriction. Clearly distinguish supported implemented fields from planned editor/content coverage. Existing code is not claimed to implement the full common editor/configuration flow. This records scope and boundaries only; it does not authorize full editor implementation, a new configuration framework or scaffolding within the current construction slice. [Accepted preference and clarification](DECISIONS.md#accepted-ordinary-founder-preference--13-october-2026); [current startup contract](CONSTRUCTION-AGGREGATE.md).

Deliver a connected development loop on Stellaris **4.5.2**: configurable lobby → empire creator → initial world → sustained planet/population/economy development → leaders/research/unity/traditions → territorial expansion and starbase/ship construction/refit. Every listed domain is part of the requested target. Deliver successive working slices rather than postponing all integration to a final release. The priority is evidenced numerical mechanics and broad supported content, rather than further visual polish. Numerical mechanics are not assumed trivial; no duration estimate is committed.

Prior content agreements remain requirements. Authoritative content-branch checkpoint `c76dd39` contains `docs/26-templates-and-traditions-selection.md`, `docs/27-voidfarers-and-perks-selection.md` and `docs/28-technology-and-assets-selection.md`, including accepted D46–D54 decisions, templates/traditions, limited Voidfarers/perks and early research selections. These are commit-qualified references because the documents are not yet integrated here and decision numbering overlaps. Verify paths and S-IDs before integration; do not merge decision logs blindly. Existing template/exception decisions are not reset to proposals. Broadening the target requires an explicit decision where it supersedes an existing exclusion or whitelist.

The prior T1 pool plus four targeted T2 technologies remains an initial accepted test profile, not the final cap of the newly requested broader stage. The latest scope supersedes prior hive/machine exclusions and the fixed regular-template-only boundary for this milestone. Voidfarers mobile-colony support is required now and supersedes any earlier stationary-only scheduling boundary. Other accepted content agreements and explicit latent-effect exceptions persist unless specifically superseded.

“Three types” is interpreted as ordinary individualist, hive mind and machine intelligence. Authority and species are separate axes: this does not automatically include every individualist machine/corporate/lithoid combination. Original `common/governments/authorities/00_authorities.txt:1201–1220,1467–1486` distinguishes hive (gestalt, non-MACHINE archetype) and machine intelligence (gestalt, MACHINE); content checkpoint `c76dd39:docs/24-first-playable-content-candidates.md:67` treats nomads as a mode rather than an authority. Voidfarers is an orthogonal origin/mobile-colony mode, not a fourth authority. Exact compatible combinations and supported IDs remain a coverage task.

The requested architecture foundation is specified separately in [scoped numerical evaluation](NUMERICAL-EVALUATION-FOUNDATION.md). Its engineering proposals do not claim recovered native execution order.

Latest accepted addition: celestial bodies with resource deposits and applicable mining/research station extraction are required. This makes earlier accepted space-economy commitments explicit, rather than introducing an unrelated new system. See [celestial deposits and extraction](CELESTIAL-DEPOSITS-AND-EXTRACTION.md) for coverage, discovery/privacy, construction and settlement contracts. Prior Voidfarers harvest/Logistic Ship capability remains separate; ordinary stations/starbases are not automatically granted to nomads.

Previously accepted combat-dependent Voidfarers debris content (`c76dd39:docs/27-voidfarers-and-perks-selection.md`, S13) is deferred with combat for this milestone; do not fabricate active debris rewards. Preserve agreed noncombat effects, with dependency-pending/latent states explicit in the coverage register. This is scheduling under the latest no-combat amendment, not cancellation of the prior content agreement.

## Baseline and separate review

Reuse the verified parsers/manifests, limited M1 jobs/workforce/modifier/economy, M2 session/lobby and M3 map/calendar/movement/permissions/observer/UI infrastructure. Full colony development, research, leaders and shipbuilding are **not already implemented**. Confirm concrete reusable interfaces and limitations from code before task allocation.

Stable M3 manual review and [defect backlog](BACKLOG.md) remain separate from this scope proposal. ECON-001/MOVE-001 are unresolved observations; PERF-001 requests a measurement rather than proving slowdown; MAP-001/CAM-001 await user/native validation. Plan approval must identify which actual review blockers to fix first. Do not silently investigate every deferred item or close acceptance because agent checks passed.

## Proposed dependency slices

| Slice | Required connected behavior | Entry dependencies and review boundary |
|---|---|---|
| A. Inventory and shared contracts | Inventory current code/content and resolve separately authorized review blockers. Specify common effects, triggers and modifiers: scopes, order, provenance, caps, rounding, authority and deterministic seed/settings. Define content coverage states. | Evidence from 4.5.2 source and transitive dependencies; unknown native rules stay pending. Agree contracts before parallel implementation. |
| B. Lobby and empire creation | Real configurable lobby and expandable empire editor; server-validated compatibility/budgets, settings freeze, reconnect and initial-world creation. Seeded galaxy size generation must replace the fixed 16-system fixture for configurable games. | A; approved setting defaults/ranges and supported empire lifecycle. Seed reproducibility does not claim identical native galaxy generation. |
| C. Planetary and mobile-colony development | All three empire lifecycles together; applicable pops/growth/assembly, habitability, housing, amenities, happiness/stability/crime or sourced alternatives; allocation/priorities/workforce/upkeep/output/deficits; districts/zones/buildings and queues. Voidfarers retain colony identity, population/jobs/economy while moving. | A/B; capability and compatibility coverage. Location-dependent effects invalidate on movement; queues and movement share authoritative transactions. Preserve accepted cancellation/ownership decisions; unresolved native formulas block only dependent behavior. |
| D. Progression and leaders | Recruit/assign supported leaders and their agreed lifecycle; research prerequisites/weights/choices/completion/unlocks; unity and tradition trees with applied modifiers. | Reviewed A/C interfaces. Leaders and progression can proceed independently only after shared ownership/effect contracts are fixed. Unlocks must lead to working behavior. |
| E. Territory, extraction and ship infrastructure | Territorial ownership and expansion prerequisites; celestial-body deposits, survey/discovery and applicable mining/research stations; outposts/starbases/modules/shipyards; ship designs/components, build/refit and necessary science/construction/colony ships. | A–D plus source-backed extraction eligibility/coverage. Contract original borders/sensors/fog before implementation; M3 public positions do not authorize public deposits. Voidfarers retain their separately supported harvest path. |
| F. Broad supported content | Expand compatible simple traits/origins/civics, jobs/traditions and technologies by dependency closure. Each included item has functional effects, compatibility checks and tests; unsupported items have exact reasons. | Iterative with B–E, not a content dump after integration. Approved exceptions are tracked separately and retained unless explicitly superseded. |

Each slice includes server authority, filtered projection, usable UI, two-player/observer scenarios and focused tests/source formula oracles. Backend-only completion does not satisfy a slice. Transactional costs/cancellation, command acknowledgment/idempotency, reconnect and private information remain integration criteria.

## Coverage and reference method

Use exact definition IDs and counts with states: supported and tested; approved but dependency pending; excluded by agreed scope; or source/native rule unknown. “Most jobs” or “simple content” is not an acceptance count. An item with an unavailable sole effect must not appear as functioning content. Conditional numerical effects can be cheap only when their complete trigger/scope/effect dependencies already work.

Check original definitions, defines, inline scripts and transitive dependencies before mechanics implementation. Route unanswered questions to reference chat `01a12f66-8bb2-7ee3-aced-76f5d06d2338`; require version, paths/lines or definition IDs, evidence and remaining unknowns. Missing script definitions do not prove absence of native behavior. Add reproducible native-game experiments to manual acceptance where required, without blocking independent tasks or asking the user to invent formulas. Project deviations require explicit agreement. Original Paradox files and derived game assets stay outside Git.

## Ownership and parallel work after approval

One integrator owns shared schemas, authority and integration order. Split planet/economy, progression/leaders, content audit/import, UI and independent QA by explicit contracts and files. Reuse existing project owners; create no new user chats without an explicit need and authorization. Integration includes independent review of delegated implementation.

The user permits Terra/Luna for less responsible work: read-only catalogs, fixtures, isolated straightforward adapters/tests and documentation. Use available models without promising unavailable ones. Scope/order arithmetic, authority/privacy, transactions and final integration require senior review. Model choice does not lower evidence or acceptance requirements.

## Completion criteria

A player can create a valid empire using agreed lobby settings, develop planets, observe population/job changes producing evidenced balances, recruit/assign leaders, complete research, acquire traditions, expand owned territory, discover supported celestial deposits, construct eligible extraction stations and observe their separate settlement, and construct/refit a ship. Two-player isolation, observer general/player views and reconnect remain functional. Coverage publishes exact supported IDs/counts and reasons for exclusions. Tests compare formulas and transactions against identified reference evidence, not merely duplicate implementation.

Every delivered slice records checks and unavailable conditions. Existing M3 visual/native acceptance is not implied by numerical completion. **Combat/war is excluded from this milestone**, preserving later conflict commitments. Diplomacy, NPCs, crises, megastructures, multiplayer launch, saves/replay and advanced Voidfarers features beyond mobile-colony support still require scheduling decisions; prior commitments are not canceled.

## Scope decisions for user review

NP-01–NP-03 are answered; do not ask them again. NP-03 includes conditional numerical content through supported generic triggers. Exact IDs and dependency enumeration are specification work; only genuinely special-system exclusions or project deviations need further decisions. NP-04–NP-08 can be settled before dependent slices without repeating settled whitelist decisions or asking the user for original-game formulas.

| ID | Decision | Dependency |
|---|---|---|
| NP-01 — accepted | No combat for this milestone; peaceful development plus ship build/refit. | Later conflict scope preserved. |
| NP-02 — accepted | All three types together, interpreted as individualist/hive/machine intelligence, plus Voidfarers mobile colonies. | Exact species/authority/origin combinations need source-backed coverage, not another organic-first choice. |
| NP-03 — accepted | Include conditional numerical content through supported generic triggers. Numerical modifier systems and robust multilayer formulas/conditions are required priorities. | Enumerate IDs and dependency closure in specifications; ask only about genuinely special systems/deviations. Retain accepted exceptions. |
| NP-04 | Leader lifecycle now: recruitment/assignment/XP/traits/upkeep/death; include council/agendas/elections immediately or stage them? | D; retain prior accepted leader/agendas decisions. |
| NP-05 | Which lobby settings ship first: proposed 200–600-system target, seed, technology/tradition multipliers, habitables/growth and presets? Agree freeze after start; defaults/ranges require source review and user agreement. | B; do not silently make a proposed range a limit. |
| NP-06 | Which tradition-finisher perks/ascension paths belong now: prior numerical whitelist only, or transformations requiring unique systems? | D/F; retain prior whitelist and exceptions unless explicitly changed. |
| NP-07 | Must durable save/load/recovery and LAN/HTTPS precede review of this milestone, or may interim slices retain session-only mode? | Operational acceptance; existing long-term persistence obligations remain. |
| NP-08 | Is the research target a full weighted vanilla pool/tree, or an expanding fully supported pool closed over implemented unlock dependencies? | D/F; accepted T1 plus four T2 remains the initial test profile, not an implicit final ceiling. |

Continue specifications and source research; record accepted answers separately from engineering proposals. Bounded implementation tasks require a later explicit implementation authorization. Stable main stays untouched.
