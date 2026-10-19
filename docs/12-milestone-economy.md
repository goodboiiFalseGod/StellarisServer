# M1 — Reproducible Planet Economy

Status: technical implementation and automated acceptance of the limited M1 are complete; results are in doc13. The user authorized autonomous M1 implementation, builds and automated tests. The user performs manual verification after the report. Disputed questions are tracked in the [log](M1-DISPUTES.md).

## Scope

Console server application on .NET 8, with no third-party packages, networking or UI. Three selected ordinary jobs, one planet, population groups, Workforce allocation, production and upkeep. Browser, full Script VM, combat, daily calendar, timed construction and AI are out of scope.

Use the installation updated by the user. The previous 3.5.3 version is not a reference for the modern economy. Development of an independent core was permitted during the update; import was deferred until the file set stabilized.

### Researched 4.5.2 Profile

The installation was updated to v4.5.2. The neutral projection reads technician/miner/farmer from common/pop_jobs/03_worker_jobs.txt, building_energy_grid/building_mineral_purification_plant/building_food_processing_facility from common/buildings/03_resource_buildings.txt, and resources and the economic_categories hierarchy. Active structural fields and numeric tables accept only the `=` operator; comparisons there cause rejection.

The definitions specify base output of 6 energy, 4 minerals and 6 food per occupied job unit. The selected buildings add 0.5 to the corresponding output and consume 2 energy each. These are values from text definitions, not measurements of actual planetary income with a full modifier stack.

WorkforcePerJob is an explicit required laboratory parameter. The demonstration scenario uses 100, justified by the sources below rather than inferred from POP_BULK_UNIT_SIZE. 600 Workforce fills two normalized units for each of the three jobs: output is 13/9/13 (energy/minerals/food), energy upkeep is 6, and net income is 7/9/13. Before month 61, technician employment is reduced to 1: energy income is 0.5, and unemployed Workforce is 100. With initial stocks of 1,000 each, expected balances after 120 months are energy 1,450, minerals 2,080, food 2,560. These figures verify the documented neutral laboratory contract; direct correspondence to current runtime, hidden rounding and full final incomes remain for manual verification.

Population upkeep is deliberately excluded from the projection, as are stability/habitability/happiness, growth and expanded group assignment. Do not treat it as a completed full planetary economy.

Additional basis for normalization: the official [Dev Diary #370, 13 February 2025](https://store.steampowered.com/news/posts/?appids=281990&enddate=1742829055&feed=steam_community_announcements), in its Workforce section, relates output to a block of 100 Workforce. This is a historical description of 4.0, not a direct measurement of 4.5.2. [Dev Diary #372](https://forum.paradoxplaza.com/forum/developer-diary/stellaris-dev-diary-372-modding-pop-groups-and-jobs.1729994/) separately distinguishes ordinary and bonus Workforce; the latter should not consume additional job slots. The neutral M1 import uses zero bonus; the core was extended with an explicit bonus input under D38/doc15.

An additional integration gate runs explicitly with an external game-root and requires the researched v4.5.2 definitions, not rewritten fixtures. Check 50/100/200 Workforce for one selected job with sufficient slots and its corresponding building: output is proportional to Workforce/100; building upkeep remains 2 energy regardless of employment. Technician: 3.25/6.5/13 energy; miner: 2.25/4.5/9 minerals; farmer: 3.25/6.5/13 food. Initial stocks are sufficient for upkeep. This gate checks import→normalization→core and source immutability; it is not a measurement inside the original engine. A version or numeric mismatch must fail the gate explicitly.

## Independent Contracts

- The parser preserves ordered repeated elements, scalar/block structure, comparison operators and file/line coordinates. It supports comments, quoted strings, @ variables and lists. Invalid structure raises an exception with its location instead of returning a partial result.
- Import creates a SHA-256 manifest of files actually used and a coverage report. Original files are not copied into Git. Version and hashes are checked before and after import; content changes during import must cause rejection.
- The economy core uses normalized numeric definitions: resources, jobs, Workforce cost per occupied unit, output/upkeep, categories, buildings and numeric modifiers. Modern game unit normalization is documented after researching the files.
- Specialized ECS: stable generation-bearing IDs, separate country/planet/group data, and ownership indexes. This is minimal storage for a specific scenario, not a general-purpose ECS framework.
- Groups have population and available labor. In M1 they are homogeneous ordinary workers without traits, political or living-condition modifiers. Splitting them supports aggregation checks; no claim is made about the full semantics of group formation.
- Each month, needs/allocation and income are calculated first; ranges/shortages are then checked and resource changes applied atomically. A rejection must not leave a partially changed world.
- The job-count command validates the ID, a finite nonnegative quantity, and that the job exists. Changes affect the next calculation; the cache must not remain stale.
- Use decimal for reference arithmetic and stable iteration order. Rounding boundaries and formulas are specified after content research. This is a technical foundation, not a claim about Clausewitz numbers.
- The sequential version is the reference. Parallel planet calculation is allowed only when results match and application is atomic.

## Selecting the Supported Scenario

### Independent Reference Contract

Neutral allocation for the test core: job slots are traversed in a fixed definition order; group labor is consumed no more than once. With no competing traits or settings, allocation is equivalent to filling with identical workers. Under labor shortage this is an **experimental policy**, not a claim about vanilla priority.

Clarification D38: output and upkeep use effective Workforce (ordinary + bonus), with output and cost modifiers kept separate. Workforce first scales the base plus its additions, then the multiplier for the corresponding operation is applied. [Exact contract](15-workforce-and-job-upkeep.md). Parent add does not inherit; this is documented in the game's economic categories. Direct runtime comparison remains a separate check.

Resource balances are capped at their imported storage limits. A resource shortage in limited M1 explicitly rejects the entire monthly transaction: vanilla shortage systems are not yet implemented. This is a test-laboratory limitation, not a change to the target game's rules. Required recurring upkeep is specified explicitly; do not silently calculate population living costs without the corresponding importer.

Storage separates EntityIds for countries, planets and groups; a reference of the wrong type is rejected. Entity deletion and ID reuse are outside M1, but a generation is reserved in the identifier. The 120-month check compares the full deterministic state hash, not just one balance.

Every selected branch of the economic calculation must have known semantics. Non-executed areas (graphics, AI, story, eligibility outside the fixed scenario) are explicitly described in the projection report. An unknown field in an executed table, resource, active trigger or modifier causes an error.

Extracting the first resources/produces entry must not be called a complete job import. Supported and excluded branches are listed separately. If modern semantics require clarification, the computational experiment is marked preliminary; the question remains open and vanilla parity is not claimed.

## Acceptance

1. Jobs and related definitions are read from external files in the selected stable installation.
2. Agreed or directly documented neutral in-game examples of production/upkeep are reproduced; sources and limitations are stated.
3. Changing available jobs recalculates allocation and results without a stale cache.
4. Repeating the same input commands for 120 months yields the same world hash. Parallel execution is compared with the reference if implemented.
5. Timing/memory measurements exist for a scaled scenario, with hardware and workload parameters. The first measurement does not set an unjustified threshold.
6. Error, parser, arithmetic, entity ownership and atomicity tests exist, along with independent review and manual verification instructions.

Each criterion receives its actual status in the final report. Waiting for an external reference is not presented as a completed criterion. Required formulas and specific control values are added to this file before the corresponding implementation.
