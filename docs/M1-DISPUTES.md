# M1 — Disputed Questions and Assumptions

Addendum doc16: trait import is limited to unconditional positive bonuses for homogeneous species. Conditional traits, Workforce penalties, combination eligibility checks and other species effects remain outside the projection; unsupported conditions/penalties are rejected, and excluded direct effects are visible in Coverage. These limitations do not imply a change to target vanilla rules. Fraction sums are checked numerically, but are not presented as a direct comparison with hidden engine arithmetic.

Statuses: open, temporary experiment, resolved. This does not replace global OPEN-QUESTIONS.md.

| ID | Status | Question / fact | Action |
|---|---|---|---|
| M1-01 | Resolved by version | Installation updated from 3.5.3 to v4.5.2; import of three jobs/buildings passed, and the manifest contains 12 files used. | Manifest records the set, but does not prove vanilla purity or complete DLC coverage. |
| M1-02 | Resolved | Independent builds were previously prohibited. | User explicitly authorized builds and automated tests for the standalone milestone. |
| M1-03 | Open | No strategic allocation algorithm has been selected for competition between heterogeneous groups. | M1 checks neutral groups; describe any temporary strategy separately and do not call it vanilla. |
| M1-04 | Open | Full match with Clausewitz's hidden rounding/allocation order has not been confirmed. | Distinguish checking read formulas from direct comparison with the game. Prepare manual examples. |
| M1-05 | Technically resolved | A specific stack had not yet been selected. | For M1, use locally available .NET 8 without NuGet and a scenario-specific ECS store. Choosing a general ECS for later stages remains open. |
| M1-06 | Resolved | The user replaced Git instructions and lifted the prohibition on independent commits. | Initial M1/M2 state was committed locally; commit future stages incrementally and preserve others' changes. D42. |
| M1-07 | Documented basis | DD370 and 4.5.2 localization support an output unit of 100 Workforce; POP_BULK_UNIT_SIZE alone is not evidence. | WorkforcePerJob remains an explicit parameter; M1 profile uses 100. Integration gate checks the neutral calculation. Direct runtime comparison and hidden rounding remain unchecked, but do not replace the original documented-example criterion with a full-economy requirement. |
| M1-08 | Projection limitation | Population upkeep, happiness, stability, habitability, growth and full allocation logic are not imported. | Laboratory income cannot be compared with total income on a real planet without decomposing modifiers. |
| M1-09 | Resolved by review | The importer silently accepted != in executable containers and numeric variables. | Added assignment checks for resources, categories, jobs, tables, conditions and variable references; regression tests pass. |
| M1-10 | New evidence | Official DD370 describes output normalization per 100 Workforce. Current 4.5.2 localization defines pop_group_unit=100 and labels output/upkeep in that unit. | This supports the numeric profile, not a full runtime reference. Details and source hashes are in doc13. A 4.5.2 save contains employment and total planetary output, but not a selected neutral example; it cannot be presented as a direct comparison. |
| M1-11 | User-confirmed | Bonus Workforce scales job output and upkeep. Additions are summed with base output; output does not affect upkeep, and cost changes apply after Workforce. | Decision D38, formulas and API contract are in doc15. This does not confirm hidden rounding, full trait import or heterogeneous allocation algorithms. |
