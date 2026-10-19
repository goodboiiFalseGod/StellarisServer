# Scoped numerical evaluation and empire lifecycle foundation

13 October construction amendment: the human explicitly accepted `duration = base build time / (1 + speed bonus fraction)`, with +100% → /2, +1000% → /11 and +50% → /1.5. This replaces the earlier ambiguous 7.5-year example without establishing native proof, stacking, quantization, live remaining-work conversion or nonpositive denominator semantics. Fixed enqueue price and live active speed remain accepted. See [the construction contract](CONSTRUCTION-AGGREGATE.md#accepted-speed-arithmetic-amendment--13-october-2026); resume only operations whose other dependencies are established.

Status: **SPECIFICATION PROPOSAL, NO IMPLEMENTATION AUTHORIZATION**. User requirements accepted 12 October 2026: multilayer formulas/condition checks, numerical modifiers, all three empire types together and mobile Voidfarers colonies. This companion to [the development plan](NEXT-PLAYABLE-MILESTONE.md) proposes engineering contracts; it does not establish unknown Stellaris native rules. Stable main `caadc6d` remains unchanged.

## Evidence and contract boundaries

Every mechanic contract identifies 4.5.2 definition IDs, source paths/lines, transitive dependencies, accepted project exceptions, unresolved native rules and an acceptance oracle. Import syntax, applicability, arithmetic and scheduling are separate evidence claims. Source ordering alone does not prove native evaluation order. Missing script does not prove a missing mechanic. Unresolved original-game behavior routes to the reference chat and reproducible manual experiment, not a guessed neutral value.

Preserve D38 and [doc15](15-workforce-and-job-upkeep.md): effective units are (ordinary + bonus Workforce) / normalization; production base plus additive base adjustment is scaled before output modifiers; job upkeep has its separate base/upkeep path. Existing contract uses checked decimal arithmetic without intermediate rounding and rejects negative bases/factors atomically. These are established project rules for that supported path, not a global claim about native numeric representation. Preserve D43: job, building/district and population upkeep are distinct sources counted once; output/job-upkeep modifiers do not leak into building upkeep. New formulas require their own documented stage/rounding/cap evidence.

Preserve accepted construction rules: payment at order, immediate slot reservation, cancellation during construction, ownership-change refund to original owner, destruction cancellation and condition-loss refund. Exact refund amount/rounding/storage caps, queue capacity/parallelism and phase timing remain unresolved where not specified. See [construction source audit](reports/construction-next-slice-452.md); ship refit and mobile queues must not inherit an unrelated refund fraction or stationary queue model by convenience.

## Typed scope and evaluation context — proposed engineering contract

An evaluation receives an immutable world revision, content-profile revision, ruleset version and typed scope bindings. Distinguish empire, colony, species/pop group, job, district/zone/building, leader, fleet/ship, starbase and spatial location IDs. Scope transitions declare input/output kinds and permitted owner/location/root/from relationships; no implicit global current scope or string-key service lookup.

Keep root/source/target, previous/from scopes and owner distinct where the reference semantics require them. A compiled expression declares required capabilities, scope kinds, data reads and expected quantity/unit. Reject an invalid scope transition at static validation. A disappeared target, inapplicable capability, false condition, unsupported operator and unresolved rule are distinct outcomes with reason codes; source contracts decide which are valid false/empty cases. Never turn an unknown rule into false, zero or one silently.

Authority, species lifecycle and origin/mode are independent axes. Model capabilities such as organic growth, assembly, happiness participation, job strata/eligibility and mobile residence from supported source definitions. These are examples of contract dimensions, not invented values or a forced shared lifecycle. Validate compatible combinations before starting a game. Individualist machine species, corporate and lithoid combinations are not included automatically by the phrase “three types.”

## Conditions, modifiers and effects — proposed separation

Use extensible typed registries for supported condition, modifier and effect operators. Each operator declares scope signatures, capabilities, dependencies, validation and trace behavior. Establish reusable evaluation/invalidation/transaction contracts first, then implement only the bounded dependency closure needed by a playable slice. A complete Paradox scripting VM is not a prerequisite or an authorized target. New operators enter through source-backed contracts and independent review, without redefining existing semantics.

Conditions are pure evaluations against a specified snapshot. Record nested logical operations, scoped transitions, iteration/set membership and comparisons in a typed representation. Define quantifier/empty-set/short-circuit semantics per supported operator from evidence; traces must distinguish unevaluated branches from false results. Dynamic applicability tracks dependencies of its predicates, not just currently active modifiers.

Modifiers contribute typed quantities with explicit scope, economic category, operation, source, duration and applicability. Each formula declares stages and applicable contributions: base adjustment, category aggregation, multiplicative factors, caps and final quantization only where established. A stable iteration order for reproducibility must not masquerade as native modifier precedence. Conflicting/unknown combinations remain unsupported until their contract is resolved.

Effects propose state changes through commands/events; they do not mutate the world during condition/formula evaluation. Separate pure calculation from validated mutation, including resource transfers, unlocks, entity creation/deletion and timed modifier lifetimes. A compiled content item is supported only when all active required operators and dependencies are supported. Existing explicitly approved latent hooks stay listed as exceptions, never silently executed or counted as complete coverage.

## Formula graph, dependency tracking and invalidation

Represent derived quantities and applicability predicates as a versioned dependency graph. Static validation checks scope/unit compatibility, missing definitions, duplicate/conflicting definitions according to evidenced loader rules and cycles. Dynamic dependencies track relevant collection membership, scope relationships, ownership and location as well as scalar reads. Cache keys include world/content/ruleset revision and typed context; stale cross-owner/cross-POV reuse is forbidden.

Invalidate on edits, population/job assignment, leader assignment, technology/tradition changes, modifier expiry, ownership/location transitions and other declared dependencies. Transitive recomputation follows the graph. A detected runtime cycle yields a visible diagnostic with its dependency path and prevents dependent publication. Do not solve with an arbitrary pass count or fixed-point approximation unless the mechanic has an explicit agreed convergence contract. Correctness tests compare cached and full recomputation results before any performance claim.

## Arithmetic, snapshots and transactions

Each quantity declares unit, supported range, arithmetic policy, rounding stage/direction, caps and overflow handling. Do not infer these globally from convenient implementation types. D38/doc15 remains binding for its path; source research must resolve new native rules or identify an explicit project exception before activation. UI formatting never feeds rounded values back into authoritative calculations.

Commands validate against an identified authoritative revision, compute a candidate delta and publish it atomically with an ACK/result revision. Idempotency prevents duplicate costs/effects on retry. Changes to resources, queue reservations, ownership and entities either commit together or leave the previous state intact with a visible error. Concurrent commands have a documented server ordering; that engineering order is separate from native same-day rules needing evidence.

Monthly settlement evaluates a documented snapshot and prepares all required balances/reports before commit. No partial world update on overflow, unresolved required formula or failed validation; exact failure/retry/pause policy must be specified before implementation. Define whether each mechanic reads pre-phase or post-phase state and how same-day completion, movement, modifier expiry and commands interact. Deterministic scheduling requires explicit phase dependencies, stable iteration/tie handling and reproducible RNG streams/positions; no default daily phase order is accepted here. Seed reproducibility alone does not claim cross-platform or native equivalence.

## Mobile colony contract

A mobile colony has stable identity and economic membership distinct from its current location and carrier. Movement must preserve population, jobs, resources and colony progression identity rather than recreate the colony. Bindings expose carrier/colony/location/owner separately, including transit when applicable; source rules must define whether location-dependent contributions exist during transit.

Location or ownership changes invalidate affected capital-system, territorial, adjacency/sensor and other location-bound predicates/contributions. A ship movement tick and a colony construction/assignment command cannot observe mutually inconsistent revisions. Queue reservations, workforce assignment and timed effects use the same atomic boundary as movement/ownership events. Arrival/destruction/owner changes revalidate applicable dependencies; exact resulting cancellation/refund/population outcomes follow sourced rules and accepted decisions, not an assumed “ordinary planet” lifecycle.

Voidfarers' movement parameters, queue sharing, sovereignty/intel consequences, capital relocation and destruction lifecycle remain source-research gates. Mobile support is accepted scope; unknown details block only dependent implementation. Preserve prior limited Voidfarers content agreements unless the new mobile requirement directly supersedes a boundary.

## Explainability, projections and diagnostics

A formula trace records world/content/ruleset revision, scoped inputs, units, stage values, contributing definition/source IDs, applicability reasons, caps/rounding and final result. Distinguish an absent contribution from a zero contribution. Link consumed effects and rejected commands to authoritative result IDs. Diagnostic verbosity must be bounded and deterministic; full traces need not be sent every tick.

The server owns full traces and authoritative evaluation. Player projections expose only authorized inputs/contributions; labels, reasons, aggregate changes and dependency IDs must not leak foreign economy/intel through explanations. Observer selected POV uses the same filtered player projection while observer identity remains read-only; general visibility follows its agreed policy. Snapshot filtering applies equally to caches, debug routes and error payloads.

Content validation publishes supported/tested IDs, pending dependencies, exclusions and unknown rules with actionable diagnostics (definition, source, operator, scope and dependency path). Reject starting a configuration whose required definitions are unresolved; do not substitute unsupported content or silently omit its required effects. Optional debug facilities require the same authorization as ordinary state.

## Acceptance evidence before implementation is considered complete

- Formula oracles identify source rules and accepted exceptions; cover stacked positive/negative/base/category effects, zero boundaries, expiry, caps/rounding and checked overflow where applicable.
- D38/D43 regressions distinguish production/job upkeep/building or district upkeep/population upkeep and prevent double charging or cross-category leakage.
- Scope/condition tests cover owner/location transitions, collection changes, missing targets, invalid scopes, branch semantics, cycles and unsupported operators.
- Transaction tests cover stale revisions, duplicate/retried commands, reservation/cancellation/ownership changes and failures before commit; no partial state or double refund.
- Snapshot/scheduling tests reproduce the same accepted event sequence and trace, including month boundaries, completions and mobile movement; random-state progression is explicit.
- Lifecycle scenarios cover ordinary individualist, hive and machine intelligence plus supported mobile combinations; inapplicable states are diagnosed rather than borrowed from another type. A Voidfarers colony crosses systems without lost population or double economic settlement, and its location-bound modifiers change at the contracted authoritative boundary.
- Cached/full recomputation agrees; filtered player/observer traces and debug errors reveal no unauthorized data. Real UI demonstrates the supported numerical loop with two players and observer.

These are proposed acceptance categories, not tests already written or passed. Before implementation, topic contracts must resolve operator semantics, new numeric policies, scheduling, lifecycle coverage and trace budgets sufficiently for each bounded slice. Independent review must separate accepted user scope, source evidence and engineering proposals.
