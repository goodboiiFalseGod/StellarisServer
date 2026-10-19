# Lobby Membership and Host Permissions

User instruction, 16 October 2026. Branch from master `1bca87c`.

- Before start (phase=lobby), a closed connection or server-detected connection loss removes the participant from the list. The old token no longer restores the removed slot; a new join creates a new unique ID. Undetected disconnects use the existing network timeout.
- When the host leaves, the connected player with the lowest ID becomes host. If no players remain, there is no host; the next player to join becomes host. An observer never becomes host.
- A change in player membership cancels a prepared start/candidate, increments the revision and clears readiness. An observer leaving does not change game membership.
- A removed player leaves no economic entities for the future game. Remaining players' presets/settings are retained; economy IDs may be recreated before start, so the client uses the current snapshot. An observer view of the removed player resets to general view.
- Only a connected host in the lobby can remove another participant (player or observer) with `kickParticipant` and `targetParticipantId`. Server rejects self-target, unknown target and kick after start. Command fields are strictly validated and retries are idempotent. This removes a slot; it is not a permanent ban.
- The excluded connection is closed with a clear message; old commands/tokens are invalid, and automatic retry does not restore the slot. The host UI shows a kick button next to other participants only in the lobby.
- Disconnect after start preserves the slot/world and existing reconnect behavior. Other current-game and host-transfer rules are unchanged.

Acceptance: player/host departure, last player and new join, no reused IDs after removal, host kick and rejection of non-host/self/post-start kick, inactive tokens/connections, candidate/readiness reset, observer and working reconnect after start. Session, strict wire codec, real WebSocket and client checks; independent diff review.
