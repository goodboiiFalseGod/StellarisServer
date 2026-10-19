# GALAXY-INITIALIZER-STRUCTURE-001 — handoff

Root acceptance: independent attempt2 review accepted; P2 endpoint validation closed.
Final combined genuine + Triangle regression **74 PASS**, log
`artifacts/galaxy-integration-ready/initializer-final-checks.txt`.
Review SHA256 `2B2EC757F3EC5DFADCDBFEB2B306E01F0190450E9E6541F27746AEF8D8CBACDC`.

October 13, 2026; base `c7bba78e44ae8d304bc7877e4fbf077193a39af9`; author `/root/galaxy_catalog`. Actual model/effort/usage unavailable. Scope complete as a structural candidate; independent actual diff/contract review pending. No commits/push, shared parser/Program/Session/planet/economy edits.

Attempt2 frozen SHA256: executor `9204A18715D7FF03EFE0D0EF8611E68FF98D4693B8FEB585839AD5605BF77F65`; numeric unchanged `F7A247D35301FD3C20F429BD7168D74B91DE154B7C9E9413115F84ACD1966A38`; checks `3D2D8CC38245E8FB14B2999BE347585C05DB0D78D35FF6D7403A961650CD23F3`.

## Public API

- `GalaxyInitializerExecution.Compile(catalog, initializerId)` → immutable `GalaxyCompiledInitializer`; retains definition, expanded source fields, descriptors, unified children, star ordinals and neighbor AST.
- `GalaxyInitializerExecution.Execute(catalog, compiled, context, NativeRandomState)` → immutable `GalaxyInitializerResult`: body IDs/parents/class/size/orbit/name/flags, home IDs, belts, orbital lines, deferred requests, draw ledger and next state.
- `GalaxyInitializerDefaults.VanillaDescriptor(missingSystemClass, missingClassSizeCallback, initialOrbit, initialAngle)` supplies proven descriptor defaults: random / count1 / distance[10,20) / angle[90,270) / fixed100000. Missing selected class-size fallback and initial accumulators remain explicit caller policy.
- `GalaxyInitializerContext(defaults, idealPlanetClass, idealDesignClass, habitableMultiplier, classPotentialPredicate, additionalRandomFilter, maximumBodies=10000, maximumDepth=64)` requires explicit condition policies. Literal class IDs still checked through catalog.
- `GalaxyNumericValues.Evaluate(value, variableSource?)`, `.Range(value, variableSource?)`: checked decimal arithmetic, local-before-global variables, inline math +−*/parentheses, cycle/depth/source errors.
- `GalaxyInitializerChecks.Run(Check)` and `RunVanilla(catalog,Check)`; root owns Program wiring.

## Verification

`dotnet build tests/Stellaris.GalaxyGenerationChecks/Stellaris.GalaxyGenerationChecks.csproj --no-restore -v:q` PASS, zero warnings/errors. Run/RunVanilla invoked through PowerShell reflection without Program changes: **6 checks PASS**.

All **360/360 vanilla definitions compile, zero gaps**. Previously seven gaps closed by source clarification: range endpoints start0; last assignment wins singleton size/orbit; signed descending ranges remain valid. Genuine execution covers every **11 ordinary initializer** (`basic_init_01..06`, asteroid1, binary1..2, trinary1..2), every **6 generic home initializer** (`random_empire_init_01..06`) and `voidworms_spawn_system_small` inline template. Each generic home produced exactly one home candidate with seed13; all selected systems had multiple bodies; binary/trinary star counts and parent references checked. Bodies ranged4..21, ledger16..130 draws in this exercised set.

Synthetic checks cover numeric variables/expression/cycles/zero division/unsafe tokens; unified child planet/moon/gap order and shared accumulator; source home alias; count/class/distance/size/angle ledger; scalar zero-span draws; deterministic rerun; star descriptor ordinal reused by count and nested-star sentinel; template arguments/positive-negative conditional header; inline cycles; unknown structural fields; descending fixed/integer ranges, missing endpoint0 and singleton last assignment.

## Evidence and limits

Read frozen initializer-source report and native correction `initializer-descriptor-defaults-handoff-452.md` SHA256 `009CC4F18624AB3C7194E5EB84AD96C8C38A691B67E02DC26090B8E005087126` from reverse owner's outputs. Its distance prose1.0 was corrected by root to raw1000000 / scale100000 =10.0. Root relayed dispatcher/range closure: class before distance; generic positive weights, modulo-total reduction and first remainder<=0; star source factor multiplies class default, then habitable multiplier with fixed truncation; named RL one modulo-count draw; asteroid mode bypasses orbit/habitable predicates.

Structural descriptor Source retains every known field, including entity/naming policy/independent orbital line flags. Body `home_planet` and `starting_planet` aliases share the runtime home boolean; source spellings remain in AST. Children planet/moon/change_orbit use ONE insertion-order vector; orbital lines afterward. Root explicit star ordinal belongs to descriptor, reused for every count instance; nested star has−1 and fails multi-entry selection without object allocation. Missing class remains distinguishable in compiled IR and executes default random.

Result `GameplayComplete=false`; `DeclaredPolicies` discloses remaining approximations: star-RL weighting/reducer not native-certified, metadata belts sampled after body/line construction without exact native RNG-interleaving claim. Planet class potential and optional requirements are supplied predicates, not a hidden condition evaluator. Fixed domain explicit context FixedScale; default100000. Source field validation is fatal for unknown structural fields; deferred effect bodies remain raw AST and are never silently executed/no-op.

Random modifiers/deposits/blockers/anomalies/rings/generated naming and body/system init_effect are explicit ordered deferred requests. No initializer selection/usage odds/max-use counts, neighbor batching, arbitrary effects engine, empire/startup economy or PLAYABLE_WORLD certification. This slice is ready for a structural producer/bootstrap adapter with these limits visible.

## Attempt2 — focused review correction

Reviewer P2: integer sampler checked only the Minimum lower bound and Maximum upper bound. Fixed: both endpoints are fully checked for int32 before delta/Abs; out-of-range ascending/descending/constant ranges produce typed `integer-range`, including oversized explicit size. Regression covers six invalid count endpoint pairs and two oversized size forms.

Read final `galaxy-generation-range-source-0d65138c4df3.md`: descending inclusive formula confirmed by native evidence; the previous uncertified note was removed. Count<=0 now skips the creation loop after the count draw, without a negative-count fatal; regression checks −1 and 0: zero bodies, exactly two count draws. This evidence supersedes the previous pending statement in the initial contract.

Exact attempt2 build checks project: PASS, 0 warnings/errors. Focused `GalaxyInitializerChecks.Run(Check)` via reflection: **5 PASS**, including new boundary/nonpositive regressions. Original full vanilla inventory/execution checked in attempt1; root will repeat exact-source integrated checks during re-review. Only owned executor/checks/handoff changed; no commits/spawn/shared edits. Re-review pending.
