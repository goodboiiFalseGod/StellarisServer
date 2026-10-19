# Playable effect surface handoff — Stellaris 4.5.2

## 1. Boundary and evidence

This handoff defines the smallest source-backed script interpreter boundary that can turn an ordinary generated galaxy into one playable **generic** empire start. It deliberately separates three claims:

1. `ORDINARY_STRUCTURAL` — ordinary star systems exist, but no empire is playable;
2. `GENERIC_HOME_READY` — a random empire initializer has produced a home candidate and normal home-system resources;
3. `GENERIC_PLAYABLE` — the native startup stage has created the country/colony and executed the capital/start-fleet on-actions.

The exact minimum is profile-dependent. The bounded reference profile used below is `MPS-1`:

- one regular biological, non-gestalt, non-nomadic empire;
- `origin_default`, no civic with a special start branch, no secondary species;
- one of `random_empire_init_01..06`;
- non-Earth, non-ship home carrier, no `special_start_planet` flag;
- normal technology start, no advanced-start and no guaranteed-colony extension;
- all DLC gates fixed by an explicit entitlement profile.

Changing origin, authority, civics, species archetype, Earth/void/ark carrier or low-tech mode changes the reachable closure. It must create a new named profile; it may not silently reuse `MPS-1`.

Snapshot: `E:/SteamLibrary/steamapps/common/Stellaris`, Cygnus 4.5.2 (9776). Primary evidence:

| Source | SHA-256 |
|---|---|
| `common/solar_system_initializers/misc_system_initializers.txt` | `7819CA8C8C9D89153F1E39E76A0E9B75C8CA10DA30D35985DA630872897158F4` |
| `common/solar_system_initializers/empire_initializers.txt` | `B344E38EE6A2C66C1A2C3BB0F90C5E52BE21CF6E8396C00C27EAC8EBE36F269E` |
| `common/scripted_effects/01_start_of_game_effects.txt` | `232925D6D7269532B8E146E60AF4AD2AD33ED60B75AF16888514685A1D6C4CB5` |
| `common/on_actions/00_on_actions.txt` | `7693E64FBE974F37DB0C68969D10F7BDEEC585CE59CA497B5E27ECD69FF8F306` |
| `events/game_start.txt` | `8171B1806103AC96EB82CB5A8ECA2712232764EA7712B1AA16049724268DB528` |
| generated native `effects.log` | `810FA0F8296C25F974B118625E2D97A81608831206FB72C270D2B36952266F3C` |
| generated native `triggers.log` | `8C8D809E0844EF97785D975AFCA2C14B2AA4D0A359A1A08A927A94924FC7FE06` |
| generated native `scopes.log` | `A7EE387C9FE3AB111E985A03F5A75AC61548764E436FEFF8B0C59F44B22EFC34` |

The native documentation logs are under `outputs/sf_custom_maps_35_test/logs/script_documentation` in the installed snapshot. They are used as the operator/scope registry. `work/initializer_schema_scan.json` is used only as a lexical coverage ledger; its flattened keys are **not** treated as an operator list.

Anchor locations in this snapshot are: `basic_init_01` at `misc_system_initializers.txt:5`, `random_empire_init_01` at `empire_initializers.txt:20`, the two home effects at `01_start_of_game_effects.txt:2,5`, capital roots at lines `786,1593,1706,2357`, on-actions at `00_on_actions.txt:4049,4059,4068`, and event IDs at `game_start.txt:1046,1131,1727`.

## 2. Source-backed priority

### P0 — ordinary structural systems

The ordinary no-effect pool is exactly these eleven definitions in `misc_system_initializers.txt`:

`basic_init_01..06`, `asteroid_init_01`, `binary_init_01..02`, `trinary_init_01..02`.

Their descriptor trees contain no `init_effect`. They require the structural initializer surface already specified by the initializer handoff: class dispatch, ranges, orbit accumulation, nested planets/moons, belts, names/flags and post-create random planet content. They do **not** require an effect interpreter. Passing P0 yields `ORDINARY_STRUCTURAL` only.

The full `usage=misc_system_init` bucket is not an ordinary minimum: it contains 185 other definitions with hostile fleets, countries, archaeology, megastructures and DLC content. Enabling that whole bucket immediately promotes the task to the broad vanilla-content surface.

### P1 — generic empire home system

`empire_initializers.txt` contains exactly six `usage=empire_init` entries: `random_empire_init_01..06`. Their direct effect statements have only four keys:

- scripted call `generate_empire_home_planet` — the 4.5.2 definition is intentionally empty;
- effect scope `every_neighbor_system`;
- scripted call `generate_home_system_resources`;
- native mutation `prevent_anomaly`.

The exact transitive runtime surface of `generate_home_system_resources` is:

- effect/control operators: `optimize_memory`, `every_system_planet`, `random_system_planet`, `while`, `random_list`, `if`, `else_if`, `else`;
- mutations: `prevent_anomaly`, `clear_deposits`, `set_planet_flag`, `set_deposit`;
- scope link: `system_star`;
- native predicates/logic: `NOT`, `OR`, `has_planet_flag`, `is_planet_class`, `is_asteroid`, `is_star`, `is_artificial`, `is_colony`, `is_colonizable`;
- scripted-trigger closure: `colonizable_planet -> {habitable_structure, habitable_planet} -> habitable_planet_not_urban`.

Required entry scope is system. `every_system_planet`, `random_system_planet` and `system_star` switch to planet scope. `every_neighbor_system` switches system→system. Random selection must use script/global RNG and retain textual order; it must not consume local galaxy RNG.

This stage clears deposits on non-colonizable bodies, prevents anomalies, assigns star/planet starting energy/mineral/research deposits and flags the selected bodies. The source performs two mineral selections through `while count=2` and three additional random-system-planet selections. A failed random-scope match must have the engine's defined no-target behavior; it cannot select an arbitrary fallback body.

### P2 — minimally playable country

Native startup order is fixed:

1. native creates the default colony/district anchor;
2. on-action `empire_init_add_technologies` → event `game_start.8` in country scope;
3. on-action `empire_init_capital_colony` → event `game_start.12` in colony scope, `from=founder species`, `fromfrom=secondary species or null`;
4. native diagnoses an empty capital pop list;
5. on-action `empire_init_create_ships` → event `game_start.9` in country scope.

For `MPS-1`, `game_start.12` enters the normal carrier branch and calls, in order:

1. `generate_start_deposits_and_blockers`;
2. `generate_start_buildings_and_districts`;
3. `generate_start_pops`;
4. `generate_home_planet_final_pass`.

`game_start.9` then creates the normal science and constructor fleets, their ships, the scientist leader, assigns owner/location/stance and attaches the leader. The final actor requirement is therefore source-backed; a synthetic “playable marker” is insufficient.

### P3 — other vanilla playable start definitions

This is the next boundary, not part of `MPS-1`. The initializer catalog has 9 `usage=custom_empire` definitions and 14 `usage=origin` definitions. Their direct initializer/body effect roots are still bounded:

- custom-empire union (8 keys): `every_neighbor_system`, `generate_home_system_resources`, `prevent_anomaly`, `generate_empire_home_planet`, `set_planet_entity`, `if`, `save_global_event_target_as`, `add_modifier`;
- origin union (24 keys): `set_planet_entity`, `prevent_anomaly`, `generate_home_system_resources`, `every_neighbor_system`, `set_deposit`, `add_deposit`, `create_ambient_object`, `set_planet_flag`, `if`, `clear_deposits`, `save_global_event_target_as`, `generate_empire_home_planet`, `set_carrier_flag`, `add_blocker`, `add_modifier`, `add_district`, `spawn_megastructure`, `last_created_ambient_object`, `random_system_planet`, `clear_planet_modifiers`, `add_planet_devastation`, `remove_planet`, `add_building`, `ruin_building`.

Those are direct keys only. Each scripted call and the selected startup branch must be closed transitively. `last_created_ambient_object` is a scope link, not a mutation operator. Supporting all 23 definitions also requires the exact empire descriptor that chooses Earth, void-dweller, arkship, toxic, shattered-ring and other alternative capital pipelines.

## 3. Operators are not argument fields

The compile surface obtained from the five start roots (`generate_home_system_resources` plus the four capital roots), their inline bundle and events `game_start.8/.9/.12`, intersected with the native effect registry, is **80 native effect operators**:

`add_blocker`, `add_building`, `add_deposit`, `add_district`, `add_modifier`, `add_planet_devastation`, `add_research_option`, `add_resource`, `add_trait`, `begin_event_chain`, `change_colony_foundation_date`, `change_dominant_species`, `change_pc`, `change_planet_size`, `change_species_characteristics`, `change_variable`, `clear_blockers`, `clear_deposits`, `clear_planet_modifiers`, `clear_variable`, `country_event`, `create_colony`, `create_fleet`, `create_leader`, `create_mining_station`, `create_ship`, `create_species`, `destroy_colony`, `else`, `else_if`, `every_deposit`, `every_moon`, `every_owned_planet`, `every_system_planet`, `export_modifier_to_variable`, `fleet_event`, `give_technology`, `if`, `modify_species`, `prevent_anomaly`, `random`, `random_deposit`, `random_galaxy_species`, `random_list`, `random_owned_starbase`, `random_system_planet`, `remove_all_buildings`, `remove_building`, `remove_deposit`, `remove_district`, `remove_planet`, `remove_planet_flag`, `remove_trait`, `save_event_target_as`, `save_global_event_target_as`, `set_built_species`, `set_citizenship_type`, `set_colony_type`, `set_country_flag`, `set_deposit`, `set_fleet_stance`, `set_leader`, `set_living_standard`, `set_location`, `set_name`, `set_owner`, `set_planet_entity`, `set_planet_flag`, `set_planet_size`, `set_population_controls`, `set_species_flag`, `set_species_homeworld`, `set_species_identity`, `set_starbase_building`, `set_starbase_module`, `set_starbase_size`, `set_variable`, `switch`, `validate_planet_buildings_and_districts`, `while`.

Add two initializer-only native operators, `every_neighbor_system` and the bare directive `optimize_memory`. Add the empty scripted call `generate_empire_home_planet`. Thus the bounded compiler must recognize 82 native effect/directive names for the full syntactic slice, even though `MPS-1` executes only a subset.

The corresponding native trigger/control surface is **40 names**:

`AND`, `any_galaxy_species`, `check_modifier_value`, `check_variable`, `else`, `else_if`, `ethos`, `exists`, `gender`, `graphical_culture`, `has_building`, `has_citizenship_type`, `has_civic`, `has_country_flag`, `has_deposit_for`, `has_origin`, `has_planet_flag`, `has_species_flag`, `has_star_flag`, `has_technology`, `has_trait`, `has_valid_civic`, `ideal_planet_class`, `if`, `is_artificial`, `is_asteroid`, `is_blocker`, `is_capital`, `is_deposit_type`, `is_planet_class`, `is_same_species`, `is_sapient`, `is_species_class`, `is_star`, `NOR`, `NOT`, `OR`, `planet_size`, `switch`, `uses_district_set`.

These lists are operator names. The following are argument fields and must never be registered as standalone effects merely because the flat scanner counted them:

| Owning operator | Argument fields/value records in this slice |
|---|---|
| `if`, `else_if`, iterators/random scopes | `limit`; enclosed remainder is effect IR |
| `while` | `count`, optional `limit`; enclosed remainder is effect IR |
| `random_list` | numeric weight keys; each value is an effect block; nested `modifier/factor/add` are weight records |
| `random` | `chance`; enclosed remainder is effect IR |
| `switch` | `trigger`, case labels, `default`; case values are effect/trigger IR according to context |
| `create_fleet` | `effect` callback; the callback runs in new fleet scope |
| `create_ship` | `name`, `design`/`random_existing_design`, `rarity`, `age` |
| `create_leader` | `class`, `sub_type`, `name`, `species`, `randomize_traits`, `traits` and numeric trait slots |
| `create_species`/`modify_species` | `class`, `portrait`, `traits`, `homeworld`, source species/target fields |
| `create_colony` | `owner`, `species`, `colony`; values are references, not nested effects unless documentation marks an effect callback |
| `add_blocker` | `type`, `blocked_deposit` |
| `add_modifier` | `modifier`, `days` |
| `give_technology` | `tech`, `message` |
| `add_resource` | resource IDs and amounts |
| `set_starbase_module` | `slot`, `module` |
| `set_starbase_building` | `slot`, `building` |
| `country_event`/`fleet_event` | `id`, `days`, `random` |
| `change_variable`/`set_variable` | `which`, `value` |
| `export_modifier_to_variable` | modifier name/source, `variable`/`which` |

`name`, `type`, `class`, `species`, `owner`, `target`, `location`, `design`, `building`, `district`, `zone`, `modifier`, `flag`, `id`, `days`, `count`, `chance`, `factor`, `value`, `min`, `max`, `slot`, `module`, `effect`, `limit` and numeric keys are therefore not automatically operators. Their meaning is selected by the owning typed operator schema.

## 4. Scope surface

The compile slice directly uses these documented scope links:

`capital_scope`, `from`, `last_created_leader`, `last_created_species`, `owner`, `owner_main_species`, `owner_species`, `prev`, `root`, `solar_system`, `species`, `system_star`, `target`.

It additionally uses the script grammar forms:

- nullable links `owner?`, `planet?`, `ship?`, `capital_scope?`;
- dotted target chains such as `prev.capital_scope.star` and `prev.capital_star`;
- `event_target:<name>` and saved/global event targets;
- typed “last created” handles required by the executed branch (`last_created_leader`, plus any last-created object used by a selected profile);
- implicit `this`, `prev`, `root`, `from`, `fromfrom` stack preservation across event, iterator, creation callback and scripted-call boundaries.

Minimum object scopes are galaxy, country, system, planet/carrier, colony, species, pop/pop-group, deposit/blocker, fleet, ship, leader and starbase. A colony is not interchangeable with its carrier; `game_start.12` uses colony scope and explicitly enters `planet?`/`ship?` carrier scopes.

Scope validity is checked against `scopes.log`. A known operator invoked on an unsupported scope is fatal, just like an unknown operator. Silent coercion to owner/planet changes branch outcomes and is forbidden.

## 5. Transitive dependencies

### 5.1 Scripted effects

For the normal capital roots alone, the fixed-point closure has **18 scripted effects**:

`add_random_natural_blocker_and_give_tech`, `create_built_robot_species`, `create_global_robot_parent_species`, `create_life_seeded_planet`, `create_ocean_paradise_planet`, `create_wilderness_planet`, `create_zombie_pop_group`, `drone_conversion_new_species_effect`, `generate_civic_secondary_pops`, `generate_home_planet_final_pass`, `generate_home_system_resources`, `generate_start_buildings_and_districts`, `generate_start_deposits_and_blockers`, `generate_start_pops`, `give_starting_resources_effect`, `kill_single_pop`, `make_nascent_starting_pops`, `set_synth_or_robot_portrait_effect`.

Compiling all syntactic branches of events `game_start.8/.9/.12` expands this to **33** effects by adding:

`add_building_if_not_present`, `add_district_and_planet_size_if_needed_effect`, `add_district_and_planet_size_if_needed_effect_amount`, `add_district_if_possible_effect`, `add_random_natural_blocker`, `convert_to_research_system_effect`, `convert_to_resource_system_effect`, `generate_earth_deposits_and_blockers`, `home_arkship_initial_setup`, `increase_habitat_level_effect`, `initialize_arkship_starbase_effect`, `reroll_planet`, `set_earth_entity`, `spawn_orbital_effect`, `void_dweller_home_planet_setup`.

`generate_empire_home_planet` is an additional resolved empty definition. Empty is valid and must not be reported as missing.

### 5.2 Scripted triggers

The five-root/inline/event slice directly references 85 scripted triggers; their fixed-point closure is **107 definitions**. The closure includes the generic eligibility families (`colonizable_planet`, `habitable_planet`, `habitable_planet_not_urban`, `habitable_structure`), authority/archetype tests, ethics, civics, DLC portrait helpers and wrangler job helpers. The exact closure must be recomputed from the ordered content snapshot, not hard-coded by count: any changed definition invalidates the handoff hash and can add edges.

Every dependency edge records caller, callee, source file/line, expected entry scope and parameter bindings. Recursive cycles are legal only if the runtime path has an explicit terminating guard; uncontrolled recursion is fatal.

### 5.3 Inline scripts

`generate_start_buildings_and_districts` directly names 47 inline scripts. `game_start/civics` adds 29 civic scripts. The fixed bundle is therefore **76 inline scripts**. Its sorted 82-file broader startup bundle hash already established by the reverse report is `D55B69B47797134A4AAAD4685E7BE004B80B3245548F7B75269751D5B87CA0EF`.

Inline scripts are parse-time expansion, not runtime effect calls. Expand them before typed compilation, retain source maps and substitute parameters before operator validation. A missing inline file or parameter is fatal even if a later runtime limit would be false, because the source cannot be compiled faithfully.

## 6. Unsupported-as-fatal policy

Every compiled node has one of four explicit states:

- `SUPPORTED_NATIVE(operator, schemaVersion)`;
- `SUPPORTED_SCRIPTED(definitionId, snapshotHash)`;
- `PROFILE_EXCLUDED(proof)` — only for a branch whose limit was evaluated from immutable profile data before its body is linked for execution;
- `UNSUPPORTED_FATAL(name, source, callStack, scope, rawArguments)`.

Unknown assignments never become no-ops. Unknown operator, unknown scripted effect/trigger, missing inline script, invalid argument field, unresolved typed reference, wrong scope, missing target, absent RNG stream or unimplemented selected branch terminates generation before publishing `WorldSnapshot`.

`PROFILE_EXCLUDED` is allowed only in the named bounded prototype profile. The proof records the exact predicate and resolved inputs, for example `owner.has_origin=origin_default`, making the void-dweller branch false. A mutable-state predicate cannot be used to prune compilation unless the state at that program point is known and recorded. Full-vanilla mode forbids profile exclusion and requires all 33 event-closure effects, 107 trigger definitions and 76 inline scripts to compile.

Fatal diagnostics must include at minimum:

`snapshotHash, profileId, initializerId, eventId/onAction, source:file:line, scripted call stack, operator, entry scope, this/root/from/fromfrom targets, raw argument block, RNG ledger position`.

## 7. Acceptance matrix

| Gate | Input/profile | Required observation | Failure condition |
|---|---|---|---|
| A0 catalog | 4.5.2 snapshot | 11/11 ordinary IDs and 6/6 random empire IDs linked in database order | missing/duplicate ID, reordered definitions |
| A1 ordinary | each of 11 ordinary IDs | descriptor tree creates valid stars/bodies; zero initializer effect calls | any effect silently invented/skipped, dangling body/orbit |
| A2 home direct | each `random_empire_init_01..06` | nonempty ordered home candidate list; `generate_empire_home_planet` resolves as empty; neighbors get anomaly prevention | missing home candidate, empty effect treated as error |
| A3 home resources | fixed bodies + fixed script RNG | exact operator trace for `generate_home_system_resources`; same flags/deposits and draw ledger on rerun | local galaxy RNG used, textual order changed, arbitrary random-scope fallback |
| A4 scope | instrumented P1/P2 run | every call's actual scope is accepted by native scope registry; stack restores after nested callbacks | implicit owner/planet coercion, broken `prev/root/from` |
| A5 capital | `MPS-1` | `.8 → .12 → pop diagnostic → .9`; `.12` calls the four capital effects in source order | reordered on-actions, empty pop list, unresolved selected effect |
| A6 economy | `MPS-1` | capital carrier has colony, owner/species, positive pop group, valid capital building/district/zone/resource anchors | decorative home planet without economic state |
| A7 actor | `MPS-1` | normal science and constructor ships/fleets exist; science leader is owned, located and assigned | synthetic actor flag, dangling fleet/ship/leader refs |
| A8 closure | compile profile | 18 normal-root effects closed; 76 inline files expanded; direct and transitive trigger edges recorded | unresolved edge, missing parameter/source map |
| A9 fatality | inject unknown operator/field/wrong scope/missing RNG | generation aborts before snapshot publication with complete diagnostic | warning/no-op/partial world published |
| A10 determinism | same content/profile/seeds twice | identical effect trace, targets, object graph and RNG ledger | unordered registry traversal or mixed RNG domains |
| A11 profile boundary | switch origin/carrier/civic | engine refuses `MPS-1` or resolves a new declared profile and closure | silently executing old minimum under new context |

`GENERIC_PLAYABLE` may be emitted only after A0–A10 pass and `ValidateForPlay` confirms connected graph, unique IDs, no dangling references, one playable country, capital colony/economy and at least one controllable starting fleet/ship.

## 8. Concrete remaining inputs

No algorithmic unknown blocks the `MPS-1` slice. Integration still needs these run-specific inputs:

1. exact empire descriptor: origin, authority, civics, ethics, species archetype/traits, secondary species, nomadic/low-tech flags and ideal class/design;
2. chosen initializer ID or deterministic selection result among `random_empire_init_01..06`;
3. explicit DLC entitlement profile;
4. script/global RNG state or supplied ordered results; it is distinct from galaxy-local and pruning RNG;
5. ordered content snapshot with the hashes above and stable definition order;
6. native-stage outputs before scripts: country, home selection, colony/carrier, default district and `from/fromfrom` species scopes;
7. typed registries referenced by the selected effects: deposits/blockers, buildings, districts, zones, resources, technologies, ship designs, leader classes/traits and starbase modules/buildings.

Without item 1, “minimal” cannot be exact because origins/civics/carrier type choose different effect closures. Without item 4, state can be semantically correct and deterministic under a replacement seed, but cannot repeat a particular native run's random choices. Missing presentation entities/localisation may be stored as opaque references; missing semantic registries are fatal.
