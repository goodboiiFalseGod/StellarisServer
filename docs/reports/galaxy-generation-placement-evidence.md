# Collision/clamp/retry: executable journal before materialization

October 13, 2026. Research partner `01a1359a-86d4-78f3-a4d2-5dda015220f6`
provided new boundary vectors. Targeted clarifications covered near-point rescale,
retry off-by-one, raw-vs-stored collision, and absence of allocation RNG.
Read-only addendum §16.4 was checked as one byte snapshot, SHA
`0DE1D764915C60F51F40B9A4DFBD52724353E28C585E1D7F30994D20CD1BAD89`.
This is static reverse-engineering evidence, not a game run or repeated machine reverse engineering by the author.
The first received E474… checkpoint was supplemented before reading;
the implementation relies on the verified snapshot cited above.

## Collision branches

Compare the current raw candidate with previously accepted stored/clamped positions
in insertion order. Ordinary distance=FM(dx,dx)+FM(dy,dy), threshold=FM(minDist,minDist).
Strict less-than rejects; equality is accepted. MinDist scalar is not sampled.
Independent ordinary vectors minDist1000000:
dx999999→square9999980<threshold10000000 reject;
dx1000000→equality accept; dx1000001→square10000020 accept.

Partner found a separate branch `0x14034A8E0/0x1403090B0/0x140997A50`:
if |dx|<100000 and |dy|<100000, components are FM-multiplied by 10000000 (×100),
and the record scale flag is activated. For minDist>=1000000 the comparator immediately reports collision;
otherwise threshold is fixedSquare(minDist×100), and comparison against scaled distance is also strict.
Independent scaled vectors minDist50000,dy0:
dx49999→scaled4999900, square249990000<threshold250000000 reject;
dx50000→equality accepted. Two ordinary FM operations are not a universal distance helper.
The initial temporary unsupported tiny-domain gate was replaced by the exact branch before acceptance.
Checked extreme int64 arithmetic is an explicitly supported domain; native overflow is not claimed.

## Clamp, retry, and state

Acceptance clamp applies to each coordinate inclusive [−50000000,50000000].
−50000001→−50000000; boundaries unchanged; 50000001→50000000.
Clamp is applied after collision; post-clamp duplicate positions may be a legitimate
result of raw comparisons; there is no additional repair/min-distance check.

`0x140976ED4–0x140976F0C`: retry budget 10000, reset after each acceptance.
On collision, first budget<1→failure; otherwise decrement+retry same acceptedIndex.
10000 rejects still allow the next sampled candidate to be accepted;
failure occurs only on the 10001st collision, whose draws are already consumed.
Allocation/materialization in this loop consumes no local/global RNG
(separately confirmed by partner); failed ordinal is not materialized.

`Replay` processes the supplied immutable candidate snapshot.
`GenerateJournal` combines real shape projection/M01/rotation/candidate RNG with these
operators; returns raw+clamped accepted coordinates, final state, and partial status.
No IDs, handles, objects, native allocation, spatial registration, or relaxation.
No generic allocator is substituted. Composite `GenerateCoordinates` remains gated.

## Checks and independent review

28 own groups PASS, Core Release0 warnings/errors.
Genuine tiny/elliptical seed1 yields 200 accepted pre-relaxation coordinates;
a repeated run produces the same journal and final local state.
Controlled source plan with target2/minDistance100units exhausted:
1 accepted +10001 rejected =10002 candidates, no-arms4draws each,
final counter advanced40008 (lastfailed included).
Replay checks reset after 10000 rejects and acceptance, exhaustion at 10001,
input-exhausted/target-reached distinction, immutable output, and post-clamp duplicates.

`/root/galaxy_review` independently read actual code/checks/§16.4 against the same hash;
accepted with no actionable findings. Reviewer did not rerun tests/native reverse engineering.
Cross-platform native seed-equivalence/FP/backend/runtime is not claimed.

## Related new research

In the same addendum, §20 describes M36 homeworld/startup, and §21 M29/M37 handles/allocators.
This information is retained as external provenance pointers; code for those stages was not added.
Planet assembly, server/Session, and existing fixtures/contracts remain under their respective ownership.
