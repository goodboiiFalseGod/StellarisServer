# Manual Economy Comparison with Stellaris 4.5.2

## Deferred Check FACULTY-CATEGORY-001 — Category Research Speed

**Pending, not checked; 16 October 2026.** Request from the current integrator `01a144d3-40d8-7b40-accd-6da02cb78b70`; do not launch the game now or repeat the general audit. Provided source anchor: `common/buildings/05_research_buildings.txt:767–782`; Faculty grants the country `category_archaeostudies_research_speed_mult` +0.25 unconditionally and another +0.25 on `pc_relic`. The source alone does not prove how category interacts with all/area/no-leader modifiers or multiple categories on one technology. The project's accepted additive rule of all + area + no-leader (−0.25) is not reopened by this experiment.

In the original 4.5.2, prepare one active archaeostudies technology and comparable save branches: without Faculty, with Faculty on an ordinary planet, and with Faculty on a relic world. Record exact technology/category/building/planet IDs, checksum/DLC/playset, raw R, reserve and its spending rules, leader, and all all/area/category modifiers. Keep these conditions identical except for Faculty's contribution under study; measure actual monthly progress, not only tooltip/ETA. Isolate other relic-context effects; if that is not possible, record the limitation instead of attributing the whole difference to category.

Use separate controlled branches to check category against a known all/area bonus; check interactions among multiple categories only if an applicable technology with confirmed source categories exists. Report exact progress deltas, before/after state and remaining unknowns; do not derive a formula without observation. No suitable state is currently available. Do not modify the installation or launch the game automatically; if saves/logs/screenshots become available later, store them locally outside Git/publication and include links/hashes and conditions in the report. This scenario does not resolve empire-limit/output dependencies or establish that Faculty construction is ready. Do not change other branches/worktrees.

## Deferred Check NANITES-CAP-001 — Country Stock Cap

Added 14 October 2026 at the request of the rare-resources owner `01a13770-e8f0-7df2-8002-e456e71a74f8`. **Not checked; launching the original game is not currently required.** This is a narrow question about the native default when max is omitted, not a repeat resource audit. Provided source: `common/strategic_resources/00_strategic_resources.txt:408–421` — nanites have no `max`, `fixed_max_amount` or `special_max_amount`; documentation on lines 7–9 sets default flags=no but does not specify numeric capacity. The reference owner found no sufficient defines evidence. Do not assume the cap is 15000, zero or unlimited by default.

Scenario for a future agreed experiment:

1. In unmodded 4.5.2, use a country that has actually obtained nanites. Record checksum, DLC/playset, date, nanite source, stock and all applicable modifiers; save a separate game branch.
2. Capture the standard resource tooltip with capacity and, if available, exact save fields. A missing displayed cap does not prove unlimited capacity. Distinguish the **country** stock from station/waystation stockpiles.
3. If reproducible, change the general `country_resource_max_add` on a separate branch through a specific confirmed source with a known value, then repeat the measurement. No source has been selected in advance; without one, modifier applicability remains unchecked.
4. If native overflow is reachable, record exact stock before/after, amount received, amount actually retained and lost, date, and background income/expenses. Do not create fake prototype stock for this experiment.

Result: the exact observed country nanite cap and applicability of the general modifier, **or an explicit unavailable limitation** with its reason. Do not report inaccessible overflow or exact fields as PASS. Do not modify the installation or publish saves/game bytes; store any screenshots locally in ignored `content-local/references/` with links and SHA256. The unknown blocks only the dependent capacity rule; including all seven rare resources in the RC does not itself prove the native numeric default.

Goal: obtain independent data from a running game rather than repeat laboratory calculations. Start with **one job on one planet**, preferably technician. There is no need to wait 120 months or create a perfectly neutral empire.

## 1. Preparation

1. Launch Stellaris 4.5.2. Record the version and checksum from the main menu, and list enabled mods.
2. Open your game and pause. The initial data collection does not change the game. For later experiments changing job slots, use a separate save/test game, not the only copy of an important game.
3. Choose a planet with ordinary technicians, miners or farmers. Do not choose technodrones, anglers or other replacement jobs. If unsure, send the tooltip; do not disable traits/civics or rebuild the empire yourself.
4. Open the planet window and the population/jobs section. Tab names vary by language/UI. We need the row for a specific job, not the empire-wide resource total in the top bar.

## 2. Minimum Set: Three Tooltips

Take screenshots while paused on the same in-game date. Send multiple frames if a tooltip does not fit.

**A. Job employment.** Hover over the Workforce value/bar for the selected row. We need actual employed labor, the set limit, maximum, and any bonus/extra workforce or efficiency penalty lines. Record the Pop count too if shown. Limit and maximum are not substitutes for actual employment.

**B. Output from that job.** Hover over the produced resource in the same row or over the job icon; use the tooltip that breaks down its output. We need total monthly output, base, additions and percentages with source names. Include the heading and unit label: it matters whether the value is the whole row total or per 100 Workforce. If the interface does not show a breakdown, send what is visible; do not guess values.

**C. Groups providing the labor.** If the Workforce tooltip lists groups, expand/show them. We need population, employment and Job Efficiency/bonus Workforce. This helps distinguish a species bonus from an ordinary output percentage. For the first step, the available tooltip is enough; no full trait investigation is needed.

Additionally, if a relevant building is present, show the tooltip for the energy grid, mineral purification or food processing building. We need its level, economic effect and upkeep. Do not build one specifically for the initial set.

A mod that adds base values to job names is not an independent reference: send the standard tooltip for actual output, not just the modified name. Identify mods that alter the economy; results from such a game cannot automatically be called vanilla.

## 3. How We Compare

From current definitions, the laboratory derives the following bases. They are for normalized 100 Workforce, **before other modifiers**:

| Job | Without selected building | With selected base building |
|---|---|---|
| Technician | 6 energy | 6.5 energy |
| Miner | 4 minerals | 4.5 minerals |
| Farmer | 6 food | 6.5 food |

Each of the three selected buildings has base upkeep of 2 energy. Actual upkeep may have its own modifiers; that is a separate check.

For a neutral case without bonus/automated Workforce or other modifiers: output = actual employed Workforce / 100 × base plus building addition. For example, 200 Workforce of technicians with an energy grid produce 13 energy **gross job output**, not net planetary income. With an additional ordinary +20% output modifier, this limited formula yields 15.6; this is an arithmetic example, not a requirement to obtain that bonus in-game.

Do not add bonus Workforce again if the interface already includes it in the displayed total. If a label is ambiguous, first establish what the field means. Under clarification D38, the core supports explicit job bonuses and separate upkeep modifiers (doc15); limited import of unconditional positive bonuses from selected traits is implemented in [doc16](16-trait-workforce-import.md). Heterogeneous allocation and population upkeep remain unimplemented. For a bonus outside supported imports, identify and account for its source separately rather than automatically treating 100 as the wrong divisor. Scale job upkeep by effective Workforce, then apply its cost modifiers; do not apply output modifiers to it.

Do not subtract population, building or planet-wide upkeep from job output. First compare gross production, then each expense separately. Stability, technologies, governors, planet specialization and other effects do not have to be absent, but must be included in the submitted breakdown. Compare differences at the precision shown; hidden rounding cannot be proved from one rounded tooltip.

## 4. Additional Check for Changing Job Slots

Only after the initial set has been reviewed, use a separate test copy of the game:

1. Record sets A/B before the change.
2. Reduce the selected job's Workforce limit using the standard slider. It does not have to be exactly 100 or 200.
3. Advance the game until actual reallocation and updated values occur, then pause again. The limit alone does not prove recalculation. Record both dates; if reallocation does not occur, that is also a result—do not repeat blindly.
4. Capture A/B again, including actual Workforce, output and modifiers. If they changed, the comparison must account for both changes, not just the limit.

Console commands, game-file changes, population removal and disabling buildings are unnecessary.

## 5. What to Send

```text
Version/checksum:
Mods:
Planet, date, job:
Actual Workforce / limit / maximum:
Pop count (if visible):
Bonus Workforce / automation / penalty (if visible):
Output and unit label (total or per 100):
Base, additions, percentages and sources:
Building and level (if present):
Screenshots A, B, C:
```

You can simply send screenshots and the version; I will interpret the table. After review, we will separately record what was confirmed, what differed and what could not be checked. New discrepancies go into M1-DISPUTES.md before formula changes. These instructions do not mean that manual comparison has been performed.
