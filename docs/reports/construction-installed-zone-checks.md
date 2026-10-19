# Installed zone application checks

12 October 2026. Contract: [installed zones](../CONSTRUCTION-INSTALLED-ZONES.md). Primary reference answer `01a131b4-0b22-7590-b49d-139aa58ddfc3` confirms raw flat slot additions, documented district scaling and the remaining missing-triggered-potential gaps. No source installation changes.

Focused real 4.5.2 ConstructionChecks: **34 PASS, 0 FAIL, 1 SKIP** (symlink creation unavailable). Synthetic coverage includes explicit instance/district/level/source/prior-revision identity, two zones sharing one district without multiplying flat slots, level 0/replacement/removal, numeric triggered multiplier reuse, changed-binding retry conflicts, null/out-of-range/inconsistent bindings, unsupported active/missing-potential/overflow rollback after an earlier building, cumulative fanout preflight before zone staging, and separate raw-slot fork/hash isolation. Malformed/control/oversized district IDs refuse before fingerprint serialization.

Real mixed example uses one explicitly supplied generator instance and energy zone bound to an explicitly supplied district identity at level 3: technician raw contribution 200 + 300 = 500; housing 200; flat slot-addition total 3. No usable-slot count is calculated. Infrastructure baseline 5 energy, balance 1000, month 0 and empty completion ledger remain unchanged. This does not generate a homeworld or claim that supplied instances exist in the live fixture.

| Exact selected ID | Current raw installed-application result under supplied ordinary context |
|---|---|
| `zone_default` | Supported zero contribution snapshot; source `max_buildings = 6` is retained metadata, not applied additive slots. |
| `zone_energy` | Supported district-scaled technician additions plus independent flat `zone_building_slots_add = 3`. |
| `zone_urban` | Whole snapshot unavailable: `00_zones.txt:89` omits triggered potential. |
| `zone_industrial` | Whole snapshot unavailable: `shared_city_non_urban_zone_modifiers.txt:1` omits triggered potential. |
| `zone_research_unity` | Same missing shared housing potential; additionally current evaluator reports `boolean-condition-value` from `jobs/zone_researchers_add.txt:4,468` and repeated `job_fe_archivist_add` at the latter container (`catalog-duplicate-amount`). No first-match, guessed boolean or duplicate summation policy applied. |

Paths above are relative to `common/` where abbreviated and to the licensed installation. These are exact observed operator/condition gates, not proof that native game effects are absent. Active habitat dynamic operands, unknown context and any unexpected mandatory root/effect continue to refuse; inactive unsupported branches stay irrelevant. Country/pop-group/amenity effects, final slots/caps/occupancy, final infrastructure upkeep, staffing and live queue/startup remain unavailable.

Economy regression **5 suites, 0 errors**; session regression **7 suites, 0 errors**. Initial compile found a missing amount-type namespace import, corrected before successful runs. Independent actual-diff review by `/root/galaxy_review` identified district-ID validation after fingerprinting; moved before serialization with targeted tests and accepted on final re-review with no remaining actionable findings. No duplicate reviewer suite, UI/QA restart or stable-main readiness claim.

Root independently inspected the final production, contract and test diff and integrated `8392197` as `1e34080`. Integrated real-source ConstructionChecks passed **34 checks, 0 failures, 1 unavailable-symlink skip**, including the exact five-zone supported/unavailable boundary above. The preceding installed-capacity checkpoint already passed 259 isolated HTTP/WebSocket checks with clean shutdown; this zone checkpoint adds no public endpoint or protocol change, so that broad suite was not repeated. Browser checks were not restarted. Stable main remains unchanged; live construction and full-stage acceptance remain open.

The integrated server Release build passed with **0 warnings and 0 errors**. The owner is addressing the exact source-backed string country-flag condition gap next; missing triggered-potential defaults and genuinely active duplicate-key semantics are not silently approved by that work.
