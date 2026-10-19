# Installed farming effects and remaining capital closure

12 October 2026. Contract: [complete farming path](../CONSTRUCTION-INSTALLED-FARMING.md). Reference turn `01a131cf-2cc9-7140-b448-b056a37b391c` confirms the entire selected farming installed modifier set and the contrasting mining/capital dependencies. Production extends only the exact rural farming ID and permits its `destroy_trigger` as retained lifecycle metadata, not an executor. No helper profile or startup defaults added.

Real 4.5.2 checks:

| Explicit trusted branch facts | Raw job addition | Raw housing |
|---|---|---|
| Regular, non-gestalt, non-spiritualist FE | farmer 200 | 200 |
| Gestalt | agri_drone 200 | 300 |
| Spiritualist FE | fe_acolyte_farm 200 | 200 |
| Regular + valid Agrarian Idyll | farmer 200 | 600 |
| Same + researched housing technology | farmer 200 | 800 |

Every known-active installed modifier identity is retained; no first-match/alias behavior. Mixed generator/farming snapshots, exact retry, full replacement/removal, unknown civic/technology/Wilderness facts, a supported first object followed by a same-context unresolved farming object, and a supported farming object followed by real unsupported mining/capital all preserve atomicity. No staffing/population, balance/upkeep, calendar or completion-ledger mutation. Gestalt/acolyte tests verify distinct source branches, not authoritative hive/machine startup configuration.

Final focused checks: ConstructionChecks with read-only local 4.5.2 root **37 PASS, 0 FAIL, 1 SKIP** (symlink creation unavailable); economy `Stellaris.Checks` **5 suites, 0 errors**. Construction was rerun after adding same-context later-object rollback cases. No broad UI/browser/QA restart. Existing reviewer/settings unchanged; actual usage unavailable.

Existing independent reviewer approved the actual production/test/contract diff with no actionable findings; no duplicate suites or edits. No licensed source files changed.

## Why complete capital application still refuses

The ordered next implementation is [whole selected shelter consumer](../CONSTRUCTION-SHELTER-EFFECT-CONSUMER.md). Its [first monthly economic checkpoint](construction-shelter-economic-consumer-checks.md) implements real output/Workforce calculations but does not change source assessment or activate either capital.

Current raw installed state consumes housing/job additions and zone-slot additions only. The following mandatory reached source containers cannot be dropped merely because their consumers are not implemented. Both complete capital operations refuse; tests verify no preceding farming object is partially published. These are source-backed implementation dependencies, **not evidence that their rules are unknowable native behavior**.

| Source in 4.5.2 | Remaining effects/conditions or operands |
|---|---|
| `00_capital_buildings.txt:53/87` | Shelter amenities 300; individual-machine founder-species predicate and its roboticist/colonist adjustments. |
| Same `:178` | Capital amenities 1000, branch-office target building addition 1, defense-army addition 4; separate consumers required. |
| `jobs/politician_add.txt:1/16`, `buildings/regular_empire_capital_jobs.txt:22` | Worker-coop, planet flag/origin exclusion, individual-machine founder-species/archetype logic; no inferred ordinary false. |
| `buildings/on_all_capital_buildings.txt:3–108` | Imperial/advanced authority and capital-system identity; jobs output +0.1; military job multipliers `modifier:job_soldier_or_warrior_add`, knight-zone predicate; capital-tier comparisons/previous scope and productivity bonus Workforce. |
| Same `:112–252` | Optional owner/ship/orbit/sector/species scopes, purge/hostility, country modifier/traits/edict/policy flags; growth, augmentor/assembler upkeep, ruler output, happiness, district upkeep/specialist Workforce; `planet.value:count_specialists`, `value:count_any_districts`. |
| Same `:255–326` | Ascension-tier comparisons and maximum districts; corporate government/capital state and `owner.modifier:job_capital_trader_add`; **country** modifiers for commercial pacts and archaeostudies, species-trait count, origin/authority/deposit/DLC predicates. These country containers are not silently covered by the current planet-only assessor. |
| Same `:330–441` | Storm-system/origin/technology/community/DLC facts, base job production/output; `value:storm_building_production_multiplier`; held `has_tradition` versus active tradition, worker-coop predicate and `value:imperious_architecture_scaling_value`. |
| Same `:444–527` | Leader existence/skill and category Workforce; planet/carrier flags, capital tiers, optional owner/origin knight jobs; **species/pop-group** growth container with robotic-species condition. No current pop-group consumer. |

Names above are relative to `common/inline_scripts/` except capital definitions under `common/buildings/`. Actual assessment diagnostics retain origins, unresolved `catalog-number-unresolved`/modifier keys and condition gates. Exact remaining unresolved planet-block starts in the tested explicit ordinary context are `on_all_capital_buildings.txt:3,24,36,112,122,137,150,164,175,196,211,220,228,242,389,402,416,444,472,487,503`, plus the capital-specific rows above. Other branches can be decisively inactive under supplied facts; raw source remains retained. Country/pop-group roots additionally prevent full capital application even though not modeled as planet contributions.

Mining remains refused: `common/districts/02_rural_districts.txt:222–225` declares housing `1 × modifier:mining_district_housing_add` with no potential. Missing-triggered-potential semantics remain pending; unavailable dynamic input is not zero. Farming has explicit potential on every modifier, so this gap does not apply to its complete raw modifier set.

## Live activation boundary

Closed: a trusted complete farming installed snapshot now reaches authoritative candidate raw job/housing state with exact source identities, replacement/removal and rollback. Still required: actual installed identities/layout/startup, effective capacity/modifier composition, job definitions/employment, final infrastructure upkeep (direct farming table is 1 energy, not automatically final payable upkeep), native quote/progress/refund/day-month/lifecycle rules, scheduler/live commands/UI and combined acceptance. No final quote, working order or playable startup claim.

Root independently inspected the actual final production, contract, test and capital-audit diff and integrated `ff90fa5` as `7285dc6`. Integrated real-source ConstructionChecks passed **37 checks, 0 failures, 1 unavailable-symlink skip**; farming assertions extend existing real-content coverage rather than adding a separately counted suite. Capital/mining whole-snapshot refusal remains verified. No browser or broad transport rerun for this narrow raw installed-effect extension. Stable main remains unchanged; this is not a full construction delivery.

The integrated server Release build passed with **0 warnings and 0 errors**. The existing backend owner is prioritizing the next concrete mandatory capital/startup consuming path against the audit above, separating known script implementation gaps from native evidence and unselected startup inputs.
