# GALAXY-PRUNING-001 attempt1

October 13, 2026. Pre-code contract; isolated Galaxy slice, without Session/planet/UI.
Input is an accepted CandidateHyperlaneGraphResult, explicit density/max density/maxdistance,
group targets (+0x98)/current (+0x9C) from supplied records, and two independent supplied
uint31 global results gA/gB. Each passes through existing FromSpecifiedSeed((int)g),
that is, counter=Mix32(0x5EA6BA9F-g*0x4AD685B3), seedBase0. Standalone SeedBundleV1 will receive seed streams separately from coordinates
and scripts. Hidden game global seed and literal bit identity are not gates.

## Source-backed pruning

Frozen narrow source9CE99BA7E1E054EEBE2140648E7B58167636D175EF812AD485AC586F5EBC7597
takes precedence over older Source6CABA §11/14: passB uses **group**
current/target, not system adjacency degrees. A/B are sorted by (weight,a,b).
PassA removes over-distance edges, then partial swap-with-last sampling down to target
floor(count*15000/h); selector pool starts at physicalindexL, not 0, then the degree
helper runs when h<max (threshold4 or rounded4h). Rejected prefix is sorted before degree appends.
PassB fully permutes records, removes iff over-distance or
(h<max && current[groupA]>target[groupA] && current[groupB]>target[groupB]),
and each such removal decrements both group counters. Then the degree helper runs on B
against combined survivingA+B; this helper does NOT change group counters. The defensive helper
removes nothing on a normal simple graph. Materialization order is survivorsA thenB,
both directed adjacency entries retain the previous length, flag0 forA/flag1 forB (M13flag0).
Physical vectors preserve count/holes A=-1, while groupaccepted/rejectedlinks are returned
as logical directed records. Native dirty/cache effects are marked with state marker2,
but engineobjects/spatial caches are not mutated. DefensiveInvariant is checked through
exactsurvivor edge membership; a clean simple graph makes the helper one-hop/no-op.

API validates unique undirected edges/valid indices, groupmembership/counters,
settings ranges/checked arithmetic before changing owned copies. Native triangles
are usually simple; duplicates are not silently collapsed. RNG trace records draws/state by
pass; external inputs are immutable. N<3 takes a terminal direct path without RNG/targets/pruning.

## Connectivity and consumer

Pruning alone does not guarantee connectivity; native partitions/bridges are separate.
Return the raw pruned result/components and a separate explicit connectivity
completion policy. A deterministic spanning repair is allowed for the prototype:
first the shortest discarded original candidates, then, if necessary,
the nearest component pairs. A bridge outside the distance cutoff gets a warning. No repair
is hidden or presented as native pruning; full trace/added edges are preserved.
The default strict-native stage does not repair topology on its own.

High-level targets contain only Id/Target: current is taken from the M13 effective counter,
not overwritten by a caller default. The low-level explicit seam accepts the full state.
N>=3 always records the 2 supplied global results, even for empty vectors; local draw count
for B is NB, for A only actual random selections.

Acceptance: source small group7/9 current2,target1: first B edge is removed, second remains;
distance removal decrements counters even when current==target; threshold equality;
partial permutation draw counts; N2 bypass; immutability/invalid snapshot; genuine 200
density0.5/1/3, separate connected prototype result/repeat deterministic/no RNG mixup.
Independent actual-diff/source/contract review before commit.
