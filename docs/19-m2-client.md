# M2 — Browser client

Current clarification: [player count](M2-PLAYER-COUNT.md); launch is available for one or more players once all connected players are ready.

Clarified stale/timeout/return contract: [M2-CONNECTION-LIVENESS](M2-CONNECTION-LIVENESS.md). WebSocket.OPEN alone does not confirm connectivity.

Russian functional interface, DOM/HTML/CSS and ES modules without npm. Target checks: Chromium/Edge on Windows. Two tabs use independent sessionStorage; reload preserves that tab's token. Tokens and access codes are not shown to other participants or passed in the URL. The disconnect button closes the connection without removing the ability to return; a separate clear-login action warns that the token will be lost. After restart, an obsolete token produces a clear error and offers a new login, without automatically taking another player's seat.

## Screens

- Connection: name, code, player/observer, connect/return; connection state and errors.
- Lobby: participants, roles, readiness, waiting for observer approval; selection of one's own buildings/traits and save button; ready; host start; observer approval and host transfer. Another player's economy settings are not visible.
- Session: month, pause/speed/step, participants and AFK, observer planet selection; stock cards, jobs/Workforce/bonus table, separate columns for output, job upkeep, building upkeep, total upkeep, and net flow, selected buildings and traits/bonus sources. The client displays server values and does not recalculate total upkeep. The latest report is labeled with its month; before the first calculation, show “not yet calculated,” not a fake zero.
- Commands show pending, ACK, or rejection. No local income/balance calculation; numeric formatting is allowed. On disconnect, controls are unavailable and the last state is clearly marked stale. Automatic return uses bounded delay and does not register a new participant; unacknowledged commands may be retried with the same ID, not blindly assigned a new ID.

## Verification and design

Calm dark palette, readable tables, visible focus/disabled/error states; adapts to a narrow window without clipping controls. All participant data is rendered through textContent, not HTML. Stable data-testid values for connection, readiness, start, month, planet, jobs, step, pause, observer, and errors. Game assets, GPU rendering, and keyboard shortcuts are outside M2. Checking two players and an observer complements transport tests; it does not replace them.
