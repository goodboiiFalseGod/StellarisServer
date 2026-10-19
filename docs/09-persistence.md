# Saves and Recovery

Manual server restart after a failure is acceptable. No more than six in-game months of confirmed progress may be lost. Proposed approach: a full snapshot every three in-game months and a log of accepted commands between snapshots.

A snapshot includes world state, calendar, RNG, order and construction queues, events and timers, visibility and last-known state, empire modes, roles and game rules, engine and protocol versions, and the content manifest. Connection secrets are protected separately from player-accessible replay data.

The log stores accepted gameplay commands, assigned ticks, ordering and required external events such as disconnections, mode transitions and administrative decisions. Player actions alone are insufficient for replay.

## Reliability

Capture a consistent state at a phase boundary; disk writes must not block simulation. A new write becomes current atomically after validation, while the previous working copy is retained. A partially written file is never considered valid.

If saving fails and data loss could exceed six in-game months, show a visible error and follow an agreed policy for stopping progress; silently continuing is not acceptable. Define the policy before implementation.

## Replay and Rollback

Support restoring a snapshot and replaying the log to a selected tick. Replay may initially be an administrative tool. Player-facing access and disclosure rules for hidden data are a separate stage.

Rollback creates a new game-history branch and does not combine later commands from the old timeline with the new simulation. Rollback availability during a live game remains undecided.

## Checks

Checksums match before saving and after restore; replay reproduces the result; a damaged latest snapshot falls back to the previous one; an interrupted write preserves the working copy; AFK, stasis and retreat states are restored; and loading fails clearly when the required content is unavailable.
