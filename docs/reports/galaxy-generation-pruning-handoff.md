# GALAXY-PRUNING-001 attempt1 — accepted handoff

Base c7bba78, feature/galaxy-generation, October 13, 2026. Author/root.
Source9CE99BA7E1E054EEBE2140648E7B58167636D175EF812AD485AC586F5EBC7597
byte-exact source is preserved in galaxy-generation-pruning-source-9ce99ba7e1e0.md.
No Session/planet/shared/UI/installation modifications.

## Actual stage

HyperlanePruning: supplied gA/gB uint31→existingFromSpecifiedSeed; 2 global draw site
metadata even for empty A/B, separate local states and draw ledger. A sorts/distances/selector
start=L/rejected sorted prefix/degree>=T; B full permutation/group current>target/distance
decrements; combined scratch degree pass without changing group-selection counter. Physical
vectors preserve holes, A-survivors flag0 then B-survivors flag1, reciprocal adjacency,
accepted/rejected directed group bookkeeping. Direct-survivor membership checks
native defensive one-hop/no-op normal invariant; corrupt branch produces typed failure. SmallN2
returns raw direct adjacency without reading targets/options/seeds. Input immutable/output owned.

HyperlaneConnectivity is a separate explicit policy: Disabled or
RestoreCandidatesThenNearestV1. Native-pruned output is preserved separately; restoration of
discarded candidates, then minimum-spanning nearest pairs, is accompanied by a record per repair
and an exceeding-cutoff flag. No RNG, hidden jitter, or claim of a native partition bridge.

## Frozen files/SHA256

| File | SHA256 |
|---|---|
| src/Stellaris.Core/Galaxy/HyperlanePruning.cs | 98F79EFB07CBC8ABF437BDA7868D5C250216EE52633EDE3CD32971718A16F5B0 |
| src/Stellaris.Core/Galaxy/HyperlaneConnectivity.cs | 32E1BA9BD30EE55235480380D7EF5CE710664D99BB6A221DA2B74D3C494C0903 |
| tests/Stellaris.GalaxyGenerationChecks/HyperlanePruningChecks.cs | 1D5A9999BCC3BBF88698FE0833AC086303B9AE1AAEA9DA3D9148FE3BB62447E3 |
| tests/Stellaris.GalaxyGenerationChecks/Program.cs | F438C004A0961868A4DAAE8A6612F870218716F5D727CEF181560B8A7A4EF61D |
| docs/reports/galaxy-generation-pruning-contract.md | 7BEE447F488ADEE791462837475E9510F41B099E297D17D4B0676009C4B20EC9 |

Program actual diff against accepted c7bba78: 21 added/0 removed lines (2 wire calls +
genuine check). No previous module edits. Release Core 0 warnings/0 errors; 67 PASS
(previous 61 + 4 pure pruning + 1 backend connectivity + 1 genuine 3-density check). Author-run.
Log artifacts/galaxy-integration-ready/pruning-checks.txt.

Checks: g0/g1 seed/first draws, A physical 2/10 invalid, start L protected 0, mandatory>R
no local draws, B 7/9 current2 target1 removes first only, distance override/equality,
degree equality 4→3 and grouped-selection counter unchanged after degree pass, empty vectors
2 global metadata/0 local draws, non-simple/invalid state/high bit/density errors, snapshots
immutable, N2 raw length 1414213/no RNG, collinear nearest repair/disabled disconnected.

Genuine tinyellipse seed1, 200 ordinary, supplied test group mapping i/20/target3:

| Density | Source pruned edges | Explicit repairs | Connected edges |
|---|---:|---:|---:|
|0.5|54|146|200|
|1.0|62|139|201|
|3.0|519|5|524|

All graphs connected after explicit policy; B draws 519; repeated outputs/ledger matched;
coordinate/script RNG were not used. Test mapping is not native partitions and is not default
production group assignment. Stage is ready for a world producer, but homes/content/startup/
partitions/lobby handoff are not ready. Final independent review accepted without findings;
existing /root/galaxy_review, GALAXY-PRUNING-001-independent-review-attempt1,
[review](galaxy-generation-pruning-review.md), SHA256
9971A3BDB7508D751942FE370C37E9740D3B9B604E32847CAE77A8324820DBA8.
Reviewer did not rerun tests; 67 PASS are author-run. Source/check unchanged after review;
only status/handoff was updated. Large full-vanilla pipeline is not claimed. Third-party/binaries remain ignored.
