# Owner country flag checks

12 October 2026. Contract: [explicit owner flags](../CONSTRUCTION-OWNER-FLAGS.md). Narrow shared evaluator correction, not playable-construction delivery. Source-backed semantics confirmed by reference turn `01a131bd-33e5-74c1-835d-4f75643935b1`; exact real source branches remain retained with origins/raw duplicate fields.

Implemented independently bounded immutable `ConstructionSourceFacts.OwnerFlags`. Assigned literal `has_country_flag` uses ordinal membership only with an explicitly existing owner. Null flags/scope stay unknown; explicit empty means no flags. No runtime caller supplies defaults as part of this change. Template/civic/carrier facts do not substitute. Unsupported shapes/operators/scopes stay unknown; no dynamic expansion or timed-flag semantics.

Checks on the owner worktree:

- `dotnet run --project tests/Stellaris.ConstructionChecks -c Release -- --game-root <local licensed Stellaris 4.5.2 root>`: **35 PASS, 0 FAIL, 1 SKIP** (existing reparse/symlink privilege check).
- `dotnet run --project tests/Stellaris.SessionChecks -c Release`: **7 suites, 0 errors**.
- Existing independent reviewer inspected the actual production/test/contract diff: approved, no actionable findings; did not duplicate suites or change files.

Synthetic coverage includes present/excluded/empty/unavailable flags, ordinal IDs, caller mutation, set/identifier limits, NOT, guarded absent owner, unknown owner, wrong scopes, malformed operators/blocks/scalars/template markers and no carrier substitution.

Real `zone_research_unity` with explicitly supplied ordinary owner/planet facts:

| Owner flag snapshot | Regular branch at `zone_researchers_add.txt:4` | FE branch at `:468` | Remaining stage blockers |
|---|---|---|---|
| Explicit empty | True | False; three raw duplicate fields/diagnostics retained, nonblocking | `catalog-potential-not-declared` |
| Contains `fallen_empire_1` | False | True | `catalog-duplicate-amount`, missing potential |
| Unavailable | Unknown | Unknown | `owner-flags-context`, duplicate amount, missing potential |

In all cases whole installed-snapshot application remains refused atomically on missing housing potential in `shared_city_non_urban_zone_modifiers.txt:1`. No sum/first-match rule was selected for active duplicates. Existing fixtures without flags now report `owner-flags-context` instead of the incorrect `boolean-condition-value`. No original files changed. Live startup, timed flags, native missing-scope rules, housing potential semantics and full-stage/manual acceptance remain unverified/pending. Integrator owns stable-main verification and readiness notice.

Root inspected the actual final production, test and contract diff and integrated `c7c0e52` as `f560667`. Integrated real-source ConstructionChecks passed **35 checks, 0 failures, 1 unavailable-symlink skip**, including empty/present/unavailable research flag branches. No UI/browser or broad transport suite was repeated for this pure source evaluator change. Stable main remains unchanged and is not ready for the full manual-review delivery.

The integrated server Release build passed with **0 warnings and 0 errors**. Existing-owner continuation targets exact reached civic-membership/source operators, retaining selected civic membership separately from validated applicability.
