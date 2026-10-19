# M1 — Results and manual check

October 12, 2026. This is a console laboratory, not a playable session. The limited technical M1 was completed against the original criteria and is handed off for manual comparison. Workforce units are supported by documentation; the current game engine was not directly measured.

This report preserves the first M1 run **before D38**. References to the network/UI not yet existing and to the lack of commits describe only that run; later stages were implemented and committed (D42). After the user's clarification, the core was extended with bonus Workforce and the cost ordering was clarified: current specification/regression in [doc15](15-workforce-and-job-upkeep.md), trait import in doc16, network/UI in doc17–21. The old hashes and measurements below are historical; neutral numeric balances should remain the same, while the hash format changes with world data. The project baseline is now fixed to 4.5.2 (D44); the historical importer technically accepts 4.5.x, which does not confirm compatibility with other patches.

## Implemented

Paradox Script parser with repeated fields and error coordinates; SHA-256 manifest of external sources; explicit neutral 4.5.x import profile; specialized country/planet/group stores; monthly transaction; job changes; sequential and parallel calculation; result log and state hash. A full VM, network, UI, AI, population growth, and cost of living are not implemented.

## Launch

PowerShell with .NET SDK 8 installed:

```powershell
Set-Location ..
dotnet run --project tests/Stellaris.Checks -c Release
dotnet run --project tests/Stellaris.Checks -c Release -- --game-root "C:\Path\To\Stellaris"
dotnet run --project src/Stellaris.EconomyLab -c Release -- simulate --game-root "C:\Path\To\Stellaris" --scenario scenarios/neutral-planet.json --output artifacts/manual
dotnet run --project src/Stellaris.EconomyLab -c Release -- inspect --game-root "C:\Path\To\Stellaris" --workforce-per-job 100 --output artifacts/inspect
dotnet run --project src/Stellaris.EconomyLab -c Release -- benchmark --game-root "C:\Path\To\Stellaris" --workforce-per-job 100 --planets 600 --months 120 --parallel yes --output artifacts/benchmark
```

The value 100 is supported by documentation for the neutral profile; other laboratory parameters are not asserted as Stellaris rules. If the installation path changed, replace `--game-root`. Do not put reports in the game installation. Errors produce a nonzero exit code and a message. Runtime reports in artifacts are ignored by Git; original game files are not copied. Without game-root, tests use only project fixtures; with game-root, the required M1 external-content v4.5.2 gate is added.

`projection.json` contains the version, file hashes, normalized definitions, and included/inactive/excluded branches. `simulation.json` contains monthly reports, the command before month 61, and the final hash. The log is diagnostic, not a ready-made save/replay.

## Laboratory reference results

From 4.5.2 content, base output was read as technician=6 energy, miner=4 minerals, farmer=6 food; selected buildings add +0.5 to the corresponding resource and each costs 2 energy. Source references and lines are listed in projection.json/Coverage, hashes in Manifest. Modifier hierarchy comes from common/economic_categories; mult is inherited, add is not.

The scenario has 600 Workforce and 2 units in each job. Months 1–60: net flows energy=7, minerals=9, food=13. From month 61, technician=1: energy=0.5, the others unchanged, unoccupied Workforce=100. Final stocks: energy=1450, minerals=2080, food=2560. Hash for 120 months:

```text
4021D9FAC092CA0E48BC19CE12C300786D886B6EBABB6E55B14D32CCFF0CACEF
```

This is a numeric contract for a limited scenario, not a promise about the final balance of a real planet.

### Additional evidence for units

After the first report, a primary source was checked: [official DD370, February 13, 2025](https://store.steampowered.com/news/posts/?appids=281990&enddate=1742829055&feed=steam_community_announcements), Workforce section. The developers' example uses 100 Workforce as the base output unit. [DD372](https://forum.paradoxplaza.com/forum/developer-diary/stellaris-dev-diary-372-modding-pop-groups-and-jobs.1729994/) clarifies the distinction between ordinary and bonus Workforce; ordinary Workforce is capacity-limited, while bonus Workforce can increase output beyond it. Bonus Workforce was absent in the first run; clarification D38 adds it to the core separately from trait import. This is historical 4.0 documentation, not a measurement of the current engine.

Local UI references were read in the installed 4.5.2:

- localisation/english/main_3_l_english.yml:5273 — pop_group_unit has value 100. SHA-256 F4A7F957E72D7258058F8978ACEE32FC9D54419529785AC22D1169F4E25BE492.
- localisation/english/economic_modifiers_l_english.yml:3–4 — output/upkeep labels refer to this unit. SHA-256 99C9241C8E495CAE32D18D5E7E331E049FA71BB0BD9585142A6DB49E7CFF6CBF.

These files were inspected read-only and not added to the repository. Local 4.5.2 saves contain workforce/max_workforce/bonus_workforce and planetary totals, but the example found is not neutral: it includes bonuses and other income sources. Therefore, the save is not used as a golden fixture. The 100 normalization now has documentary support; independent verification inside 4.5.2 remains a separate manual check.

## First measurement

Ryzen 7 5700X, 8 cores/16 threads, about 32 GB RAM; Windows build 26200; .NET 8.0.30, SDK 8.0.424, Release. 20 countries, 600 planets, 3 groups/3 jobs/3 buildings each, one warmup month and 120 measured months. Sequential and parallel runs were separate, with no comparison to Stellaris. One run per mode, not a statistic over repeated runs.

| Mode | p50 per month | p95 | p99 | Allocated over 120 months | Working set after cycle |
|---|---|---|---|---|---|
| Sequential | 5.055 ms | 7.250 ms | 10.085 ms | 108647728 bytes | 62357504 bytes |
| Parallel | 1.038 ms | 2.956 ms | 5.661 ms | 109187144 bytes | 63434752 bytes |

Hashes for both workloads matched: B6D768F80A854A6597D28937BD94954A2CD8A79CD4D9A9DF4D47D2BF2A295F7D. Working set is not peak memory; allocated is total allocation, not retained memory. These figures do not establish a full-game SLA; the workload does not yet include growth or heterogeneous groups. No measurements were taken on Ryzen 5 3600.

## User check

Step-by-step game instructions and a minimal screenshot set: [manual economy comparison](14-manual-economy-comparison.md). One job is enough for the first step; no need to change the game session.

1. Run tests and simulate; check for no FAIL, month count, and reference stocks in simulation.json. You may change jobs or the command in a copy of the scenario; negative values/unknown jobs should produce a clear error.
2. In Stellaris 4.5.2, open an ordinary organic planet without replacements for the three selected jobs. Show one job's tooltip: Pop count, Workforce, base output, and the full modifier list. Do not compare total empire income with one job.
3. Use the tooltip to determine the units for actual output/employment. Check the selected building separately: +0.5 output and upkeep of 2 energy, accounting for modifiers listed by the game.
4. Record screenshots/values and discrepancies in M1-DISPUTES.md; if they differ, clarify normalization and update the specification before implementation. The documented profile has passed automated checks, but manual comparison may reveal unaccounted behavior. Full monthly income requires separate import of population and planetary modifiers.

## doc12 criteria status

Final run from `..`: four suites PASS (parser, economic core, synthetic import, real v4.5.2 definitions), 0 errors. The real gate includes nine neutral cases with 50/100/200 Pop at 1 Workforce/Pop. Against real 4.5.2 files: 120 months, repeated/parallel run PASS, reference hash preserved; balances 1450/2080/2560 verified by a separate assertion. Workload hashes match. Review found and fixed silent acceptance of comparisons instead of assignments in containers and numeric variables; regression tests were added. A Git repository exists, but no commits or branches had been created.

Completion audit: each of the six original criteria below has code/automated tests or a saved measurement, and manual check instructions are prepared. Criterion 2 is supported by new documentary evidence and an integration check, not an assumption about hidden behavior. Scope was not expanded to the full economy. The user has not yet performed manual acceptance.

1. External import and manifest — checked against available 4.5.2.
2. Documented neutral example — checked against the original criterion: 4.5.2 text definitions, the 100 Workforce unit basis in DD370/localization, and an integration gate with nine scenarios. The previous pending status for the units was cleared after obtaining this evidence. Hidden rounding and full runtime economy are not claimed; manual comparison remains the next check, not a completed test.
3. Job changes and recalculation — checked.
4. 120 months and sequential/parallel hash — checked.
5. Baseline time/memory — measured; portability and full workload are not claimed.
6. Automated tests and independent review — completed; manual instructions prepared. Manual acceptance has not yet occurred.
