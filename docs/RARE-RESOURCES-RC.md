# Rare resources in the experimental RC

Contract, 14 October 2026. Direct user instruction: all seven resources in the current version; no additional special sources beyond implemented mechanics are required. This expands scope; it does not confirm that implementation is complete.

## Behavior and sources

`volatile_motes`, `exotic_gases`, `rare_crystals`, `sr_zro`, `sr_dark_matter`, `sr_living_metal`, `nanites` use the shared ledger, production, expenses, construction, refunds and UI. IDs, numbers, categories, conditions and limits are imported from 4.5.2. Artificial stock, technology grants and special factories are prohibited.

Ordinary refinery/chemical/crystal plants: `common/buildings/04_manufacturing_buildings.txt:442–638`. Source composites `industrial_jobs_*_produces_add` are expanded through `common/static_modifiers/24_static_modifiers_jobs.txt:155–170` with provenance: output from existing metallurgists/artisans, without new refinery jobs. Production Add applies to the exact source category; Mult follows the existing category hierarchy. Output and job upkeep scale with effective Workforce; structural upkeep is separate. Source `planet_limit=1` counts installed and pending buildings; cancellation releases the limit. Tech prerequisites and ordinary costs are preserved.

New orbital systems, events and anomalies are outside this slice. The current world may have no reachable source of zro, dark matter, living metal or nanites; disclose this without fabricating income.

Later direct user decision: technology weights consider only implemented colony deposits. This is a temporary prototype policy, not native proof of territory/orbital deposits. Source helpers for the three rare resources retain their branches and conditions; unknown conditions and hidden deposits are not replaced with False. Under this policy, `any_planet_within_border` is resolved only for weight queries about the presence of deposits with a confirmed AST shape. Other predicates, potential/cost and unknown shapes retain the existing unknown gate. No new sources are created.

## Nanite cap

In `common/strategic_resources/00_strategic_resources.txt`, six resources specify `max=15000`; nanites at lines 408–421 specify no max. The reference owner did not establish the native default from defines/documentation; manual queue item `NANITES-CAP-001` remains pending. Do not infer 0, 15000 or unlimited by analogy.

Runtime distinguishes capped, unlimited and unspecified. For unspecified, preserving a confirmed balance, applying a zero change and safely debiting do not require an upper cap; insufficient stock is checked as usual. A positive credit/refund that depends on the cap receives a typed diagnostic and fails atomically, without losing the balance, order or part of the monthly economy. The general setter cannot bypass the credit gate. Zero nanite flow does not block other resources. This is a temporary dependent gate, not a native numeric rule.

## Acceptance

Independent actual-diff review; focused Core checks for composites/category/effective Workforce, installed/pending limit, credit/debit/cap/refund/rollback. Then Session source binding and UI for all seven resources, plus shared Release/HTTP/WS/browser checks. End-to-end path: real science/reserve → rare-production tech → paid producer → nonzero output from zero stock → paid upgrade → recurring upkeep. The old building remains active until atomic replacement; cancellation/refund follows the current construction contract. Synthetic fixture funding does not satisfy this criterion.
