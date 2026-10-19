# B02 — current timer applicability

13 October 2026. Read-only assessment at backend owner `babb52b`, reviewed and accepted by integrator chat `01a12d71-f1d2-7e31-b692-caaeebd67ace`. B02 remains an **unconfirmed timing risk**, not a reproduced slowdown or an established contract violation. No production fix, clock change, stronger scheduling policy or timing framework is authorized by this report.

## Source and contract boundary

- `src/Stellaris.Session/Session.cs:197–220`: M2 runs at most one `CalculateMonth` per `Tick`; its next deadline at line 217 uses the caller's pre-calculation `now` plus the configured interval.
- `src/Stellaris.Session/M3/M3GameSession.cs:228–245,425–456`: M3 similarly advances at most one day. `AdvanceDay` prepares its deadline from supplied entry time at line 434 before monthly economy commit, then publishes it at line 441. Its real Stopwatch duration is diagnostic telemetry, not a completion-time scheduling input.
- `src/Stellaris.Server/Program.cs:95–103` samples UTC immediately before the M2 call. `src/Stellaris.Server/M3Server.cs:115–127` samples its monotonic Stopwatch immediately before the M3 call under the runtime gate. Both loops use a 50 ms `PeriodicTimer`.
- `docs/17-m2-network-sandbox.md:17` forbids accumulated missed months/bursts after pause or long calculation. `docs/M3-TIME-AND-MOVEMENT.md:14` and `docs/M3-SESSION-RUNTIME.md:13` forbid missed-interval batching; the latter explicitly permits at most one day per Tick. `docs/M3-SESSION-RUNTIME.md:21` requires potentially failing deadline preparation before economic commit.

If actual calculation duration is at least the configured interval, the entry-relative deadline is already due when that call returns. A pending/coalesced timer notification can therefore start another single calculation without a full post-completion idle interval. This is a conditional static result, not a measured runtime occurrence. There is no missed-step counter, backlogged calculation loop or multiple steps inside one Tick. The explicit one-step/no-backlog requirements are implemented. An exact minimum interval measured from completion is not an explicit current requirement; treating the broad M2 wording as that stronger policy would require a separate decision.

## Existing oracle and limitations

`tests/Stellaris.SessionChecks/Program.cs:120–123` covers late-entry single-month behavior. `tests/Stellaris.M3SessionChecks/Program.cs:271–287` covers monotonic time, delayed single-day behavior, same-time no-repeat and speed deadlines relative to supplied `now`. These deterministic entry-time oracles do not model time spent inside a calculation. A second Tick with a later fake timestamp represents a later call, not proof that the previous calculation itself was slow.

The inspected session/core/test code has no injected completion-time clock or inner-calculation callback for this scenario; duration telemetry uses a real Stopwatch. No sleeps, busy waits, oversized load, private-state reflection or new clock framework were introduced. No tests or browser/physical performance checks were run for this read-only finding. Earlier suite results are not a slow-calculation reproduction.

If completion-relative spacing is later chosen explicitly, first specify the scheduling rule and a bounded deterministic completion-time oracle while preserving M3 atomicity, monotonic validation, pause/manual/speed resets and failure semantics. Do not add a potentially throwing clock read or deadline calculation after economy commit. Until such a requirement is accepted, preserve current scheduling and leave B02 unconfirmed.
