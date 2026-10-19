# Player-Count Validation — 14 October 2026

Contract: [player count](M2-PLAYER-COUNT.md). Branch `fix/lobby-player-count`.

- Release builds of the server and network checks: no errors or warnings.
- Session checks: 12 suites, 0 errors. New scenarios for 1/2/3/10/20 players check each player's readiness and connection, separate economies and privacy, late join, reconnect and host transfer.
- Client Node checks: 11 tests, 0 errors; start with 1/2/3/10/20 players and blocking when unready/disconnected; previous connection/reconnect checks preserved.
- Real HTTP/WebSocket with a separate fresh server: 20 players and an observer simultaneously; start, all-planet report, privacy, permissions, retries, reconnect and host transfer to the last player. Existing negative checks for access code, Origin and 16 KiB pass.
- M1 regressions: 7 suites, 0 errors; real 4.5.2 content and traits checked, installation sources unchanged.
- Codex browser: separate real sessions with one and three players start paused at month 0. In the three-player session, the third player's unready state blocks start; once ready, start succeeds.
- Independent diff and contract review: no findings.

Not checked: capacity beyond 20 players, long-duration game, physical LAN. Removing a fixed limit does not guarantee unlimited resources. This validation does not confirm full gameplay/graphics M3.
