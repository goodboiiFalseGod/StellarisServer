# M2 — Protocol and server

Current clarification: [player count](M2-PLAYER-COUNT.md); the fixed limit of eight connections has been removed, and the observer sees all player planets.

Handling missing browser responses and background-tab edge cases: [M2-CONNECTION-LIVENESS](M2-CONNECTION-LIVENESS.md).

Transport: ASP.NET Core/.NET 8, no external NuGet packages; WebSocket `/ws`, JSON UTF8 protocol=1, full authorized snapshot. Static HTML/CSS/JS without a bundler. The M2 technical choice does not lock in the final game renderer/protocol. Compression is disabled. HTTP/WS is loopback or trusted LAN only; Internet access requires HTTPS/WSS and a separate deployment review.

## Connection and messages

First message: `{type:"join",protocol:1,name,role:"player"|"observer",token?,joinCode}`. The access code is printed when the process starts; the token is returned only to its own client. Message limit: 16 KiB; first message within 10 seconds. Name length: 1..32 characters. The server checks Origin for browser connections and does not enable CORS. Connection limit: 8; no more than 20 commands/second per connection. The transport limits broken heartbeats/inactive connections; application ping/pong runs every 10 seconds, with a 30-second timeout.

Responses: `{type:"welcome",protocol:1,participantId,token,sessionId}`, `{type:"snapshot",state}`, `{type:"result",result}`, `{type:"error",message}`. A command is `{commandId,type,...arguments}`. ID length is 1..64 characters and IDs are unique per participant. Up to 10000 new commands per participant per process; then requests are rejected with an explanation (memory is bounded and old IDs are not reused). Repeating an ID returns its previous result; different content with the same ID is rejected. ACK means the setting was accepted, not that a month has already been calculated.

Commands: `configure` (traits[],buildings[] in lobby), `ready` (ready), `start`, `setJobs` (planetId,job,count), `pause` (paused), `speed` (speed), `step`, `transferHost` (targetParticipantId), `approveObserver` (targetParticipantId). `ping` is a transport command without an ID; response is `pong`. The connection session determines all permissions, not sender fields. Ownership errors return the same response for an unauthorized and an unknown ID.

## Projections and synchronization

`planet.reportMonth` contains the month of the last report or null. New commands and retries from the client are sent through a shared queue at no more than 8/second, and only after a fresh snapshot; up to 100 unacknowledged commands are allowed, with a visible rejection if the queue is full.

`state`: protocol,sessionId,sequence,phase (lobby/running),month,paused,speed,you (ID),participants[],planets[],catalog,lastError,lastTickMilliseconds. Participants: id,name,role (player/pendingObserver/observer),isHost,connected,afk,ready. Planet economy: id,ownerId,name,population,workforce,jobs (limits),bonusMultipliers,traits[],buildings[],balances,report (last successful month or null). `report` separately provides `produced`, `jobUpkeep`, `buildingUpkeep`, compatible aggregate `upkeep`, and `net`; the aggregate must not be subtracted again by the client. The catalog contains jobs/buildings/traits with Russian labels and sources; it does not expose original scripts or full installation file paths. The public participant list contains no participant settings or tokens.

A player receives only their own planet; an approved observer receives both; pendingObserver receives none. The shared world hash is not sent to clients. Sequence increases on state changes, and the client applies only current snapshots for its sessionId. On return, it receives a complete fresh snapshot. Snapshots are sent no more than 5/second; a single task sends over WebSocket, the queue is bounded, and a slow connection is disconnected or given a newer snapshot in place of a stale one, rather than blocking calculation. A specific command rejection does not disclose another player's content.
