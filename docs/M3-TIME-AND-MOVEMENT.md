# M3 — time and movement

Status: contract for future implementation; M01–M03 and temporary profile N01 are approved. Reference is 4.5.2; parameters derived from files are distinguished from approved M3 deviations. This does not describe an already-running server. Related: [slice](M3-GRAPHICAL-SLICE.md), [protocol](M3-SPATIAL-PROTOCOL.md), [remaining questions](M3-DISPUTES.md).

## Daily simulation

`elapsedDay=0` is 2200.01.01. The 30th successful step is 2200.02.01; the 360th is 2201.01.01. All months have 30 days, with 12 months per year; do not use Gregorian leap-year rules. The date is derived from integer elapsedDay, not the OS clock. `completedMonths=floor(elapsedDay/30)`; the M1 monthly economy counter increments only when a month closes successfully.

1. Commands and timer run through one serialized state owner. An accepted command receives commandSequence; the server determines ordering for simultaneous arrivals, not thread order. Commands/real-time timeouts continue while paused.
2. A daily step uses the committed state after preceding commands. Movement for one game day is calculated in staged state, followed by the new date. At a new month boundary, the existing M1 core calculates both economies once using the latest accepted vacancy settings.
3. Publishing the date, movement, and monthly economy is atomic. An error cannot leave fleets advanced with the old date/balances; the entire day remains unchanged, the session is paused, and safe diagnostics are shown. Commands accepted before the step attempt are retained. Sequence increases for the published error/pause; tick and elapsedDay do not. Detailed server errors, even without tokens/paths, are not sent to another client.
4. Success increments elapsedDay/tick and sequence. The first reportMonth remains null until the first month closes; it is then 1 on 2200.02.01. Displayed calendar month and reportMonth are separate fields.

Speeds 1/2/4 mean game days per real second. The monotonic real-time timer is independent of system clock changes. Pause, speed changes, and stepDay reset the deadline; missed intervals are not caught up in a batch. `stepDay` is host-only and only while paused. 30 steps produce exactly one monthly calculation; commands between steps are applied at the next relevant phase. AFK/heartbeat remain real seconds, not game days.

## Coordinates and fleet state

Galaxy and local system coordinates are separate spaces. Local navigation is two-dimensional `(x,z)` in the plane, with `y=0`; Three renders 3D models above it. Fleet position is server-authoritative; ship scale and decorative offsets do not change distances. Fleet ID does not depend on drawable/instance number.

Phases: `idle`, `movingLocal`, `approachingExit`, `charging`, `inTransit`. M3 uses three identical corvettes; there is no acceleration, angular-velocity limit, collision, or physical formation. Vectors and durations must be finite; zero distance completes without division by zero. Numeric format and rounding are described below; system radius and exit coordinates are specified in M3-WORLD-FIXTURE and must not be taken from a model bbox.

When moving to a point, remaining distance decreases by `speedUnitsPerDay × dt`; the fleet cannot overshoot the target. Within one day, the remaining dt is consumed sequentially across phases; zero-duration phases must not create an infinite loop. After the final arrival, the fleet is idle. The server sends current state; the client does not run its own pathfinding/simulation. Coordinates/distances/fractional dt/phase times use finite binary64 values; position epsilon is 10^-6 local units, and a completed position is set exactly to the target. `phaseStartedDay`/`phaseEndsDay` are fractional days from the start, not calendar day numbers; elapsedDay/tick remain integers. Bitwise identity across platforms is not claimed for this numeric profile without a separate test.

## Orders and cancellation

- `moveFleetToPoint`: own fleet in the current system only, with a finite point inside the permitted area. Replaces the active local order; starts from the actual position, without teleportation. During charging, cancels preparation and starts local movement; during inTransit, it is rejected because there is no “current system” there.
- `moveFleetToSystem`: visible, permitted systems only. The server chooses the shortest path by number of hyperlanes in the graph available to the participant; hidden intermediate systems/edges are not used. Ties are resolved lexicographically by stable IDs. An unreachable target is rejected without changing the previous order. The route is rechecked before entering a new segment.
- Travel to the exit is physical. Upon arrival, charging begins; after preparation completes, inTransit begins; when transit completes, the fleet appears at the corresponding destination entrance. Transit is not ordinary retreat; M3 has no emergency-retreat mechanics.
- `stopFleet` before inTransit immediately cancels the order/preparation and sets idle at the current position. During inTransit, it stores a pending stop: transit completes, then the fleet idles at the entrance.
- Replacing an inter-system order during inTransit stores only the last accepted pending target; a new route is built after arrival. This does not permit cancellation of a segment already underway. Stop replaces the pending target; a new valid target replaces pending stop. ACK confirms acceptance, not arrival.
- A target in the current system outside inTransit cancels the remaining inter-system route and leaves the fleet idle at its current position. During inTransit, destinationSystemId is the target of the current transit; such an order means stop after arrival. Other targets are handled as pending destinations.

Pause freezes all phases. Disconnect/AFK does not cancel an accepted order; no new strategic decisions are made. M3 has no war, closed borders, FTL blocking, or automatic combat reactions. If a target loses visibility, a new order is rejected/the old route safely stops at the nearest available phase boundary; errors do not reveal the hidden object. The exact test for this case is recorded with projections.

## Numeric sources and approved profile

Local 4.5.2: `common/scripted_variables/03_scripted_variables_ships.txt:13` sets speed_very_fast=160; `common/ship_sizes/00_ship_sizes.txt:122` uses it for corvette. `common/defines/00_defines.txt:1783` sets SHIP_SPEED_SCALE=0.045 and explains units/day at scale=1. Derived for an unmodified baseline: 7.2 units/day. This is not the speed of an arbitrary design with engines/modifiers.

In the same file, HYPERLANE_WINDUP=150 micro updates at 10/day → 15 days without modifiers; WINDDOWN=0. HYPERDRIVE_INTERSTELLAR_TRAVEL_SPEED=1.0 does not disclose the distance/time formula. Do not interpret it as “one day” or “one hyperlane jump per day.” SHIP_THRUSTER_1 (`common/component_templates/00_utilities_thrusters.txt:83`) provides no speed modifier; HYPER_DRIVE_1 (`00_utilities_drives.txt:96–134`) provides no windup modifier. The approved test profile is limited to these, without other speed effects; it is not a full design import. The user closed [N01](M3-QUESTIONS.md): for M3, transit itself takes a fixed 5 days, system radius is 150, and exits/entrances are at 140. This is an authorized temporary substitute for the unknown formula, not a claim about original-game behavior.

### Rechecking the sources

Sources rechecked October 12, 2026 (paths relative to the 4.5.2 installation):

| Parameter | Value | Source |
|---|---|---|
| Corvette base speed | `max_speed=@speed_very_fast=160` | `common/ship_sizes/00_ship_sizes.txt:122`, `common/scripted_variables/03_scripted_variables_ships.txt:13` |
| Speed scale | `SHIP_SPEED_SCALE=0.045` | `common/defines/00_defines.txt:1783`; comment relates scripted speed to units/day at scale=1 |
| Wind-up | `HYPERLANE_WINDUP=150`, 10 micro updates/day → 15 days | `common/defines/00_defines.txt:1804` |
| Wind-down | `HYPERLANE_WINDDOWN=0` micro updates | same file, line 1805 |
| Interstellar travel speed | `HYPERDRIVE_INTERSTELLAR_TRAVEL_SPEED=1.0` | same file, line 1806; the text contains no complete duration formula |
| Corvette acceleration / rotation | `0.35` / `0.1` | `common/ship_sizes/00_ship_sizes.txt:123–124`; excluded by approved M01, units not recovered |
| Action arrival threshold | `MOVE_TO_ACTION_THRESHOLD=5.0` | `common/defines/00_defines.txt:1802`; M3 sets the exact target position under its contract |

`SHIP_THRUSTER_1` has an empty `modifier` (`00_utilities_thrusters.txt:95–96`); the only ship_modifier on `HYPER_DRIVE_1` is disengage opportunities (`00_utilities_drives.txt:111`). Their technologies are start_tech (`common/technology/00_eng_tech.txt:2078`, `00_phys_tech.txt:2558`). This confirms that the baseline components are available, but not the full starting fleet composition of each empire. The derived 7.2 units/day and 15 days apply to the unmodified baseline; no runtime measurement was taken. The new instruction to take parameters from the game does not turn the approved 5 days and N01 geometry into vanilla values.

## Acceptance

Dates day0/day29/day30/day359/day360; a monthly economy error on day30 does not change date/positions/balances; repeating stepDay does not close a month twice. Zero distance, exact arrival, remaining day fraction across phases, distant target, unreachable route, tied paths, cancel approach/preparation, stop/replace during transit, pause in all phases, AFK, and reconnect. Repeating commandId does not extend preparation or move twice. A hidden/foreign/nonexistent target produces a safe rejection. The implementation is not claimed to be an exact simulation of Clausewitz movement.
