# Vanilla galaxy content coverage inventory — Stellaris 4.5.2

## 1. Snapshot and counting method

Inventory root: `E:/SteamLibrary/steamapps/common/Stellaris` (read-only). The authoritative version marker is `launcher-settings.json`: **Cygnus v4.5.2 (9776)**, `rawVersion=v4.5.2`, mod compatibility `4.5`.

The scan covers every `*.txt` below the four semantic roots `common/`, `events/`, `map/`, and `prescripted_countries/`: **2,261 files**. A deterministic TSV contains `relative-path<TAB>byte-size<TAB>SHA-256` for every file. Its SHA-256 is:

`E0C75A299051E07D1C8C5C076D0E44358F4A650F966BA3499238D76106CC2CC0`

The narrower union of the named generation families below contains **1,638 unique files**; its file-manifest SHA-256 is:

`5A3F1295C4A56B2AFFBD9DB17F03CD398838691D9760667E35F9767C6F5DEB9F`

Counts are lexical, after removing `#` comments while preserving quoted strings. “Records” means assignments at brace depth zero; `@` variables are reported separately. This is reliable for ordinary keyed databases and repeated wrapper records, but it is not a replacement for the engine parser: anonymous setup arrays, nested inline scripts, wrapper record types, parameter substitution and duplicate resolution require typed parsing. Family rows overlap, so they must not be summed.

Generated evidence:

- `work/vanilla_manifest_all_semantic_files.tsv` — complete four-root file ledger;
- `work/vanilla_manifest_files.tsv` — selected family ledger;
- `work/vanilla_manifest_scan.json` — machine-readable counts, DLC archive inventory, gate literals and sampled operator counts;
- `work/scan_vanilla_manifest.py` — read-only scanner.

## 2. Authoritative content roots and counts

### 2.1 Galaxy geometry and setup

| Family | Exact root | Files | Top-level records | `@` variables | Interpretation |
|---|---|---:|---:|---:|---|
| Setup scenarios | `map/setup_scenarios/*.txt` | 6 | 5 | 0 | Five active `setup_scenario` blocks; `static_galaxy_example.txt` is commented documentation. |
| Galaxy shapes | `map/galaxy/*.txt` | 1 | 10 | 0 | `elliptical`, `ring`, spiral 2/3/4/6, `bar`, `starburst`, `cartwheel`, `spoked`. |
| Lobby layout | `common/gamesetup_settings/*.txt` | 1 | 0 | 0 | Anonymous outer arrays; **51** nested `setting=` entries, including `galaxy_size`, `galaxy_shape`, `num_hyperlanes`, gateways and wormholes. This file describes UI exposure, not the numeric generator contract by itself. |
| Defines | `common/defines/*.txt` | 2 | 12 namespaces | 0 | Numeric constants and metadata. Generation must read the resolved values actually used by 4.5.2. |

Dependency: lobby values select a setup scenario and shape; those provide system count, radii, shape parameters, initializer quotas and scaling settings. Native coordinate generation, relaxation, topology, pruning, clusters and partitions consume the resolved numeric record. The content loader must not infer these values from display/localisation strings.

### 2.2 Systems, stars, planets and environmental content

| Family | Exact root | Files | Records | `@` variables | Notes |
|---|---|---:|---:|---:|---|
| Solar-system initializers | `common/solar_system_initializers/**/*.txt` | 40 | 448 | 88 | **360 non-`@` top-level records**. Includes ordinary, home, special, hostile, pre-FTL, DLC/origin and test initializers. |
| Star classes | `common/star_classes/**/*.txt` | 2 | 45 | 0 | Class registry used by system/star creation and class weighting. |
| Planet classes | `common/planet_classes/**/*.txt` | 12 | 78 | 0 | 69 class-like records plus 9 top-level `random_list` aggregators. |
| Deposits and blockers | `common/deposits/**/*.txt` | 26 | 605 | 12 | **593 `d_*` definitions**. There is no separate vanilla `common/blockers` database: blockers are deposit definitions. 134 lexical category assignments select a `deposit_cat_*blocker*` category across 16 files. |
| Deposit categories | `common/deposit_categories/**/*.txt` | 1 | 9 | 0 | Includes resource, blocker, natural-blocker and rare categories. |
| Districts | `common/districts/**/*.txt` | 10 | 177 | 30 | 147 non-`@` top-level definitions; database order matters for `default_starting_district`. |
| Planet modifiers | `common/planet_modifiers/**/*.txt` | 1 | 71 | 0 | Random/explicit planet feature modifiers. |
| Static modifiers | `common/static_modifiers/**/*.txt` | 38 | 3,098 | 3 | Referenced by classes, events, origins and startup effects. |
| Anomalies | `common/anomalies/**/*.txt` | 18 | 340 | 13 | 327 non-`@` top-level records. Requires anomaly potential/weight evaluation and event links. |
| Resource regions | `common/resource_regions/**/*.txt` | 1 | 4 | 0 | Used by the post-topology resource-region assignment stage. |
| Ambient objects | `common/ambient_objects/**/*.txt` | 21 | 197 | 0 | Repeated `ambient_object` wrapper records; referenced by initializer/effect content. |
| Asteroid belts | `common/asteroid_belts/**/*.txt` | 1 | 6 | 0 | Belt visual/type catalog used by initializer descriptors. |
| Dust clouds | `common/dust_clouds/**/*.txt` | 1 | 3 | 0 | Content-side nebula/dust presentation registry. Procedural nebula membership remains a native geometry stage. |
| Storm types | `common/storm_types/**/*.txt` | 1 | 8 | 0 | Referenced by post-generation/event content where enabled. |

Required initializer parser fields/operators include ordered top-level definitions; `usage`; `initializer`; `max_instances`; `spawn_chance`; `scaled_spawn_chance`; `scaled_spawn_settings`; `planet`; `moon`; `change_orbit`; class and size/distance/angle ranges; flags; names; `init_effect`; nested initializer references; and all scripted weights/potentials. Source order is semantic for weighted selection, ties, use counts, descriptor execution and effect RNG.

### 2.3 Bypass, gateways, wormholes, nebulae and other system objects

| Family | Exact root | Files | Records | `@` variables | Role |
|---|---|---:|---:|---:|---|
| Bypass types | `common/bypass/**/*.txt` | 1 | 10 | 0 | Runtime bypass definitions and link behavior. |
| Megastructures | `common/megastructures/**/*.txt` | 30 | 191 | 27 | Includes gateway object definitions and construction/runtime properties. |
| Initializers | `common/solar_system_initializers/**/*.txt` | 40 | 448 | 88 | `abandoned_gateways_01` creates ruined gateways through the generic initializer path. |
| Startup/post-generation event | `events/game_start.txt` | included in 171 event files | — | — | `game_start.31` creates vanilla natural wormhole pairs after the native generator. |
| Nebula content dependencies | setup scenarios, `common/dust_clouds`, random/name catalogs, effects/events | included above | — | — | Scenario fields govern native procedural nebula generation; content supplies presentation and later effects. |

The implementation boundary must therefore include both native geometry stages and the post-generator script engine. Treating gateways, wormholes or nebulae as one undifferentiated native tail would omit vanilla behavior.

### 2.4 Empire and startup catalogs

| Family | Exact root | Files | Records | `@` variables | Notes |
|---|---|---:|---:|---:|---|
| Governments, recursively | `common/governments/**/*.txt` | 27 | 715 | 0 | Includes government forms, authorities, civics, origins and councilors. Counts overlap the next rows. |
| Authorities | `common/governments/authorities/*.txt` | 1 | 8 | 0 | Empire authority branch input. |
| Origins | `common/governments/civics/*origin*.txt` | 2 | **77** | 0 | `00_origins.txt`, `01_origins_non_playable.txt`. |
| Civics | the other four `common/governments/civics/*.txt` files | 4 | **281** | 0 | Ordinary, special, gestalt and corporate civic catalogs. |
| Ethics | `common/ethics/**/*.txt` | 2 | 17 | 0 | Startup branch conditions and country/pop setup. |
| Species archetypes | `common/species_archetypes/**/*.txt` | 1 | 12 | 6 | Species capability/type mapping. |
| Species classes | `common/species_classes/**/*.txt` | 2 | 42 | 0 | Species class and portrait-class behavior. |
| Traits | `common/traits/**/*.txt` | 44 | 1,145 | 19 | Species/leader trait selection and startup effects. |
| Species rights | `common/species_rights/**/*.txt` | 13 | 80 | 0 | Startup population state dependencies. |
| Prescripted empires | `prescripted_countries/**/*.txt` | 20 | 53 | 0 | Country templates referencing authority, civics, origin, ethics, species, names and initializer. |
| Fallen empires | `common/fallen_empires/**/*.txt` | 1 | 6 | 0 | Dedicated generation stage and initializers. |
| Personalities | `common/personalities/**/*.txt` | 5 | 51 | 0 | Generated country metadata dependency. |
| Country types | `common/country_types/**/*.txt` | 9 | 101 | 0 | Scope/type capabilities for created countries. |
| Name lists | `common/name_lists/**/*.txt` | 76 | 80 | 0 | Generated system/planet/fleet/leader names and RNG order. |
| Random names | `common/random_names/**/*.txt` | 7 | 642 | 0 | Repeated typed random-name records; names are generation output and can consume RNG. |

An empire/startup resolver must preserve cross-references among all these registries, validate the selected home-system initializer, and execute origin/civic/authority/species/ethic conditions before and during `empire_init_capital_colony`. Merely loading prescripted countries is insufficient for random empires.

### 2.5 Transitive object catalogs used by startup effects

| Family | Exact root | Files | Top-level records | `@` variables |
|---|---|---:|---:|---:|
| Buildings | `common/buildings/**/*.txt` | 28 | 540 | 42 |
| Pop jobs | `common/pop_jobs/**/*.txt` | 22 | 370 | 5 |
| Pop categories | `common/pop_categories/**/*.txt` | 4 | 24 | 0 |
| Colony types | `common/colony_types/**/*.txt` | 7 | 101 | 0 |
| Zones | `common/zones/**/*.txt` | 7 | 146 | 0 |
| Zone slots | `common/zone_slots/**/*.txt` | 3 | 33 | 0 |
| Technology | `common/technology/**/*.txt` | 35 | 701 | 3 |
| Strategic resources | `common/strategic_resources/**/*.txt` | 1 | 26 | 0 |
| Global ship designs | `common/global_ship_designs/**/*.txt` | 39 | 436 | 0 |
| Ship sizes | `common/ship_sizes/**/*.txt` | 40 | 618 | 299 |
| Component templates | `common/component_templates/**/*.txt` | 55 | 1,776 | 270 |
| Special projects | `common/special_projects/**/*.txt` | 28 | 721 | 1 |

These catalogs are transitive dependencies because startup effects add buildings/districts/zones/resources/technology, create stations/ships/fleets or schedule projects. The safe completeness rule is to parse the complete 2,261-file semantic ledger, then expose typed registries incrementally; hand-picking only the headline galaxy directories will silently break origins and DLC starts.

### 2.6 Script graph

| Family | Exact root | Files | Top-level records | `@` variables |
|---|---|---:|---:|---:|
| Scripted triggers | `common/scripted_triggers/**/*.txt` | 37 | 1,697 | 12 |
| Scripted effects | `common/scripted_effects/**/*.txt` | 43 | 1,711 | 24 |
| Scripted modifiers | `common/scripted_modifiers/**/*.txt` | 2 | 138 | 0 |
| Script values | `common/script_values/**/*.txt` | 15 | 683 | 1 |
| Scripted variables | `common/scripted_variables/**/*.txt` | 23 | 2,908 | 2,908 |
| Inline scripts | `common/inline_scripts/**/*.txt` | 627 | 3,122 | 0 |
| On-actions | `common/on_actions/**/*.txt` | 4 | 489 | 0 |
| Events | `events/**/*.txt` | 171 | 10,380 | 79 |
| Event chains | `common/event_chains/**/*.txt` | 24 | 300 | 0 |
| Precursor civilizations | `common/precursor_civilizations/**/*.txt` | 2 | 9 | 0 |

The event files contain **10,023 top-level event blocks**: 5,245 country, 1,097 carrier, 1,023 ship, 939 fleet, 693 situation, 317 astral-rift, 161 first-contact, 141 generic, 99 espionage-operation, 88 observer, 73 leader, 67 system, 23 colony, 19 starbase, 14 planet, 13 pop-group, 6 agreement, 3 bypass and 2 pop-faction events. There are 201 top-level namespace declarations. The scanner sees 11,472 `id=` occurrences and 10,258 unique literal ID tokens at all depths; only the parsed top-level event object must define identity.

The parser/evaluator surface needed for vanilla generation includes:

1. Clausewitz blocks, anonymous list entries, repeated keys, quoted and bare scalars, comments, `@` substitution, `$PARAMETER$` inline arguments and source-order retention.
2. Typed values: booleans, signed/fixed numbers, ranges, dates/times, database references, localisation/name keys, scope references and saved event targets.
3. Trigger logic: implicit AND, `OR`, `NOR`, `NOT`, `NAND`, `if`/`else_if`/`else`, `trigger_if`, `switch`, `limit`, comparisons, scripted triggers/values and DLC predicates.
4. Scope traversal and iteration over galaxy, country, system, star, planet, moon, pop, leader, fleet, ship, bypass and event-target scopes; ordered/random/every/any variants must preserve their documented iteration/RNG rules.
5. Effects used during creation: flags and variables, class/name/owner changes, deposits/blockers/modifiers, districts/buildings/zones, colony/pop/country creation, fleets/ships/stations/megastructures/bypasses, event scheduling, on-actions and nested scripted/inline effects.
6. Weighted/random operators and script/global RNG streams. A deterministic replacement seed is permitted by the current requirement, but operator order and stream ownership must be stable.

Lexical occurrence counts in the selected-family union illustrate the minimum active surface: `initializer=` 319, `scaled_spawn_chance=` 71, `max_instances=` 144, `planet=` 2,574, `moon=` 657, `change_orbit=` 1,028, `init_effect=` 1,270, `spawn_chance=` 419, `random_system=` 299, `random_list=` 1,352, `random=` 1,411, `while=` 1,634, `every_system=` 52, `add_deposit=` 1,973, `add_blocker=` 101, `add_modifier=` 3,163, `create_colony=` 31, `set_capital=` 46, `fire_on_action=` 91 and `inline_script=` 5,657. These counts come from the machine-readable selected-family ledger, include nested lexical occurrences, and are not a whitelist: aliases, scripted wrappers and effects expressed as block names add further operators.

## 3. Stage/dependency map

| Generation stage | Primary inputs | Required downstream registries/operators |
|---|---|---|
| Resolve lobby/setup | `common/gamesetup_settings`, `map/setup_scenarios`, `map/galaxy`, defines | DLC profile; fixed/range parser; selected scenario/shape record. |
| Create and relax systems | Resolved scenario/shape/defines | Native coordinate/RNG contract, Triangle backend, system materialization. No script catalog needed yet except resolved numeric inputs. |
| Build/prune hyperlanes | Relaxed systems, scenario `max_hyperlane_distance`, lobby `num_hyperlanes` | Candidate graph, stable edge order, connectivity pruning, explicit deterministic RNG. |
| Nebulae, partitions, clusters, resource regions | Scenario/defines, systems/topology, `common/resource_regions`, dust/name content | Native membership/assignment plus scripted potential/weight evaluation for resource regions. |
| Select and execute ordinary/special initializers | `common/solar_system_initializers` | Star/planet classes, ranges, nested initializer references, ambient/belt objects, flags/names, `init_effect`. |
| Materialize planets/moons and random features | Initializer descriptor vectors | Planet/star classes, deposits/categories, districts, modifiers, anomalies, script weights/triggers/effects. |
| Place empires and choose starts | Setup quotas, candidate systems, prescripted/random empire descriptors | Governments, origins, civics, authorities, ethics, species, traits, rights, name lists, home initializers. |
| Capital/startup pipeline | `common/on_actions`, scripted/inline effects, `events/game_start.txt` | Buildings, districts, zones, pops/jobs, technologies, resources, ships/designs/components, origin/civic branches. |
| Fallen/marauder/nomad/precursor/special content | Dedicated catalogs, initializers, events/effects | DLC triggers, scripted eligibility and weights, special projects/event chains. |
| Gateways, wormholes and later scripted features | Initializers, megastructures, bypass, `game_start.31` and other on-actions/events | Script scope selection, distance checks, object creation/link effects, global/script RNG. |

The implementation should build a reference graph before generation. Every reference needs a source location, target registry/type and resolved target. Missing or ambiguous references must be diagnostics; silently substituting `null` would falsely claim vanilla support.

## 4. DLC gates and archive boundary

The installation contains **36 DLC directories**, **35 `*.dlc` descriptors** and **35 ZIP archives**. `dlc005_novel` is the exception: it has ebook files and no `.dlc`/ZIP. The 35 archives total **1,204,651,220 bytes**. Of them, **22 are real non-empty asset archives** and **13 are 22-byte empty ZIPs**. Together they contain **1,494 archive members**, but **zero** `*.txt` members below `common/`, `events/`, `map/` or `prescripted_countries/`. Thus the claim that all DLC ZIPs are 22-byte placeholders is false: for example `dlc040.zip` is 110,709,491 bytes and `dlc014.zip` is 75,958,533 bytes. In this 4.5.2 build, gameplay scripts are loose files in the four semantic roots; the non-empty DLC archives carry assets such as sound, music and graphics.

Exact non-empty archive sizes are:

| Archive | Bytes | Archive | Bytes |
|---|---:|---|---:|
| `dlc012.zip` | 58,984,397 | `dlc014.zip` | 75,958,533 |
| `dlc016.zip` | 48,542,400 | `dlc017.zip` | 37,927,542 |
| `dlc018.zip` | 85,847,809 | `dlc019.zip` | 20,515,245 |
| `dlc020.zip` | 91,727,879 | `dlc021.zip` | 32,698,392 |
| `dlc022.zip` | 25,371,184 | `dlc023.zip` | 13,935,306 |
| `dlc024.zip` | 24,638,015 | `dlc025.zip` | 38,615,404 |
| `dlc026.zip` | 29,767,976 | `dlc027.zip` | 101,425,237 |
| `dlc028.zip` | 21,671,170 | `dlc031.zip` | 47,054,651 |
| `dlc032.zip` | 101,954,918 | `dlc034.zip` | 32,658,292 |
| `dlc036.zip` | 89,766,269 | `dlc037.zip` | 75,472,689 |
| `dlc038.zip` | 39,408,135 | `dlc040.zip` | 110,709,491 |

`work/vanilla_manifest_scan.json` records, per DLC directory, the descriptor path, archive path, archive byte size, member count, semantic-text member count and archive SHA-256. This is the provenance ledger for official DLC assets. The 13 empty archives are dlc001/002/003/004/010/013/015/029/030/033/035/039/042.

Within the selected generation families the machine-readable ledger contains **288** `host_has_dlc=` occurrences and **1** `local_has_dlc=` occurrence, spanning 26 literal product names:

| Gate literal | Occurrences | Gate literal | Occurrences |
|---|---:|---|---:|
| The Machine Age | 43 | Apocalypse | 30 |
| Galactic Paragons | 26 | Plantoids Species Pack | 24 |
| Toxoids Species Pack | 21 | Infernals Species Pack | 20 |
| Megacorp | 17 | Federations | 17 |
| BioGenesis | 17 | Necroids Species Pack | 13 |
| Shadows of the Shroud | 10 | Overlord | 9 |
| Nomads | 9 | Ancient Relics Story Pack | 8 |
| Leviathans Story Pack | 5 | Aquatics Species Pack | 5 |
| Lithoids Species Pack | 3 | Distant Stars Story Pack | 3 |
| Stargazer Species Portrait | 2 | Nemesis | 1 |
| First Contact Story Pack | 1 | Astral Planes | 1 |
| Cosmic Storms | 1 | Grand Archive | 1 |
| Rick The Cube Species Portrait | 1 | Vipra the Vapor Species Portrait | 1 |

The correct model is “load the loose vanilla definitions, then evaluate content gates against an explicit host/local entitlement profile.” Deleting gated definitions at parse time is unsafe because the same definition can contain both gated and ungated branches. DLC names must match descriptor/script literals exactly.

## 5. Load order and merge boundaries

For this unmodded inventory, preserve at minimum `(semantic root, normalized relative path, lexical definition index, identifier)` for every parsed record. Preserve repeated keys and ordered vectors. Do not store a file or database in an unordered map and later reconstruct traversal order.

The exact engine database enumeration, duplicate-ID replacement and mod `replace_path` semantics remain an external content-loader boundary (`M38` in the reverse report). Evidence already shows that database traversal order affects weighted ties and the last `default_starting_district=yes` definition. The scanner also observes repeated top-level keys in several typed/wrapper databases, so “last file alphabetically wins” must not be invented without loader evidence.

For the requested vanilla-only target, this is not a runtime blocker if the implementation checks in a resolved ordered 4.5.2 manifest and validates all references against it. Supporting user mods later requires a separate loader-compatibility layer. The active DLC entitlement vector must be recorded next to the manifest; it is content state, not seed state.

## 6. Localisation boundary

`localisation/**/*.yml` and `locales/` are not included in the semantic file hash. Localisation text does not choose systems, topology, planet classes or initializer branches. Preserve localisation keys as opaque values during generation and resolve them only for display. `common/name_lists` and `common/random_names` are included because selection of a name is generation behavior and can consume RNG; the translated text behind a selected key is presentation-only.

## 7. Known gaps and concrete completion gates

No additional native geometry formula is required to inventory vanilla content. The remaining gaps are content-runtime work and must be reported explicitly:

1. **Typed parser:** anonymous arrays in `gamesetup_settings`, repeated wrapper records, inline parameters, scripted variables/values, ranges and source locations need a real Clausewitz parser. The lexical scanner is evidence and coverage accounting only.
2. **Resolved vanilla load order:** exact engine merge/duplicate behavior is not proven. Freeze an ordered 4.5.2 catalog and reject duplicate ambiguity; do not claim general mod compatibility.
3. **Trigger/effect coverage:** 627 inline-script files, 1,697 scripted triggers, 1,711 scripted effects, 489 on-actions and 10,023 event blocks make a hand-written origin-only interpreter incomplete. Unsupported operators must fail with `file:line`, scope and call stack, not silently no-op.
4. **Reference closure:** all initializer, class, deposit, district, modifier, event, scripted effect/trigger, origin/civic/authority/species, bypass and megastructure references need validation before generation.
5. **DLC profile:** evaluate both host and local gates using explicit entitlements. Loading every branch as active is not equivalent to owning every DLC because gates can be negative or nested.
6. **RNG ownership:** native galaxy RNG, global/script RNG and private weighted substreams must remain distinct. Exact hidden game seed is no longer required, but deterministic repeatability is.
7. **Post-generator scripts:** startup on-actions, `empire_init_capital_colony`, `game_start.31` wormholes and initializer `init_effect` calls are part of vanilla generation completeness.
8. **Output semantics:** visual assets and translated strings may be deferred. Missing gameplay definitions, conditions, links or effects may not be deferred under a “full vanilla content” claim.

A defensible acceptance test loads all 2,261 semantic files, resolves the full typed reference graph under each supported DLC profile, runs every vanilla setup size/shape and every playable origin/home initializer, reports zero unsupported operators on exercised generation paths, and emits a deterministic content/RNG trace. Small geometric or seed-relative differences remain acceptable under the current requirement; skipped definitions and silent effect omissions do not.

## 8. Coverage status baseline

Этот manifest описывает требуемую полноту, а не утверждает, что все перечисленные runtime families уже реализованы. Статусы следует вести раздельно для каждой definition: `parsed`, `linked`, `conditionEvaluable`, `executable`, `verified`. Итоговый family status равен минимальному статусу её definitions; наличие хотя бы одного неизвестного operator запрещает статус `executable` без явно выбранного approximation mode.

Текущий доказанный baseline:

| Stage/family | Reverse contract | Runtime coverage gate |
|---|---|---|
| Setup/shape/seed/coordinates | Закрыт для vanilla contract | Прогнать пять setup scenarios и все selectable shapes из resolved manifest. |
| System materialization | Закрыт bounded safe-success contract | Fresh deterministic manager snapshot; native allocator identity не требуется. |
| Lloyd/Triangle relaxation | Закрыт bounded `native-contract` | `3..2047` sites, finite binary32, documented deviations; bit certification не gate. |
| Topology candidates | Контракт закрыт; следующий implementation stage | Triangle ordinary-only input, annulus/length, stable A/B split и group counters. |
| Hyperlane pruning/materialization | Reverse закрыт в основном addendum §§11/14 | Реализовать после topology candidates с deterministic supplied pruning seeds. |
| Partitions/clusters/regions | Reverse описан | Требуются graph fixtures и resolved resource-region conditions. |
| Initializer selection/execution | Reverse описан для native branches | Требуются typed catalog, use counts, nested neighbor batches и executable script conditions. |
| Planet/moon/orbit construction | Reverse описан | Требуется покрыть все descriptor forms из 40 initializer files. |
| Deposits/districts/modifiers/anomalies | Reverse описан | Требуются typed registries, potentials/weights и post-create mutation order. |
| Empire/startup content | Vanilla branch matrix описана | Требуется interpreter closure для origins/civics/authorities/species/ethics/on-actions. |
| Gateways/wormholes/nebulae | Stage ownership установлен | Generic initializer gateway, `game_start.31` wormholes и native nebula fixtures. |

## 9. Следующий законченный этап: BuildTopologyCandidates

Рекомендуемая граница следующей реализации:

`post-relax ordinary systems → fresh Triangle topology call → strict annulus filter → fixed whole-unit length → stable same/cross-group split → cross-group counters`.

Обязательные условия:

1. `N<2` возвращает пустой результат без Triangle и RNG.
2. `N=2` создаёт прямую симметричную adjacency с неокруглённым integer-root length и полностью обходит pruning.
3. `N>=3` передаёт Triangle только ordinary systems; 256 relaxation dummy sites исключаются.
4. Topology coordinate равна `float32(trunc0(postRelaxRaw/100000))`; при `postRelaxRaw=t×1000` это `float32(trunc0(t/100))`.
5. Backend edge direction/order сохраняются; invalid endpoint является contract violation.
6. Annulus использует original fixed coordinates и строгие положительные границы.
7. Candidate length округляется вниз до целой map unit и снова хранится в fixed scale `100000`.
8. Same-group edge идёт в A. Cross-group edge увеличивает counters обеих групп на 1 и идёт в B. Split стабилен.
9. Для `N>=3` adjacency ещё не материализуется; sorting, density/max-distance pruning и game RNG принадлежат следующему этапу.
10. Preflight/backend failure не оставляет частично изменённые A/B, counters или adjacency.

После этого этапа следующая граница — `PruneAndMaterializeHyperlanes`, уже описанная в §§11/14 основного addendum.

## 10. Snapshot identifiers

- Игра: Stellaris Cygnus `v4.5.2 (9776)`.
- `stellaris.exe` SHA-256: `400DF27C82DDC845AA9DCE79BD468D81F18F060D299CD263E3AFA93AEF4F7A83`.
- Полный ledger 2,261 semantic files: `E0C75A299051E07D1C8C5C076D0E44358F4A650F966BA3499238D76106CC2CC0`.
- Selected generation-family ledger 1,638 files: `5A3F1295C4A56B2AFFBD9DB17F03CD398838691D9760667E35F9767C6F5DEB9F`.
- Узкий handoff `relaxed → M13`: `577AB231319A923008315429770764025171DBE216D01300F56529882DA07897`.

Manifest не включает переводные localisation strings в semantic hash. Name-list/random-name definitions включены, поскольку их выбор является частью генерации и RNG ledger.
