# Networking and Visibility

The intermediate M2 uses an agreed simplified contract: JSON/full snapshots, trusted LAN, no deltas. See [doc18](18-m2-protocol.md). Requirements below are future targets, not claims that M2 implements them.

## Protocol

Working direction: binary messages over WebSocket, an initial snapshot and subsequent deltas. Define protocol version, message-size limits, update frequency and compression policy before implementation. Use HTTPS/WSS for remote browser connections; local mode may use a separate launch profile.

Required message types: lobby join, readiness, roles and permissions, start, command, acceptance or rejection, snapshot, delta, event, heartbeat, connection recovery, pause/speed and game end.

A command carries a deduplication ID, type and arguments. The server session identifies the sender. The server checks role, ownership, visibility of required targets and game conditions; client UI does not replace these checks.

## Synchronization

Each update has a sequence number, server tick and state version. A client must not apply a delta without its base state; after a disconnect it receives recovery data or a new snapshot. Queues are bounded: if a client falls too far behind, send a current snapshot or disconnect it with an explanation.

Simulation granularity, network update frequency and rendering frequency are independent. After a command, the client shows that it is pending; it does not report resource spending or victory before the server responds.

## Visibility

An ordinary player receives only their permitted projection. Filter snapshots, deltas, events, errors and replay access. Do not reveal an unknown entity through diagnostic responses.

Observers see everything. This is a privileged role granted by the server. After intel is lost, distinguish disappearance from current visibility, last-known state and actual destruction.

## Acceptance

A repeated command has no duplicate effect; ownership by another player does not grant control; state is correct after reconnection; observers cannot modify the world; intercepted messages contain no hidden data; and a slow client does not stop the server.
