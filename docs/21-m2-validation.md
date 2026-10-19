# M2 — Validation results

October 12, 2026, project `..`. M2 is implemented; the user's short manual scenario was completed, while extended acceptance and the later-added visual upkeep breakdown remain open (see handoff section). [Launch](20-m2-run-and-test.md). Specifications doc17–19 and decisions D39–41 were recorded before implementation; disputed details are documented separately. The interface is a local buildless DOM client following the Sites workflow, with no cloud registration or publication. Measurements below refer to saved runs and were not repeated during this editorial update.

## Automated checks

- Release server build on drive D: 0 errors, 0 warnings.
- Existing Stellaris.Checks: 7 suites PASS, 0 failures, 479.02 ms. Real 4.5.2 import and previous economy were unchanged.
- New Stellaris.SessionChecks: 7 suites PASS, 0 failures. Lobby/readiness, observer, permissions/ownership/unknown IDs, duplicate IDs and changed ID contents, return, AFK after 60 seconds while paused, speed/no catch-up, disconnect and host transfer, old token in a new session, JSON filtering, separate expense sources, and atomic economy rejection.
- Stellaris.ServerChecks on the final server from drive D: PASS. Real HTTP and WebSocket, 2 players and an observer; invalid code, message >16 KiB, and foreign Origin are rejected. Verified real trait/building import, separate JobUpkeep/BuildingUpkeep and compatible total, separate snapshots, foreign commands, identical errors for foreign/unknown IDs, idempotent step, return to the previous planet, and host transfer.
- `node --check app.js`: PASS. Independent static review of session, transport, client, and tests completed; the finding about burst retries was fixed with a shared 8/second queue.

## Browser check

Three independent Chromium in-app browser tabs and a real loopback server:

1. Host and second player joined with different names; observer saw no economy before approval, then both planets, without controls.
2. Selected ingenious and energy grid through the UI and saved settings; both players marked ready; launch started paused at month 0 with no report yet.
3. First step: technicians W=200, B=30, Effective=230; energy output 14.95, expense 2, net=12.95. The second player produced 12 with no building.
4. Changing jobs to 1 produced W=100, B=15, and output 7.475 in the following month; the second player was unchanged.
5. After the second client disconnected, the UI became stale and controls were blocked; the economy continued. Return preserved participant #2, planet, and current month. Reload preserved the login.
6. When the host disconnected while time was running, the second client saw the next month and the former host as offline. Host return and pause worked.
7. The process was stopped and restarted from the final project on D. All previous browser tokens were rejected with a visible error; stale economy was not shown as current.

There were no browser console errors or warnings on the checked client. The dark interface, tables, and paused state were visually checked. LAN between two PCs, separate Edge, narrow screen, and prolonged slow TCP receiver load were not checked. Slow-client protection uses a bounded queue and send timeout; simulation does not wait for network sends, but this does not replace a stress test.

## Handoff

### User's manual run

On October 12, 2026, the user reported that all manual tests in the “Manual scenario” section of doc20 passed. Based on their response, the environment was the current PC, local testing on one computer, Google Chrome. This is the user's result for the short scenario; separate logs, measurements, and browser version were not provided.

The extended doc22 plan (T01–T14) was not performed separately. Its items are not automatically marked PASS because they overlap with the short scenario. LAN between physical PCs, separate Edge, a narrow window, and prolonged load remain untested. This report does not claim full extended acceptance.

Separate job/building upkeep columns were added after that manual run and checked automatically at the HTML/JSON/WebSocket level. The user has not yet manually checked their visual presentation.

Test processes are stopped after checking; the user starts a fresh session using doc20. There are no saves, map, fleets, or combat, per the agreed M2 scope. The Stellaris installation was not changed, and Paradox source files or login secrets were not added to Git. No branches or commits had been created at the time of the first run; the stage was later committed as D42. bin/obj artifacts are ignored by Git.
