# M2 — Browser Connection-Loss Detection

Defect fix, not a new game mechanic. Clarifies the [protocol](18-m2-protocol.md) and [client](19-m2-client.md). During a blackhole, a WebSocket may remain OPEN, so readyState alone is insufficient to determine command availability.

## Contract

- The client sends a ping every 10 real seconds after joining. The most recent successfully handled response on the current connection (including a pong or a snapshot with the same sequence) confirms liveness; sequence growth is not required while paused. An invalid message does not extend the deadline.
- After 30 seconds without confirmation, the client synchronously invalidates the connection, blocks command sends/retries, marks the last state stale, closes the old socket and starts the existing reconnect flow. Do not wait for close; it may never arrive. Controls remain unavailable until welcome and a fresh snapshot.
- Before welcome, the deadline is also 30 seconds from the start of the attempt. If no saved token exists, do not automatically register again; a manual retry remains available. Login rejection and manual disconnect do not trigger automatic return.
- Use monotonic time. Check via timer and before treating the UI as online/sending a command; visibilitychange and pageshow trigger an immediate check. A background browser may delay timers: do not promise disconnect exactly at 30 seconds while the page is frozen. On resume, check the deadline before any input/send; an old response received after expiry but before the timer runs does not revive the expired connection.
- Old socket handlers (open/message/error/close) do not alter the new connection, UI or queue. Invalidation happens once and does not create multiple reconnect timers. Manual disconnect/login cleanup cancels liveness monitoring.
- Unacknowledged commands retain their original IDs. After a new welcome and snapshot, retry them through the existing queue at 8/second; do not regenerate IDs. Server idempotency remains required: a lost ACK does not mean the command has a new effect.

## Acceptance

Deterministic Node test of the actual app.js with stub DOM/WebSocket/clock and no network delays: OPEN without responses → stale/disabled/reconnect; pong while paused with unchanged sequence keeps the connection alive; a late response from an expired/old socket does not revive it; background resume checks the deadline before sending; manual disconnect does not reconnect; reconnect waits for a snapshot and retries the same commandId; an acknowledged retry is not sent again. This test does not replace existing format/server checks. A genuinely frozen tab/physical network loss require separate manual verification.
