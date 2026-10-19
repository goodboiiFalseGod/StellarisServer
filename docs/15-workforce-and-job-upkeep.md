# Workforce, Base Output and Job Upkeep

12 October 2026. Direct user confirmation, decision D38. This contract clarifies the M1 formulas; it does not include import of all traits, automation or the full population economy.

## Calculation Order

15 October 2026, direct user decision: a mineral shortage does not stop
simulation. After the combined monthly net of all of the country's colonies,
the `minerals` stock is `max(0, previousBalance + net)`. The signed net,
production and upkeep remain fully represented in the report. The month, daily
phase and construction continue atomically; order prices and purchase
affordability checks are unchanged. This authorization applies only to
minerals; shortage rules for other resources and storage/overflow guards remain.
This is an accepted product rule, not a claim about the complete native
4.5.2 shortage/penalty mechanics.

Live acceptance `6a169f4`, 15 October 2026: exact Release 0/0, protocol 2/m3,
924 real local files (460 assets/464 EN+RU, 0 diagnostics), two standard players.
Using its own miner limit, miner 0 produced net minerals −30: stock 110 on
2200.04.06, stock 0 on 2200.08.11 and still 0 on 2200.11.29; the date advanced
without a shortage stop. Other stocks/permissions were not substituted. Five
Stellaris.Checks suites PASS; independent `/root/mineral_floor_review` accepted
the actual diff and atomicity. Served client SHA256 matched source
`09dbda98faaaf76d645c7bdc2773cc3f480c03b0b617801c8bd4ef6c08eb9a82`.
DOM checkpoints: ignored `artifacts/minerals-zero-live-6a169f4-dom.txt`.
Screenshots are local and ignored only:

- [Zero stock](../content-local/references/minerals-zero-live-6a169f4.png), SHA256 `c786ba8ffbf8c4c1f62cd36cb34440e4b77dd536a903f0054982cb861bf4ba34`.
- [Following months](../content-local/references/minerals-zero-continued-6a169f4.png), SHA256 `5073a9c1f8a8bf30fa2521f639bfe12859e376d96f2aae61043ac3dccbc26aa9`.

For a specific job, W is the actually occupied ordinary Workforce, B is the bonus Workforce of employed workers. D is output normalization (D=100 in the Stellaris profile):

```text
EffectiveUnits = (W + B) / D
ScaledProduction = EffectiveUnits * (BaseProduction + ProductionAdd)
Production = ScaledProduction * (1 + SumOutputModifiers)
ScaledUpkeep = EffectiveUnits * (BaseUpkeep + BaseUpkeepAdd)
JobUpkeep = ScaledUpkeep * (1 + SumUpkeepModifiers)
```

Base production additions are added to the base before scaling. Job cost reductions/increases are separate upkeep multipliers applied **after** Workforce. Output does not affect upkeep. Existing BaseUpkeepAdd denotes an addition to the base per normalized unit, not a separate fixed monthly payment; this API does not define a separate post-scaling flat addition.

For both operations, add applies only to the exact economic category; mult accumulates across the category and its ancestors, as in the existing core. Production and upkeep use different Modifier.Operation values and must not be mixed. All calculations use checked decimal arithmetic without intermediate rounding. A negative base after addition or a negative final multiplier is an explicit monthly-calculation error; a zero multiplier is allowed. An error must not partially mutate the world.

Building or district upkeep is a separate source. It is not derived from job upkeep, is not scaled by Workforce, and is not changed by output or job-upkeep modifiers. Only explicitly supported modifiers for the corresponding building/district category apply; M1/M2 do not yet execute such modifiers. Population upkeep is a third separate system and is not yet imported.

The report stores `JobUpkeep`, `BuildingUpkeep` and their sum, `Upkeep`. `Net = Produced - Upkeep`; each source contributes exactly once. Within the current M1/M2 scope, `BuildingUpkeep` contains only the direct `resources.upkeep` of selected buildings. Districts are not implemented or disguised as buildings; adding them requires a separate report source. `Upkeep` remains as a compatible aggregate, but should not be the only displayed expense. Do not subtract building or population costs from gross job output when comparing results.

## Bonus Workforce Data in the Limited Core

### WF Summary in the Planet Menu

For existing `development.economy`, the summary displays “Job slots, WF”
as the sum of `jobs[].capacity` and “Unemployment, WF” as
`baseWorkforce − Σjobs[].assigned`. `assigned` is ordinary occupied WF from
authoritative PreviewPlanetAllocations; bonus WF is not subtracted from base WF.
These values represent capacity/unoccupied WF in the current prototype, not
native job or unemployed pop counts; no division by 100 is applied.
If the DTO is absent/incomplete, jobIds repeat, values are negative/non-numeric,
or total assigned exceeds baseWorkforce beyond JS-sum tolerance, the indicator
remains Unknown (`—`); a residual within tolerance is clamped to zero.
Other unknown KPIs are not filled in by guesswork; the summary layout/order is unchanged.
Job-capacity tooltips/ARIA in the catalog and inspector use localization keys
from supplied `jobDefinitions` (then `jobs`) and explicitly state WF. If bindings
are absent, the original ID remains; native localization keys are not guessed.

Addendum: automatic import of unconditional positive bonuses from selected traits is described in [doc16](16-trait-workforce-import.md). The import limitations below refer to the state before this addendum; limitations on heterogeneous workers and penalties remain.

Implemented `SetJobBonusWorkforceMultiplier(planet, job, multiplier)` sets the bonus fraction of ordinary occupied Workforce for a specific job; for example, 0.2 means B=W*0.2. This is a numeric core input for homogeneous workers; the limited trait importer in doc16 passes calculated fractions here. A complete allocation algorithm for heterogeneous groups is absent. Default is 0; reset to 0 must be checked. Negative bonuses are not yet supported by this API and are explicitly rejected; an efficiency-penalty model requires a separate contract.

Job slots constrain only W. Bonus B consumes no additional slots, may raise effective Workforce above capacity, is not taken away from other jobs and is not created without actual employment. With partial employment, B decreases proportionally. A change to job capacity or bonus is recalculated in the next successful month. The report from the last successful month is preserved until then.

JobAllocation stores Count as the number of occupied ordinary normalized units and Workforce as W, and additionally reports BonusWorkforce=B and EffectiveWorkforce=W+B. Bonus settings and reports are included in the deterministic StateHash even before the next month. The state format/check hash therefore changes, although a zero-bonus scenario should retain its previous numeric balances.

## Required Checks

1. W=100, B=20, BaseProduction=6, ProductionAdd=1, output=+25%: output is 10.5. With BaseUpkeep=2 and upkeep=-25%, upkeep is 1.8. With upkeep=+50%, upkeep is 3.6. Output does not change these costs.
2. With W=100, B=20 and one slot costing 100, effective Workforce is 120; the bonus on the first job does not deprive a second job of ordinary Workforce.
3. Partial employment W=50 yields B=10 at a 0.2 fraction. Zero employment yields zero output and JobUpkeep, while BuildingUpkeep remains.
4. A base addition can produce a resource absent from the base table. Parent add is not inherited; mult and operations remain separate.
5. Resetting/changing the bonus and job slots changes the next calculation and StateHash. Invalid job/entity type/negative bonus is rejected without changing state; overflow and invalid monthly formulas are atomic. A 100% upkeep discount is allowed; below -100% is rejected.
6. Repeated/parallel 120-month runs with bonuses and commands produce identical hashes. Previous synthetic and real v4.5.2 checks pass; neutral balances of 1450/2080/2560 are preserved.
7. BonusWorkforce changes only JobUpkeep; job-upkeep and output modifiers do not change BuildingUpkeep. With both sources, `Upkeep = JobUpkeep + BuildingUpkeep`, and the balance is reduced by that sum once. JSON and UI show the breakdown. Real selected 4.5.2 buildings retain their own upkeep at zero employment.

The core API and its tests do not by themselves imply support for all traits. Limited CLI import of unconditional positive bonuses from selected traits is implemented separately; its contract, regression and review are in [doc16](16-trait-workforce-import.md).

## Implementation and Regression

Implemented in ... Added SetJobBonusWorkforceMultiplier, BonusWorkforce/EffectiveWorkforce to the employment report, and bonus settings to StateHash. Laboratory normalization remains defined by JobDefinition.WorkforcePerJob, equal to 100 in the 4.5.2 profile. Ordinary Workforce and its allocation are unchanged. The bonus applies only to the employed portion of a job.

Release build and final checks after all changes:

```powershell
Set-Location ..
dotnet run --project tests/Stellaris.Checks -c Release -- --game-root "C:\Path\To\Stellaris"
dotnet run --project src/Stellaris.EconomyLab -c Release -- simulate --game-root "C:\Path\To\Stellaris" --scenario scenarios/neutral-planet.json --output artifacts/workforce-v2
```

Five suites PASS, 0 errors: parser, previous core, bonus Workforce/upkeep, synthetic import, and real 4.5.2 definitions. The first six acceptance items in effect at the time were checked; item 7 was added and checked later in the section below. Additional tests use decimal overflow to distinguish stage ordering: Workforce first, then mult, separately for production and upkeep. Errors were checked for atomicity in both sequential and parallel execution. Independent static review of the core and delegated tests found no material issues.

The neutral 120-month run yielded the previous balances: energy=1450, minerals=2080, food=2560; repeated/parallel runs matched. New state hash (format v2):

```text
C4788BCC2705690054FF785B2A914071399F0891AEB90DE799D06D8F2097A3DD
```

The hash change is expected due to the profile, bonus settings and report fields; neutral balances did not change. The first M1 hash remains in historical doc13 and is not used as the v2 golden.

Repeated load test on the same Ryzen 7 5700X: 600 planets, 20 countries, 120 measured months after one warmup, with no bonus in the CLI. p95: 7.747 ms sequentially, 2.173 ms in parallel. Total allocations: 112103728/112646912 bytes. Hashes matched: 258DB303D18E174F0F5119B4338D7E22952D3AD89F0814EC02AAC848834C8E1E. One measurement per mode; no performance threshold or full-game forecast is inferred from it. Reports are in artifacts/workforce-v2-bench-serial and artifacts/workforce-v2-bench-parallel (ignored by Git).

At the time of the run described, the bonus was set through the numeric job API; limited automatic trait import was implemented later in [doc16](16-trait-workforce-import.md). Heterogeneous group allocation, efficiency penalties, automation and full population upkeep remain unimplemented. No manual comparison against a running game was performed. The game installation, branches and commits were not changed during the described run.

## Separating Expense Sources

Inspection of v4.5.2 files confirmed distinct categories: selected buildings have `resources.category = planet_buildings` and their own `energy = 2`; jobs are in separate job categories. Neutral technician and farmer have no direct upkeep; miner has only a conditional cost outside the supported neutral scenario. Vanilla modifiers separately name `planet_jobs`, `planet_buildings` and `planet_districts` upkeep. This supports D43 but does not replace runtime testing of the full Clausewitz stack.

There was no double deduction in the core: JobUpkeep was calculated once, then direct upkeep for selected buildings was added once. The issue was losing the provenance of the total in the single `Upkeep` report field. The current report also stores `JobUpkeep` and `BuildingUpkeep`; compatible `Upkeep` remains their sum, and `Net` subtracts it once. The old `SetFixedUpkeep` method remains as a compatibility bridge to `SetBuildingUpkeep`, but new calls use the domain-specific name.

Release builds of the server, laboratory and checks completed without warnings. Seven Stellaris.Checks suites, seven SessionChecks suites and real HTTP/WebSocket ServerChecks passed. Real technician/miner/farmer jobs were checked at 0/50/100/200 Workforce: at zero employment, direct building upkeep of 2 energy remains. Neutral balances 1450/2080/2560 were preserved; new StateHash due to the profile and report schema: `3AD65C815829FB2D6A69F963D3B0B2921749EF9AE7D1D9E94CCF7EE2DBB643FC`. Previous v2 hashes remain historical and are no longer the current golden.

Modifiers to building/district upkeep are not implemented: building import does not yet retain its economic category, and districts are absent from M1/M2. Therefore the system applies neither job modifiers nor unsupported building modifiers to BuildingUpkeep. Population upkeep is also unimplemented. These limitations are shown explicitly rather than replaced by guesses.
