# M2 — Connection-Monitoring Fix Validation

12 October 2026. Contract recorded before implementation in [M2-CONNECTION-LIVENESS](M2-CONNECTION-LIVENESS.md).

app.js now has a 30-second deadline for incoming responses, immediate invalidation without waiting for close, command blocking/stale state, and deadline checks on page resume and before sending. Old callbacks are ignored; welcome without a snapshot does not allow retries. Unacknowledged commands retain their original IDs.

The targeted test runs the actual app.js in a Node vm with stub DOM/WebSocket/monotonic clock: `node --test tests/client/m2-connection.test.mjs`. This deterministically checks client behavior; it is not browser/network acceptance. Server effects are checked separately by SessionChecks; retaining IDs alone does not prove server idempotency.

Primary agent result: **10/10 PASS**, `node --check app.js` PASS. Independent code review found no material issues; reviewer ran the previous test version 9/9. After review, a Set-based mock of server effects was removed and a test for invalid responses was added; the primary agent reran the final suite. Checks covered blackhole while OPEN, unchanged sequence while paused, old callbacks, delayed timers, manual disconnect, pre-welcome deadline, and retry/ACK of the same commandId.

The primary agent reran regressions: Stellaris.Checks with real 4.5.2 content — 7/7; SessionChecks — 7/7. Two `dotnet run` calls for SessionChecks ended with “build failed” and no diagnostics; a separate `dotnet build --no-restore -c Release` then succeeded (0 warnings/errors), and `--no-build` run passed. The cause of the first failures is unknown; C# sources were not changed.

Real Google Chrome, background timer throttling, physical network loss and integration HTTP/WebSocket ServerChecks were not rerun for this fix. Browser scenarios are required before declaring manual readiness; the earlier short user walkthrough does not cover this new defect.
