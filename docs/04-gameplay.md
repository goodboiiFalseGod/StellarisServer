# Gameplay and Economy

## Economy

Use the target Stellaris version's Pop Groups/Workforce model. Store population composition, group attributes, labor supply, job allocation, employment and derived planet statistics. Do not treat historical grouping criteria as universal rules; verify them against the target version.

Implement resources, caps, production and upkeep, jobs, buildings and districts, queues, research and related modifiers. Exact categories and formulas depend on the enabled content set.

Before implementing each calculation, document its inputs, formula, rounding, application timing, modifier sources, recomputation dependencies, shortage behavior and example results. Deviations require a user decision.

The user has confirmed the job production/upkeep formulas and modifier order: see the [Workforce and job upkeep contract](15-workforce-and-job-upkeep.md) and decision D38. Bonus Workforce affects both operations; output affects production only. Job upkeep is adjusted after Workforce scaling; do not confuse it with Pop upkeep or fixed building upkeep.

## Map and Development

The first check uses a fixed map. A playable version includes generation of 200–600 systems, hyperlanes, starting ownership, sensors, exploration, expansion and colonization. Matching the original map from a seed is not required, but custom generation must be reproducible.

Empire creation is limited to a small agreed set of templates, without complex unique scenarios. The template set remains open. Players must not be offered definitions whose required mechanics are missing.

## Interactions

The first playable game needs wars and peace, system and planet conquest, limited diplomacy, resource exchange, basic traditions and limited events. Agree on claims, occupation, ground combat and war endings before implementation.

Events and offers keep their normal deadlines. Expiry results in refusal; the displayed consequences and actual refusal effect are defined per event type. Automatically selecting the first event option is prohibited.

## Victory and Pacing

The balance target is to determine a winner within 4–5 hours, over roughly 50–60 in-game years. The game end date is configurable; there is no required fixed end year. The user accepted the submitted POINTS EXPLANATION as the foundation: periodic points every 10 years, 4 points for each controlled rival starting world at each interval, and achievements. The [scoring specification](11-victory-and-scoring.md) retains the reference and records the user's clarifications.

The unit, range and default for the end-date setting, final tie-breaker, active achievement subset, starting era, technology speed and development cost remain unspecified. The source covers intervals up to 100 years; this does not impose a maximum game duration. These questions block final balance, but not engine research.

## Population Source

[Official Developer Diary #370, published on Steam](https://store.steampowered.com/news/posts/?appids=281990&enddate=1740052856&feed=steam_community_announcements): the 4.0 transition to Pop Groups/Workforce. It explains the basic approach but does not replace checking the selected game version.
