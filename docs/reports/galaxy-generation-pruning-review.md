# Independent pruning review

Report ID: `GALAXY-PRUNING-001-independent-review-attempt1`.
Reviewer: `/root/galaxy_review`, independent from the author/root.
Date: October 13, 2026. Branch `feature/galaxy-generation`, baseline `c7bba78`.

Result: **accepted for bounded clean-simple pruning and a separate explicit
prototype connectivity policy; no actionable findings**.
Read the two actual new source files, check file, contract/handoff, and complete frozen
narrow source. Actual Program diff against `c7bba78`: 21 added lines, 0 removed.
Parallel settings/initializer changes are outside this review.

## Reviewed SHA-256

Hashes were checked read-only by the reviewer. Paths are relative to the assigned checkout.

| File | SHA-256 |
|---|---|
| src/Stellaris.Core/Galaxy/HyperlanePruning.cs | 98F79EFB07CBC8ABF437BDA7868D5C250216EE52633EDE3CD32971718A16F5B0 |
| src/Stellaris.Core/Galaxy/HyperlaneConnectivity.cs | 32E1BA9BD30EE55235480380D7EF5CE710664D99BB6A221DA2B74D3C494C0903 |
| tests/Stellaris.GalaxyGenerationChecks/HyperlanePruningChecks.cs | 1D5A9999BCC3BBF88698FE0833AC086303B9AE1AAEA9DA3D9148FE3BB62447E3 |
| tests/Stellaris.GalaxyGenerationChecks/Program.cs | F438C004A0961868A4DAAE8A6612F870218716F5D727CEF181560B8A7A4EF61D |
| docs/reports/galaxy-generation-pruning-contract.md | 7BEE447F488ADEE791462837475E9510F41B099E297D17D4B0676009C4B20EC9 |
| docs/reports/galaxy-generation-pruning-handoff.md | 3AFEB81B6B49723070C3DF592BDD44CAC272AE59E7602B0C88AD58192C8615ED |
| docs/reports/galaxy-generation-pruning-source-9ce99ba7e1e0.md | 9CE99BA7E1E054EEBE2140648E7B58167636D175EF812AD485AC586F5EBC7597 |

## Properties checked

Validated simple M13 input is copied; unsigned handles/groups/weights/options and
selection-counter minimum are checked before working with owned records. N<3 bypasses
options/groups/seeds/pruning. N>=3 preserves both supplied global results even with
empty vectors; A/B independent local RNG state follows the source mix/seed policy.

A/B sorting uses the full `(weight,a,b)` key. A mandatory distance rejection
is strict; random pool starts at physical L, swap-with-last consumes one draw
per actual selection. Rejected prefix is sorted before the degree suffix. Physical holes
preserve remaining fields. Degree equality permits removal; subsequent degrees
are updated. B full permutation consumes NB draws; strict current/target predicate
uses group counters; distance override decrements counters regardless of
target/density maximum. The subsequent combined degree pass does not change counters.

Defensive helper is limited to the clean direct-edge invariant: every survivor has
one-hop reverse adjacency, so the source helper is a no-op. Corrupt/non-simple native
continuations are not simulated. Final edges are A survivors then B survivors,
flags0/1 and directed accepted/rejected group records match the narrow source.
Outer RNG/world objects are not mutated; outputs are immutable.

Connectivity policy is separate from pruning. Disabled preserves disconnected result;
RestoreCandidatesThenNearestV1 first adds shortest discarded candidates,
then deterministic nearest-component spanning pairs. Each repair and cutoff excess
is recorded explicitly; this is a prototype approximation, not a native partition/bridge implementation.

Actual tests include independent RNG/source vectors, physical start=L, mandatory
excess/equality, strict B counters/distance override, degree equality/counter separation,
empty draws, immutable snapshots, invalid/non-simple inputs, N2 raw bypass and explicit
collinear connectivity repair. Genuine density cases check repeat output/ledger,
connected explicit policy, and no mixing with coordinate RNG.

## Validation and limitations

Author-run evidence: Core Release **0 warnings/errors**, **67 PASS** (prior61 +6).
Genuine 200 ordinary with synthetic group mapping i/20/targets3:
density0.5 — 54 pruned + 146 repairs; 1.0 — 62 + 139; 3.0 — 519 + 5.
Reviewer did not rerun build/tests; this is not reviewer-run validation.
Machine reverse engineering and game/UI review were not reproduced.

Source-backed pruning acceptance requires supplied actual groups/targets/counters,
validated simple candidates, and gA/gB. Native object resolution/cache effects,
pathological corruption/OOM continuations, native partitions/bridges/full world,
and bit certification are outside acceptance. Topology marker is metadata, not an actual engine refresh.
Explicit connectivity repair is allowed only as a named approximation; it does not
confirm full-native hyperlane equivalence. Stable integration/push is not authorized.
The reviewer's only write is this Markdown; implementation/tests were unchanged.
