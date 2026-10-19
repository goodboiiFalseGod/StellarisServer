# M2 — Manual acceptance plan

Goal: check the network economy sandbox with two players and an observer. This is a plan, not a report of completed work. Requirements: [M2](17-m2-network-sandbox.md), [client](19-m2-client.md); launch commands: [instructions](20-m2-run-and-test.md). Questions and feedback form: [separate document](23-m2-test-questions.md).

## Preparation

Allow 30–45 minutes for the main run and another 20 minutes for stability checks. Record commit (`git rev-parse --short HEAD`), OS, browser, and server/client hardware. Use a fresh server and new, non-duplicated tabs: A — first player/host, B — second player, O — observer. Start with localhost, then repeat network checks on a trusted LAN. Do not run ServerChecks against this manual session: the test occupies seats and changes the world.

For each row, mark PASS / FAIL / BLOCKED / NOT CHECKED. Expected numbers below are exact before UI formatting; rounded display must not change the calculation. Initial stock of each resource is 1000. Check restart last: it destroys the current session.

## Main run

| ID | Actions | Expected result |
|---|---|---|
| T01 | Sign in with an invalid code, then correctly connect A and B. Connect O. | Invalid sign-in is explicitly rejected; two distinct players, A is host. O sees no planets before approval. |
| T02 | A selects only “Изобретательные” and “Энергосеть” and saves. B leaves settings empty. Try to start before both are ready. Then mark ready and save A's settings again. | Early start is unavailable. Saving configuration clears A's readiness. The other player cannot see A's configuration. |
| T03 | A approves O. Both players are ready; A starts. | Month 0, paused, no second world starts. Report has not yet been calculated. A and B see only their own planet; O can see both. |
| T04 | A steps one month. Compare A, B, and O. | Month 1 for everyone. A: technicians W=200, B=30, effective Workforce=230; energy output 14.95, building upkeep 2, net flow 12.95, stock 1012.95. B: energy output 12, stock 1012. Both have minerals 1008 and food 1012. |
| T05 | A changes technician jobs from 2 to 1, applies, then steps. | Before the step, report remains labeled with the previous month. In month 2 for A: W=100, B=15, output 7.475, net flow 5.475; energy 1018.425. B energy 1024. Both have minerals 1016 and food 1024. |
| T06 | A sets technicians to 0.5, applies and steps; then repeats with 0. | At 0.5: W=50, B=7.5, output 3.7375. At 0: W=0, B=0, output 0. Energy grid consumes 2 in both cases. B is unaffected. |
| T07 | O switches planets; B checks whether global controls are available. | O sees the relevant reports for both planets but cannot edit them. B cannot change pause, speed, step, or another player's planet. |
| T08 | While paused, wait 10 seconds. Restore A's two technician jobs; resume time, cycle through speeds 1/2/4, then pause. | Month does not change while paused. Speeds produce approximately 1/2/4 months per second, accounting for screen refresh; stepping is unavailable while time runs. There is no catch-up burst after pause. |

## Connectivity and permissions

| ID | Actions | Expected result |
|---|---|---|
| T09 | While paused, B clicks “Отключиться”. Wait just over 60 seconds while observing through A. Step twice; return B using the previous login. | First offline, then AFK even while paused. Controls are blocked in the disconnected client and its state is marked stale. B's economy continues; return restores the same empire and current month without a new participant. |
| T10 | Reload B's tab and click “Вернуться”. Separately, briefly interrupt B's network if safe and the server stays connected. | Reload preserves login. On network interruption there is a clear status and automatic return after recovery; no extra empire or obvious duplicate command application. Network interruption detection need not be instantaneous. |
| T11 | While paused, disconnect and return A. Then resume time, disconnect A again, and return. | In the first case pause remains; in the second, months continue. No replacement host is assigned automatically. Returning A retains permissions. |
| T12 | A transfers permissions to connected B; check both players' controls. | B gets permissions immediately; A loses them but can still manage their own economy. O cannot become host. |
| T13 | Leave time running for 20 minutes, periodically change one's own jobs and reconnect B. Watch the terminal and UI. | No crash, stalled simulation, duplicate participants, growing command queue, or unexplained month mismatch. Record CPU/RAM and latency; this test does not establish a numeric SLA. |
| T14 | Save results, stop the server with Ctrl+C, and restart. Try to return using the old login. Clear the login and connect again. | Old token is rejected; old world is not restored. Clearing login warns that login will be lost. New login creates a new lobby. |

## Compatibility and additional checks

- On a second physical PC on the LAN, repeat T01–T05 and T09–T12. Do not expose the port to the Internet. If connection fails, record address, error text, and firewall state; do not automatically classify it as a simulation error.
- In Edge on Windows, check login, stepping, jobs, and return. In a narrow window, check that buttons, tables, and errors are accessible; scrolling is acceptable, inaccessible controls are defects.
- Run automated regression from doc20 separately: M1 core with real content, session, HTTP/WebSocket. Foreign-planet ID substitution, duplicate commandId, and network-data filtering are checked there; the absence of a UI button does not prove protocol security.
- A slow TCP client and prolonged load are not considered checked by an ordinary manual run. Comparison with running Stellaris is a separate doc14 check, not an M2 result.

## Acceptance outcome

Critical defects include crashes, incorrect economy, changes to another player's world, disclosure of another player's data, inability to return to a live session, duplicate command execution, or permission violations. If one occurs, preserve evidence and do not declare M2 accepted. Full manual acceptance requires the main run and LAN; if a second PC is unavailable, explicitly leave that condition untested.

Not M2 bugs: no map, fleets, research, construction, saves, kick/ban, or static; jobs are a laboratory editor, and speeds are months/sec. A deficit atomically stops the month instead of implementing full vanilla mechanics. These limits do not mean the final game will omit those features.
