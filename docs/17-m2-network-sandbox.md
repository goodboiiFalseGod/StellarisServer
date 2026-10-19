# M2 — Network economy sandbox

Status: approved and implemented on October 12, 2026. The user confirmed a successful short manual run of the launch instructions; individual acceptance scenarios were not checked. This is not a full manual acceptance of M2. Quick index: [protocol](18-m2-protocol.md), [client](19-m2-client.md), [disputed questions](M2-DISPUTES.md), [launch](20-m2-run-and-test.md), [results](21-m2-validation.md).

## Result and scope

Current change to the player count: [any positive number of players](M2-PLAYER-COUNT.md). References to two players below describe the original M2; the seat limit and requirement for exactly two players have been superseded.

A standalone .NET server serves a browser client. Two players control separate empires, one planet each; an additional observer can see both. Local PC and trusted LAN only, with no cloud publication. Existing M1 jobs, buildings, and formulas are used without changing the economic model. There is no map, combat, research, construction queue, disk saves, kick, ban, or static. Restarting the process ends the session without recovery; old tokens become invalid. This is an intermediate M2, not the entirety of the former “Stage 2” in doc10.

In the lobby, a player selects a subset of the three M1 buildings and three traits (ingenious/industrious/agrarian); initial job counts are 2, population is 600, WorkforcePerPop=1, WorkforcePerJob=100, and stocks are 1000 each. Species settings are homogeneous. A selection sets readiness to false. Starting as host requires two connected, ready players; the session starts paused at month 0. New players cannot join after start; returning players and an approved observer are allowed. Job counts may be 0..6 in increments of 0.5; changes take effect the following month. This is a laboratory editor, not construction.

## Controls and lifecycle

The first player becomes host. An observer cannot be host and gets full visibility only after the host approves them in the lobby. Host transfer is to the connected second player. The former host immediately loses host permissions. Disconnecting the host does not change speed/pause and does not automatically transfer permissions. The process remains alive even with no clients connected.

Only the host can pause, set speed, or step the month; stepping is only available while paused. Laboratory speeds are 1, 2, or 4 months per real second, not the pace of the final game. During a pause the calendar stops, but commands and timeouts continue. After disconnect, a participant is immediately offline, then AFK after 60 real seconds; the economy continues. A token returns to the previous participant; a second active connection using the same token is rejected. Secrets are not shared with other clients or written to logs.

Mutations are serialized; the core is not changed concurrently with a calculation. An economy error leaves the month atomically unchanged, pauses the session, and shows a general diagnostic without another player's economic details. Missed months do not accumulate, and there is no catch-up burst after a pause or lengthy calculation.

## Acceptance

Two independent clients complete lobby→start→their own jobs→step/speed→disconnect→return. The observer sees both planets and cannot change the world. The server rejects another player's planet, invalid roles/parameters/phases, and repeats without a duplicate effect. Player snapshots contain no other player's economic data. A slow client does not block simulation. Check timeout during pause, host transfer, host disconnect, and token invalidation after a new process. All previous M1 tests pass. Real WebSocket checks, browser checks, and manual launch instructions are required.
