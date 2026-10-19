# GALAXY-SYSTEM-MATERIALIZATION-001 attempt1 — handoff

October 13, 2026. **Stage completed; frozen code delta accepted by independent review**.
The user's direct request was completed in a separate galaxy checkout;
HEAD `5a67a51a2e28609cfdcf861a1b25fc0bd840b573`, feature/galaxy-generation.
No commits/push/packages/native game memory/installation writes.
Session/server/client/planet prototype and existing fixtures/shared contracts were unchanged.

## Alignment and source

Reverse partner `01a1359a-86d4-78f3-a4d2-5dda015220f6` clarified system-specific ctor/
registration/failure, unsigned highest/capacity, and exact free-link sentinel/pop.
Latest immutable local analytical snapshot:
[source-7c4c181256fd](galaxy-generation-system-materialization-source-7c4c181256fd.md),
SHA `7C4C181256FD94B50866BB84F985F2DDD258BE846313DBC7A424A7DB6ED359FE`, §16.4–16.4.1.
Previous 091265DE/244EB6 checkpoints were preserved separately and not overwritten;
external outputs were left untouched. These are research Markdown files, not game scripts/assets/bytes.
[Pre-code contract](galaxy-generation-system-materialization-contract.md).

## Frozen files and SHA-256

| File | SHA-256 |
|---|---|
| src/Stellaris.Core/Galaxy/NativeSystemHandles.cs | 983680A3D49F05776AB4C8E493E8B9AF9BE415C1B8A31C3DD8B2999153A3282C |
| src/Stellaris.Core/Galaxy/SystemAllocatorSnapshot.cs | AF608D7184A44BB53A0370A04ECBF3255EE870BBD03B70FDAA8C9C7CFE0D4E41 |
| src/Stellaris.Core/Galaxy/NativeSystemObject.cs | 435A044A24E14F5BB958384838275BDAFCDD412F6534C80BF8CDCBC1958B31B8 |
| src/Stellaris.Core/Galaxy/SystemMaterialization.cs | 771F57BFFD24EC4006AD734132746CCCBB23EBDC7380D5CED2BBEC566B5D3E71 |
| tests/Stellaris.GalaxyGenerationChecks/Program.cs | EE206C0B1B12D219D69A063D523BDBB9B4175A746F3B12A471385BDB581C0AF2 |
| docs/reports/galaxy-generation-system-materialization-contract.md | A8B1A1F7BEE5D32524C91AE18E9F2001FE4517BDDE7A2911CC4BA6FAA5B820FF |

13 previous source .cs files in Galaxy/Content/Galaxy were rechecked against entry SHA baseline: unchanged.
Old adapter evidence/review/contract were not rewritten. Program adds 179 lines,
without removing prior checks; exact baseline for actual delta is saved in ignored
`tests/Stellaris.GalaxyGenerationChecks/bin/review-baselines/materialization-program-ec5a7231.cs`,
SHA `EC5A72314C1B3648BB4F2A3C667BA1D1B8BD2A8B18773302640D2A3663F19FDF`.

## Result and safe success domain

Generate/Replay creates actual isolated managed system objects inline on
acceptance, using the source-known constructor subset, unsigned full handles, raw→clamped positions,
arm, explicit opaque name/context, and generation order. Registry stores the same object references.
Initial immutable snapshot: capacity/free-head/links/object words/chunks/flags,
active/highest/dirty/world fields, and resolver binding. Fresh-only support; initial state
is not derived from seed or taken from planet assembly.
Slot registration precedes ctor and handle write; then full-handle same-object lookup,
payload/world fields, position/sentinel/clamp, unconditional normal helper no-op, arm, append.
Constructor does not simulate unknown star/planet/content fields. Final spatial refresh is pending.

Preflight checks all required slots/storage and the same resolver before draws/IDs; invalid/nonfresh,
stop/grow/chunk/unsafe binding return a typed result without a fake sentinel system. This is a safe API-domain
rejection, not an exact simulation of native failure continuation. Native late resolver failure already
consumes an ID; the defensive post-registration branch returns a consumed-ID error and updated snapshot,
without simulating sentinel mutation/append. It is unreachable through the normal owned table; external
native resolver/OOM/free/grow/caches are not executed or claimed as runtime-covered.

## Author-run checks

`dotnet build src/Stellaris.Core/Stellaris.Core.csproj -c Release --no-restore`
→ **0 warnings/errors**.
`dotnet run --project tests/Stellaris.GalaxyGenerationChecks/Stellaris.GalaxyGenerationChecks.csproj -c Release --no-restore -- <installation-root>`
→ **39 PASS**: previous 33 + 6 materialization groups.

Independent fresh0/1/1023/1024, reuse-generation arithmetic, unsigned high-bit/sentinel,
highest/capacity vectors; ctor/default/payload/position/helper/arm/object identity,
chunk boundary1023/1024, corrupt chain/missing records/flags/storage/resolver/grow/nonfresh gates,
retry10001 no-ID rejects, prefix/input exhaustion, and seeded local draw ledger.
Genuine tiny/elliptical seed1 creates 200 objects with handles 0..199; positions/raw positions
and final RNG state match the existing journal; import 5 scenarios/10 shapes/8 hashed files.
Whitespace in new source/check files was checked without errors. Native backend/full seed is not claimed.

## Independent review

After freeze the existing `/root/galaxy_review` checked only the new actual delta,
frozen local source, and exact Program baseline. Result: **accepted in the bounded fresh-manager
domain, no actionable findings**. Reviewer personally saved
[galaxy-generation-system-materialization-review.md](galaxy-generation-system-materialization-review.md),
ID `GALAXY-SYSTEM-MATERIALIZATION-001-independent-review-attempt1`,
SHA `CAAF06070B69A23292660679370E2D2902B4B60406EDC59ABFD1807DE794C7A5`.
Their report includes full hashes for actual source/check/contract/evidence and handoff before this
documentation-only record of acceptance. Code/check/contract/source snapshot were unchanged after freeze.
Reviewer did not rerun tests/native reverse; 39 PASS/Core 0/0 are author-run evidence.
The author does not claim self-acceptance. Do not change code/check hashes without repair/re-review.
No stable/M3 integration. A complete galaxy is not a gate for the planet prototype.
