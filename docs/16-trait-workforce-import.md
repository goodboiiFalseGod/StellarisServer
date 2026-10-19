# Importing bonus Workforce from traits

October 12, 2026. User request: define the contract first, then implement and verify it against real content.

## M1 contract

A scenario accepts an optional `Traits` array of species-trait IDs. All groups belong to one species with one trait set. An omitted array means an empty set; explicit null, empty, unknown, and duplicate IDs are rejected. Heterogeneous worker assignments are not modeled.

The importer reads `common/traits`, requires `allowed_archetypes` (leader traits are not accepted), and extracts the unconditional `modifier`. Numbers and variables are resolved by ContentSession. Supported modifiers are `pop_bonus_workforce_mult`, `pop_<job>_bonus_workforce_mult`, and `pop_cat_worker_bonus_workforce_mult` for the three worker jobs. Composite names are recursively expanded through the real definitions in `common/static_modifiers/24_static_modifiers_jobs.txt`, multiplying coefficients. Unknown bonus_workforce modifiers, cycles, invalid operators, and negative values are rejected. Penalties remain outside the scope of doc15 and are not automatically zeroed.

A selected trait with `triggered_*`, `inline_script`, or `auto_mod=yes` is rejected: conditions and automatic trait changes are not implemented. Availability, cost, archetype, and opposites are not checked: the scenario supplies an existing species, not a species constructor. Other direct effects are excluded with Coverage. Known bonuses for other jobs/strata are marked “outside M1.” This is not a complete model of trait, happiness, and population economics.

For each job, applicable rates from selected traits are summed. B=W*SumTraitRates, followed by the doc15 formulas, including upkeep. The result contains IDs, rates, Coverage, and a SHA256 manifest of all files read. The CLI applies the rates automatically before calculation. A neutral scenario without Traits is unchanged.

## Acceptance

- Synthetic direct, generic, and composite bonuses; addition; variables; excluded effects; and the listed errors.
- Real trait_ingenious/trait_industrious/trait_agrarian traits give 0.15 to the corresponding jobs; trait_syncretic_proles adds 0.10 to workers through the vanilla group. Together, the rates are 0.25.
- Conditional trait_aquatic is explicitly rejected; the error does not partially apply to the world.
- W=100, base 6, bonus 0.15: output 6.9. Synthetic upkeep scales; partial/zero employment preserves doc15 behavior.
- Existing tests, neutral balances, and repeated/parallel runs pass. The CLI is checked against real 4.5.2 content. File checks are not a manual comparison with the game engine.

## Implementation and verification

Implemented in `..`: TraitWorkforceImporter expands vanilla groups, loads global variables itself, and returns an immutable projection. The CLI accepts `traits` in JSON and automatically passes nonzero rates to the economic core. Example: `scenarios/trait-planet.json`. No additional numeric bonus fields are needed. The specification and notes were written before implementation.

Final Release run on October 12: 7 suites PASS, 0 failures (564.80 ms), including existing checks and real 4.5.2 traits. Verified direct/generic/composite bonuses, coefficients, local and global variables, unknown pop-leaf, cycles, negative values, conditions, invalid operators, partial/zero employment, upkeep, and reproducibility. The real import manifest contains 80 files with SHA256; original content was not copied into the project. Review fixed writing zero bonuses into the neutral state and failure to load global variables automatically. Tests were repeated after the fixes.

CLI on drive D: 120 months with ingenious/industrious/agrarian, rates 0.15; final stocks energy=1625.50, minerals=2242, food=2794. Repeated/parallel runs matched:

```text
1E5CD1429B611243731C505F6C65C8A16435E3A0EEF636D969B72BC486793BE7
```

The neutral scenario again produced the previous 1450/2080/2560 and previous hash `C4788BCC2705690054FF785B2A914071399F0891AEB90DE799D06D8F2097A3DD`. Reports: `artifacts/trait-workforce` and `artifacts/trait-neutral-regression` (ignored by Git).

Independent review is complete; no substantial open findings remain. No branches or commits had been created at the time of this run; the stage was later committed as D42. The game installation was not changed. No manual comparison against a running Stellaris was performed. Conditional traits, penalties, heterogeneous species, and other economic effects remain outside this implementation.
