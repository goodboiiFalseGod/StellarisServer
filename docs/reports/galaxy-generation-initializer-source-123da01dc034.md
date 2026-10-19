# Solar-system initializer schema and dependency closure — Stellaris 4.5.2

## 1. Scope and evidence

Snapshot: `E:/SteamLibrary/steamapps/common/Stellaris`, **Cygnus v4.5.2 (9776)**. The authoritative source root is:

`common/solar_system_initializers/**/*.txt`

It contains **40 files**, **360 initializer definitions** and 88 top-level `@` variables. The scanner expands neither inline scripts nor scripted effects; it parses structural blocks, preserves repeated keys and recursively inventories nested `planet`/`moon` descriptors. Evidence artifacts:

- `work/initializer_schema_scan.json` — exact field/value-shape counts, scalar value inventories and lexical script-key coverage;
- `work/initializer_reference_check.json` — direct-reference closure checks;
- `work/scan_initializer_schema.py` and `work/check_initializer_refs.py` — read-only scanners;
- `work/vanilla_manifest_report.md` — complete 4.5.2 content-root inventory;
- `outputs/native-generation-reverse-addendum-452.md`, especially §§6, 15, 18 and 22 — native execution order and RNG/range contracts.

The schema counts below are for the raw vanilla source after comment removal. All lists remain ordered; repeated keys are distinct records.

## 2. Top-level initializer record

Every non-`@` top-level assignment is an initializer ID. Its value is a block with these fields in the 4.5.2 snapshot:

| Field | Occurrences | Source shape | Required interpretation |
|---|---:|---|---|
| `planet` | 1,975 | block, repeated | Root planet/star descriptor; may recursively contain planets and moons. |
| `change_orbit` | 571 | 566 scalar, 5 range blocks | Shorthand class `none`: advances orbit accumulator without creating an object. |
| `class` | 355 | scalar | Star class or star random-list reference. |
| `flags` | 309 | value-list block | System/star flags, insertion order retained. |
| `init_effect` | 247 | effect block | Runs after all objects and their effects in the system have completed. |
| `asteroid_belt` | 241 | block, repeated | Belt type and radius. |
| `usage` | 239 | scalar, repeated | Six values occur: `misc_system_init`, `origin`, `custom_empire`, `nomad_init`, `fallen_empire_init`, `empire_init`. |
| `usage_odds` | 200 | 149 blocks, 51 scalar | Scripted weight: base plus modifiers; scalar form must remain valid. |
| `neighbor_system` | 163 | block, repeated | Required/optional initializer-tree edge. |
| `max_instances` | 143 | scalar | Shared use limit; failed spawn rolls also consume an instance natively. |
| `name` | 104 | scalar | Literal/localisation key; blank means generated name. |
| `scaled_spawn_chance` | 71 | scalar | Galaxy-size-scaled chance; takes precedence over `spawn_chance`. |
| `spawn_chance` | 30 | scalar | Percent chance, default 100. |
| `prevent_anomalies` | 22 | scalar bool | Suppress generic anomalies in the system. |
| `namelist` | 15 | scalar | Random-name catalog reference. |
| `primitive_system` | 13 | scalar bool | Pre-FTL setting scaling/category. |
| `mandatory_neighbors` | 5 | scalar bool | Allows a missing required neighbor to create a new system. |
| `inline_script` | 5 | block | Parse-time expansion; all five raw top-level uses are the Grand Archive voidworm initializer template with a `SIZE` argument. |
| `orbital_line` | 4 | block | Standalone system orbital line. |
| `scaled_spawn_settings` | 1 | scalar | Lobby setting token used to scale spawn chance. |
| `inner_radius_offset` / `outer_radius_offset` | 1 each | scalar | Radial placement metadata. |

The 360 definitions are the database entries before usage/DLC/trigger filtering. The source files themselves contain no second definition layer in DLC ZIP archives; DLC conditions are inside loose script and its transitive effects.

### Selection contract

For a usage bucket, evaluate `usage_odds` in database order and exclude only negative weights. A zero-weight entry remains eligible. The selector always consumes one local galaxy RNG draw. Positive total selects by weight; zero total falls back to `draw mod eligibleCount`. Then consume a second local draw for the spawn roll. A rejected spawn increments use count before retry; a successful executor increments it at entry. An empty eligible set is invalid input for a safe implementation.

For the playable prototype, an explicit deterministic RNG is acceptable even if it does not match the game's hidden seed. The database order, number of draws, and separation of local galaxy RNG from script/global RNG must still be stable.

## 3. Planet, moon and nested-planet descriptor

The source contains **2,031 planet descriptors** after recursively counting 1,975 root `planet` blocks plus 56 nested `planet` blocks, and **657 moon descriptors** including 18 nested moons. Nested planets are required for multi-star systems: a star descriptor can contain planets orbiting that star. Treating only `moon` as recursive loses active vanilla systems.

### 3.1 Fields

| Field | Planet occurrences | Moon occurrences | Shape/meaning |
|---|---:|---:|---|
| `class` | 1,839 | 521 | Literal planet class, named random list, or reserved dispatcher mode. Missing/invalid resolves to no object. |
| `count` | 1,014 | 353 | Scalar or `{ min max }`; default 1; signed integer inclusive sampling. |
| `orbit_distance` | 1,843 | 641 | Scalar or range; relative to the current sibling orbit accumulator. |
| `orbit_angle` | 1,803 | 604 | Scalar or range; polar angle relative to the previous object. |
| `size` | 1,337 | 358 | Scalar/range; otherwise class defaults. Integer inclusive sampling. |
| `has_ring` | 994 | 178 | Explicit bool; absent permits class-probability random ring behavior. |
| `has_independent_orbital_line` | 7 | 0 | Preserve orbital line independently of planet lifetime. |
| `name` | 547 | 155 | Name/localisation key; absent uses generated naming. |
| `namelist` | 2 | 0 | Random-name catalog reference. |
| `satellite_naming_policy` | 27 | 0 | `upper_case_roman_numerals` or `lower_case_latin_letters`. |
| `entity` | 62 | 17 | Visual entity override; may be stored as opaque presentation metadata. |
| `flags` | 188 | 76 | Carrier/planet flags. |
| `home_planet` | 27 | 3 | Append successfully created handle to the home candidate list. |
| `starting_planet` | 17 | 6 | Startup/content flag distinct from the native home-candidate list. |
| `modifiers` | 107 | 26 | Every active raw value is `none`: clear/suppress random modifiers according to descriptor flag semantics. |
| `modifier` | 1 | 0 | Explicit planet modifier (`pm_dangerous_wildlife` in the example initializer). |
| `deposit_blockers` | 80 | 17 | Every active value is `none`: convert generated blockers after random deposits. |
| `anomaly` | 18 | 1 | Anomaly category reference or `none`. |
| `init_effect` | 793 | 134 | Effect block, executed after descriptor post-create content. |
| `moon` | 639 | 18 | Recursive moon descriptor. |
| `planet` | 56 | 0 | Recursive planet descriptor, normally under a star in a multi-star system. |
| `change_orbit` | 454 | 3 | Advances the child orbit accumulator without creating an object. |
| `orbital_line` | 9 | 0 | Child standalone orbital line. |

The native parser also recognizes `deposits=none`, but this token has zero active occurrences in the 4.5.2 initializer files. It should remain in the typed schema because the executor has a proven descriptor bit for it and mods may use it; it is not required for raw vanilla coverage.

### 3.2 Class dispatcher

Reserved class modes are:

`none`, `star`, `ideal_planet_class`, `ideal_design_class`, `random`, `random_colonizable`, `random_non_colonizable`, `random_non_ideal`, `random_asteroid`.

Otherwise resolve first as a `common/planet_classes` class ID, then as a named `random_list` wrapper from the same database. The source uses 67 distinct planet-class tokens and 28 moon-class tokens; all resolve against the vanilla registry plus reserved modes. Random lists are wrapper records (`random_list = { name = rl_* planets = {...} }`), so a resolver that indexes only top-level assignment keys will incorrectly report them missing.

`star` chooses the star class associated with the system class. General random modes filter by orbit interval, colonizable/asteroid flags, optional requirements, star-class override weights and the habitable-world multiplier. Preserve planet-class database order for weighted ties. A failed class selection behaves as `none`: the orbit advances, but allocation, size/angle/content draws and child execution are skipped for that instance.

### 3.3 Structural execution order

For each compiled descriptor, in compiled vector order:

1. Consume the integer `count` draw even for a zero span.
2. Per instance, resolve class.
3. Consume `orbit_distance` and update the orbit accumulator before checking class validity.
4. If class is invalid/`none`, stop this instance here.
5. Consume size, allocate and attach the planet/star/moon.
6. If `home_planet=yes`, append its handle immediately to the ordered home list.
7. Consume `orbit_angle`.
8. Execute post-create content in the fixed order below.
9. Execute nested moons and nested planets according to their compiled child vectors; preserve the engine's category/vector order rather than textual interleaving.
10. Execute standalone child orbital lines after object children.

Post-create order for a created body:

1. random modifiers are produced inside the factory before attach;
2. attach object to system or parent body;
3. `modifiers=none` removes random modifiers, otherwise apply explicit modifiers;
4. unless `deposits=none`, run random deposits;
5. `deposit_blockers=none` converts generated blockers to non-blocker swap types;
6. apply explicit/null anomaly policy;
7. resolve ring behavior;
8. apply flags, name and entity metadata;
9. execute body `init_effect` last.

Body `init_effect` receives planet scope; the system/tree scopes must expose the native `this`/`prev`/`root`/`prevprev` relations. A prototype may use different object IDs, but scope identity and parent/child links must be consistent.

## 4. Other structural descriptors

### 4.1 Asteroid belts

There are **241** blocks, each with `type` and `radius`. Six types are referenced and all resolve in `common/asteroid_belts`: rocky, icy, crystal, debris, space-fauna and empty belts. Radius is scalar in 239 blocks and a range in 2. A headless prototype may retain the belt type/radius without rendering it; dropping it loses content state used by presentation and possible later effects.

### 4.2 Orbital lines

There are **13** blocks: 4 system-level and 9 planet-level. Their only field is `orbit_distance_from_parent`. They can be represented as lightweight orbital metadata until rendering exists.

### 4.3 Neighbor systems

There are **163** neighbor descriptors with this exact raw field surface:

| Field | Count | Meaning |
|---|---:|---|
| `initializer` | 163 | Referenced initializer ID; 73 distinct IDs, all resolve among the 360 definitions. |
| `hyperlane_jumps` | 120 | Inclusive allowed hop-count range. |
| `trigger` | 90 | Parent/tree-scoped scripted eligibility. |
| `distance` | 48 | Euclidean distance range. |
| `min_orientation_angle` / `max_orientation_angle` | 4 each | Angular sector for neighbor search/mandatory placement. |
| `spawn_chance` | 2 | Percent gate. |
| `hyperlane_distance` | 1 | Allowed summed hyperlane distance range. |

Execution is two-phase by batch: initialize every system in the current batch, including all object effects and the system `init_effect`; only then search neighbors and form the next batch. Neighbor search is nearest-first under its constraints. With `mandatory_neighbors=yes`, failure creates a new system after up to 15 sampled placements; each attempt consumes distance plus two angle draws, enforces the system buffer, and the fifteenth rejected position is still used with a diagnostic. The new neighbor is linked by one hyperlane. Parent effects therefore finish before child discovery/execution.

The active neighbor-trigger lexical surface is small: seven assignment tokens across 90 blocks (`num_guaranteed_colonies`, `galaxy_size`, `any_neighbor_system`, `has_star_flag`, `exists`, `NOR`, `NOT`). This narrow evaluator is enough for structural neighbor selection in raw 4.5.2, provided it implements the correct system/tree scopes.

## 5. Dependency closure

### 5.1 Direct typed references

The following direct references were checked and have **zero unresolved values** in the vanilla snapshot:

| Source field | Distinct values | Target registry |
|---|---:|---|
| initializer `class` | 32 | `common/star_classes` class/random-list records |
| neighbor `initializer` | 73 | `common/solar_system_initializers` IDs |
| planet `class` | 67 | `common/planet_classes` IDs/random-list names plus reserved modes |
| moon `class` | 28 | same |
| non-`none` planet `anomaly` | 9 | `common/anomalies` IDs |
| moon `anomaly` | 1 | `common/anomalies` ID |
| explicit planet `modifier` | 1 | `common/planet_modifiers`/`common/static_modifiers` |
| asteroid-belt `type` | 6 | `common/asteroid_belts` |
| system `namelist` | 3 | nested catalogs under `common/random_names`/`common/name_lists` |
| planet `namelist` | 1 | same |

Nested random-name catalogs are IDs below wrapper blocks, not necessarily top-level keys. Names and localisation keys are different types: name-list selection is semantic and may consume RNG; translation of the selected key is presentation-only.

### 5.2 Required roots beyond the initializer directory

For structural creation and post-create content, load these roots as one resolved ordered snapshot:

- `common/star_classes`, `common/planet_classes`, `common/asteroid_belts`;
- `common/planet_modifiers`, `common/static_modifiers`;
- `common/deposits`, `common/deposit_categories`, `common/districts`;
- `common/anomalies`, `common/event_chains`, `events`;
- `common/name_lists`, `common/random_names`, localisation keys as opaque display references;
- `common/scripted_triggers`, `common/scripted_effects`, `common/scripted_modifiers`, `common/script_values`, `common/scripted_variables`;
- `common/inline_scripts`, including `solar_system_initializers/*`, `grand_archive/voidworms_system_planet_initializer` and any transitively called startup scripts;
- object catalogs referenced by effects: buildings, zones, districts, jobs, technologies, resources, ambient objects, ships/designs/components, megastructures and bypasses;
- explicit DLC entitlement profile for `host_has_dlc`/`local_has_dlc` branches.

Direct closure is only the first pass. Every scripted effect/trigger and inline expansion must add its own typed edges until a fixed point. Preserve `file:line`, caller, scope type and parameter bindings on every edge.

## 6. Minimal interpreter surface for a playable prototype

“Minimal” here means enough to execute vanilla initializer paths, not merely enough to parse them.

### Layer A — parse and expand

Support comments, quoted/bare scalars, fixed/integer numbers, anonymous/value-list blocks, repeated keys, `@` variables, source order, `$PARAMETER$` substitution and `inline_script={script=... args...}` expansion. Expand before compiling the typed initializer; the five Grand Archive definitions rely on this to produce their planet bodies.

### Layer B — weights and triggers

Implement fixed arithmetic, `base`, `modifier`, `factor`, `add`, comparisons, implicit AND, `OR`/`NOR`/`NOT`, `if` families, DLC gates, scripted triggers/values and scope quantifiers (`any_*`, `every_*`, ordered/random variants as encountered). Raw `usage_odds` blocks contain 65 distinct assignment tokens and neighbor triggers add two more to a 67-token union. Token count includes operands/fields, so `initializer_schema_scan.json` is the exact coverage ledger; it is not an operator registry by itself.

### Layer C — scopes and state

Represent galaxy, country, system, star/body, planet/moon, fleet, ship and bypass scopes; parent/tree scope chain; `this`, `root`, `prev*`; event/global targets; object flags; variables; owners; and collections for neighbor systems and bodies. Implement deterministic traversal order.

### Layer D — creation effects used by initializer `init_effect`

At minimum support conditional/loop/random control flow and the active object mutations: set/remove flags, names, owners and classes; prevent/assign anomalies; clear/add/set deposits and blockers; add/remove modifiers; add buildings/districts/zones/resources/technology; create ambient objects, fleets, ships, stations, countries/colonies, megastructures and bypasses; save/use event targets; call scripted/inline effects; fire/schedule events and on-actions.

Raw effect blocks contain 276 distinct assignment keys at system scope, 386 at planet scope and 157 at moon scope; their union is **529 keys**. Many are argument fields inside effect operators, not independent operators. A truthful implementation should compile these blocks to typed IR and report the exact unsupported operator with source/call stack. Treating unknown keys as no-op is incompatible with full vanilla content support.

### Layer E — random planet content

Load ordered modifier/deposit/anomaly databases, evaluate potentials and weights, preserve separate script/global RNG, then run the proven post-create order. `common/deposits` has 593 definitions and blockers are deposit categories, not a separate database. Resource Abundance affects the null-deposit weight. District min/max repair and blocker/unblocked minimum repair run after selection and require live holder updates between choices.

## 7. Recommended prototype increments and gates

### Increment 1: structural universe

Parse and resolve all 360 initializers; expand all top-level inline scripts; instantiate every structural descriptor; support exact class dispatch, ranges, orbit accumulators, nested planets/moons, belts, flags, names-as-keys and neighbor batches. Store `init_effect` as typed/deferred IR.

Acceptance gates:

- 40/40 files and 360/360 definitions parse;
- counts match: 2,031 planets, 657 moons, 571 top-level changes, 241 belts, 163 neighbors, 13 orbital lines before runtime count expansion;
- zero unresolved direct references in `initializer_reference_check.json`;
- every range has an explicit numeric kind and sampling rule;
- every definition and descriptor retains source order/location;
- unknown structural fields are fatal diagnostics.

This produces a traversable/playable-shaped galaxy, but it is not yet full vanilla content execution because effects and random planet content are deferred.

### Increment 2: ordinary and empire-start systems

Implement usage weights/chances, random modifiers/deposits/blockers/anomalies, core effect control flow and state mutation, home-candidate semantics, and the initializer subset reached by ordinary systems plus every playable origin/home initializer.

Acceptance gates:

- all six usage classes selectable;
- deterministic rerun produces the same initializer/body/reference trace;
- every playable origin resolves its home initializer and returns a usable home body or an explicit source error;
- no unsupported operator appears on exercised ordinary/start paths;
- RNG trace labels local galaxy versus script/global draws.

### Increment 3: complete vanilla initializer execution

Close transitive effects for special, hostile, fallen, pre-FTL, crisis, enclave, nomad and DLC systems, then startup/on-action dependencies.

Acceptance gates:

- every one of the 360 initializer IDs can be invoked under at least one valid context or is classified as intentionally unreachable/test-only with evidence;
- every `init_effect`, `usage_odds`, neighbor trigger and inline expansion compiles;
- all typed references close under the selected DLC profile;
- exercised paths contain zero silent skips and zero unknown operators;
- unsupported presentation assets do not alter semantic state.

## 8. Allowed approximations

These deviations are compatible with the current requirement when recorded in the generation trace:

- use a supplied deterministic seed instead of reproducing the hidden game seed;
- use the accepted native-contract Triangle/topology and tolerate small coordinate/edge differences;
- assign implementation-owned object IDs and allocator metadata;
- keep `entity`, belt visuals and orbital lines as opaque semantic metadata without rendering;
- retain localisation keys instead of translated strings;
- choose statistically equivalent random modifiers/deposits when exact global RNG state is unavailable, while preserving filters, weights and deterministic order;
- use a stable documented ordering for equal candidates where native tie order is unknown.

The following are not acceptable under a full-vanilla claim:

- ignoring top-level or descriptor fields;
- flattening nested planets into root siblings;
- treating `change_orbit` as a body;
- executing neighbor search before parent/system effects finish;
- resolving random-list wrapper names as missing classes;
- silently skipping unknown triggers/effects or initializer references;
- activating all DLC branches regardless of the explicit entitlement profile;
- omitting deposits/blockers/anomalies/naming while claiming content-complete systems.

The practical handoff boundary is clear: structural schema and direct reference closure are complete for the 4.5.2 snapshot. The remaining work is typed inline expansion and interpreter coverage, measured against the exact JSON key ledger and exercised initializer paths rather than against native bit identity.
